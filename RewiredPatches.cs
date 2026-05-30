using HarmonyLib;
using Rewired;
using Rewired.Data;
using Rewired.UI.ControlMapper;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.UI;

namespace CupheadRewiredCompat;

[HarmonyPatch]
internal static class RewiredPatches
{
    [HarmonyPatch(typeof(InputManager_Base), "Start"), HarmonyPostfix]
    private static void SetUserData(ref UserData ____userData) => ____userData = ReInput.UserData;
    [HarmonyPatch(typeof(InputManager_Base), nameof(InputManager_Base.userData), MethodType.Getter), HarmonyPostfix]
    private static void GetStaticUserData(ref UserData __result) => __result = ReInput.UserData;
    [HarmonyPatch(typeof(UserDataStore_Cuphead), "Save"), HarmonyPostfix]
    private static void Save()
    {
        IList<Player> allPlayers = ReInput.players.GetPlayers(false);
        for (int i = 0; i < allPlayers.Count; i++)
            RewiredCupheadManager.Save(allPlayers[i]);
    }
    [HarmonyPatch(typeof(UserDataStore_Cuphead), "Load")]
    //[HarmonyPatch(typeof(SettingsData), "ApplySettingsOnStartup")]
    [HarmonyPatch(typeof(SlotSelectScreen), "Start")]
    [HarmonyPostfix]
    private static void Load()
    {
        IList<Player> allPlayers = ReInput.players.GetPlayers(false);
        for (int i = 0; i < allPlayers.Count; i++)
            RewiredCupheadManager.Load(allPlayers[i]);
    }

    [HarmonyPatch(typeof(ControlMapper), nameof(ControlMapper.Initialize)), HarmonyPostfix]
    private static void InsertMissingActions(ControlMapper __instance) => LoadPages(__instance);
    [HarmonyPatch(typeof(ControlMapper), nameof(ControlMapper.Open), []), HarmonyPrefix]
    private static void LoadPages(ControlMapper __instance)
    {
        if (!__instance.initialized) return;
        /*foreach (var page in RewiredCupheadManager.newPages)
        {
            if (__instance._mappingSets[(int)page.Value]._actionCategoryIds.Length > (int)page.Key)
            {
                __instance._mappingSets[(int)page.Value]._actionCategoryIds = __instance._mappingSets[(int)page.Value]._actionCategoryIds.AddToArray((int)page.Key);
                __instance._mappingSets[(int)page.Value]._actionCategoryIdsReadOnly = new ReadOnlyCollection<int>(__instance._mappingSets[(int)page.Value]._actionCategoryIds);
            }
        }*/
        bool settorefresh = false;
        foreach (var action in RewiredCupheadManager.Actions)
            if (!__instance.currentMappingSet._actionIds.Contains(action.Value.id))
            {
                __instance.currentMappingSet._actionIds = __instance.currentMappingSet._actionIds.AddToArray(action.Value.id);
                __instance.currentMappingSet._actionIdsReadOnly = new ReadOnlyCollection<int>(__instance.currentMappingSet._actionIds);
                settorefresh = true;
            }
        if (settorefresh)
        {
            //labelstoResize.Clear();
            __instance.lastUISelection = null;
            __instance.axisToggleObjects.Clear();
            __instance.inactiveAxisToggleObjects.Clear();
            GameObject.DestroyImmediate(__instance.references.inputGridInnerGroup.parent?.GetComponent<MapperScroller>());
            GameObject.DestroyImmediate(__instance.references.actionsColumn?.Find("ScrollRect")?.GetComponent<MapperScroller>());
            __instance.CreateInputGrid();
            __instance.CreateLayout();
            /*foreach (var action in RewiredCupheadManager.Actions)
            {
                var themap = ReInput.mapping.GetActionElementMap(action.Value.id);
                var entry = __instance.inputGrid.list.GetActionEntry(1, action.Value.id, AxisRange.Positive);
                if (entry.fieldSets.TryGet((int)ControllerType.Keyboard, out var _keyboard))
                    foreach (var field in _keyboard.fields.list)
                        field.value.SetLabel(themap.keyCode.ToString());
                if (entry.fieldSets.TryGet((int)ControllerType.Joystick, out var _joystick))
                    foreach (var field in _joystick.fields.list)
                        field.value.SetLabel(themap.controllerMap);
            }*/
            __instance.Redraw(true, false);
        }
    }
    [HarmonyPatch(typeof(ControlMapper), nameof(ControlMapper.Open), []), HarmonyPostfix]
    private static void ResetSets(ControlMapper __instance)
    {
        __instance.references.inputGridInnerGroup?.parent?.GetComponent<MapperScroller>().SetSets();
        //__instance.references.actionsColumn?.Find("ScrollRect")?.GetComponent<MapperScroller>().SetSets();
    }
    [HarmonyPatch(typeof(ControlMapper), "OnRestoreDefaultsConfirmed")]
    [HarmonyPostfix]
    private static void RestoreDefaults(ControlMapper __instance)
    {
        RewiredCupheadManager.RestoreDefaults(__instance.currentPlayer);
        __instance.Redraw(false, false);
    }

    [HarmonyPatch(typeof(ControlMapper), nameof(ControlMapper.Initialize)), HarmonyPostfix]
    private static void SetSomeVars(ControlMapper __instance)
    {
        __instance.allowElementAssignmentConflicts = true;
    }

    [HarmonyPatch(typeof(ControlMapper), "CreateInputGrid")]
    [HarmonyPostfix]
    private static void MaskEm(ControlMapper __instance) // This part got extremely tricky to math, fix, and align correctly.
    {
        var column = __instance.references.inputGridInnerGroup;
        if (column.parent.GetComponent<MapperScroller>() == null)
        {
            _ = column.parent.GetComponent<RectMask2D>() ?? column.parent.gameObject.AddComponent<RectMask2D>();
            var scrollrect = column.parent.parent.GetComponent<ScrollRect>() ?? column.parent.parent.gameObject.AddComponent<ScrollRect>();
            Canvas.ForceUpdateCanvases();
            /*var scrollbar = column.gameObject.AddComponent<Scrollbar>();
            scrollbar.SetDirection(Scrollbar.Direction.TopToBottom, true);
            scrollbar.transition = Selectable.Transition.None;
            scrollbar.interactable = false;*/
            scrollrect.horizontal = false;
            scrollrect.vertical = true;
            scrollrect.content = column as RectTransform;
            scrollrect.viewport = column.parent as RectTransform;
            column.parent.gameObject.AddComponent<MapperScroller>().Initialize(scrollrect, __instance);
            (column.parent.GetComponent<Image>() ?? column.parent.gameObject.AddComponent<Image>()).enabled = false;
        }

        column = __instance.references.actionsColumn;
        if (column.Find("ScrollRect")?.GetComponent<MapperScroller>() == null)
        {
            var scrollrect = column.Find("ScrollRect") ?? new GameObject("ScrollRect", typeof(RectTransform), typeof(RectMask2D), typeof(Image)).transform;
            if (scrollrect.transform.parent == null)
            {
                scrollrect.gameObject.layer = LayerMask.NameToLayer("UI");
                scrollrect.transform.SetParent(column, false);
                scrollrect.GetComponent<RectTransform>().anchorMin = new(0f, 0f);
                scrollrect.GetComponent<RectTransform>().anchorMax = new(1f, 1f);
                scrollrect.GetComponent<RectTransform>().pivot = new(0f, 1f);
                scrollrect.GetComponent<RectTransform>().sizeDelta = Vector2.down * 62f;
                scrollrect.GetComponent<RectTransform>().anchoredPosition = Vector2.down * 62f;
            }
            var container = scrollrect.transform.Find("Container") ?? new GameObject("Container", typeof(RectTransform)).transform;
            if (container.transform.parent == null)
            {
                container.gameObject.layer = LayerMask.NameToLayer("UI");
                container.transform.SetParent(scrollrect.transform, false);
                container.GetComponent<RectTransform>().anchorMin = new(0f, 1f);
                container.GetComponent<RectTransform>().anchorMax = new(1f, 1f);
                container.GetComponent<RectTransform>().pivot = new(0f, 1f);
            }
            var actualscrollrect = column.GetComponent<ScrollRect>() ?? column.gameObject.AddComponent<ScrollRect>();
            Canvas.ForceUpdateCanvases();
            /*var layoutgr = container.GetComponent<HorizontalLayoutGroup>() ?? container.gameObject.AddComponent<HorizontalLayoutGroup>();
            layoutgr.childForceExpandHeight = false;
            layoutgr.childForceExpandWidth = false;
            layoutgr.spacing = -5f;
            layoutgr.SetLayoutHorizontal();*/
            actualscrollrect.horizontal = false;
            actualscrollrect.vertical = true;
            var thechild = __instance.references.inputGridActionColumn;
            thechild.SetParent(container.transform);
            actualscrollrect.content = thechild.parent as RectTransform;
            actualscrollrect.viewport = actualscrollrect.content.parent as RectTransform;
            actualscrollrect.movementType = ScrollRect.MovementType.Clamped;
            scrollrect.gameObject.AddComponent<MapperScroller>().enabled = false; //.Initialize(actualscrollrect, __instance);
            scrollrect.GetComponent<Image>().enabled = false;
            scrollrect.GetComponent<MapperScroller>().enabled = false;
            __instance.references.inputGridInnerGroup.parent.GetComponent<MapperScroller>().otherScrolls.Add(actualscrollrect);
            //container.GetComponent<RectTransform>().sizeDelta = __instance.references.inputGridInnerGroup.GetComponent<RectTransform>().sizeDelta;
        }
        else
        {
            var scrollrect = column.Find("ScrollRect");
            var actualscrollrect = column.GetComponent<ScrollRect>();
            var container = scrollrect.transform.Find("Container");
            var thechild = __instance.references.inputGridActionColumn;
            thechild.SetParent(container.transform);
            actualscrollrect.horizontal = false;
            actualscrollrect.vertical = true;
            actualscrollrect.content = thechild.parent as RectTransform;
            actualscrollrect.viewport = actualscrollrect.content.parent as RectTransform;
            /*scrollrect.GetComponent<MapperScroller>().Initialize(actualscrollrect, __instance);
            scrollrect.GetComponent<MapperScroller>().enabled = false;*/
            //container.GetComponent<RectTransform>().sizeDelta = __instance.references.inputGridInnerGroup.GetComponent<RectTransform>().sizeDelta;
        }
    }

    /*private static readonly HashSet<ControlMapper.GUILabel> labelstoResize = new HashSet<ControlMapper.GUILabel>();
    [HarmonyPatch(typeof(ControlMapper), "CreateInputGrid"), HarmonyPrefix]
    private static void Refresh() => labelstoResize.Clear();*/
    [HarmonyPatch(typeof(ControlMapper), "CreateInputFieldSet"), HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> MakeThemAlign(IEnumerable<CodeInstruction> _instructions)
    {
        var instructions = _instructions.ToList();
        object operandTarget = null;
        for (int i = 0; i < instructions.Count; i++)
        {
            if (instructions[i].opcode == OpCodes.Ldloc_S && instructions[i + 1].OperandIs(5f) && instructions[i + 2].opcode == OpCodes.Ldloc_S
                && instructions[i + 3].Is(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(RectTransform), nameof(RectTransform.offsetMin))) &&
                instructions[i + 4].opcode == OpCodes.Stloc_S && instructions[i + 5].opcode == OpCodes.Ldloca_S)
            {
                operandTarget = instructions[i].operand;
                continue;
            }
            else if (instructions[i].opcode == OpCodes.Ldarg_0 && instructions[i + 1].Is(OpCodes.Ldfld, AccessTools.Field(typeof(ControlMapper), nameof(ControlMapper.axisToggleObjects)))
                && instructions[i + 2].opcode == OpCodes.Ldloc_S && instructions[i + 3].Is(OpCodes.Ldfld, AccessTools.Field(typeof(ControlMapper.GUIElement), nameof(ControlMapper.GUIElement.gameObject)))
                && instructions[i + 4].opcode == OpCodes.Callvirt)
            {
                if (operandTarget == null)
                    throw new NullReferenceException("Operand Target is not assigned!! What happened??");
                instructions.InsertRange(i, [
                    new CodeInstruction(OpCodes.Ldloc_S, operandTarget),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    Transpilers.EmitDelegate<Action<RectTransform, ControlMapper>>((component2, __instance) => {
                        component2.offsetMax -= Vector2.up * (__instance._inputRowHeight * RewiredCupheadManager.Actions.Count); // The soluton, but well...
                    })
                    ]);
                break;
            }
        }
        return instructions.AsEnumerable();
    }
    /*[HarmonyPatch(typeof(ControlMapper.InputGridEntryList.MapCategoryEntry), "SetAllActive"), HarmonyPostfix]
    private static void SetVisibilityCheckboxs(bool state)
    {
        foreach (var label in labelstoResize)
            label.SetActive(state);
    }
    [HarmonyPatch(typeof(ControlMapper.InputGridEntryList), "Show"), HarmonyPostfix]
    private static void ShowCheckboxes(int mapCategoryId)
    {
        foreach (var label in labelstoResize)
            label.SetActive(true);
    }*/
}

#if DEBUG
[HarmonyPatch]
static class DebugPatches
{
    [HarmonyPatch(typeof(SettingsData), "ApplySettingsOnStartup"), HarmonyPostfix]
    private static void InsertNewBind()
    {
        if (RewiredCupheadManager.CreateNewInput("InstantlyKillBoss", "Instantly Kill Boss", key: UnityEngine.KeyCode.K))
        {
            var key = Localization.Instance.AddKey();
            key.key = "Instantly Kill Boss";
            key.translations[(int)Localization.Languages.English].text = "KILL EVERYONE";
            key.category = Localization.Categories.RemappingButton;
        }
        if (RewiredCupheadManager.CreateNewInput("NothingInput", "Nothing Input"))
        {
            var key = Localization.Instance.AddKey();
            key.key = "Nothing Input";
            key.translations[(int)Localization.Languages.English].text = "Do Nothing";
            key.category = Localization.Categories.RemappingButton;
        }
    }
    [HarmonyPatch(typeof(Level), "Update"), HarmonyPostfix]
    private static void KillThem(Level __instance) // This seems like cheating, but you better not reimplement this.
    {
        if (__instance.Started && !__instance.Ending)
        {
            for (int i = 0; i < __instance.players.Length; i++)
            {
                if (__instance.players[i] == null) continue;
                if (__instance.players[i].input.GetButton("InstantlyKillBoss"))
                {
                    foreach (var entity in GameObject.FindObjectsOfType<DamageReceiver>().Where(x => x.gameObject.GetComponent<AbstractPlayerController>() == null))
                        entity.TakeDamageBruteForce(new DamageDealer.DamageInfo(float.MaxValue, DamageDealer.Direction.Neutral, default, DamageDealer.DamageSource.Super));
                    break;
                }    
            }
        }
    }
}
#endif