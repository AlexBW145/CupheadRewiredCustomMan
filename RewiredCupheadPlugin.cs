using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Rewired;
using Rewired.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace CupheadRewiredCompat;

[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
public class RewiredCupheadPlugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger = new ManualLogSource("Rewired Compat API");
    private const string
        PLUGIN_GUID = "alexbw145.cuphead.rewiredcompat",
        PLUGIN_NAME = "Rewired Compat API",
        PLUGIN_VERSION = "1.0.0.1";
    public static string GUID => PLUGIN_GUID;

    private void Awake()
    {
        Logger = base.Logger;
        new Harmony(PLUGIN_GUID).PatchAll();
    }
}

[Serializable]
internal class RewiredCupheadData()
{
    public string actionName;
    public ControllerType controllerType;
    public int controllerId;

    public KeyCode keyCode;
    public ModifierKeyFlags modifierKeys;
    public int elementIdentifier;
    public Pole axisContribution;
    public ControllerElementType elementType;
    public AxisRange axisRange;
    public bool invert;

    public RewiredCupheadData(ActionElementMap elementMap) : this()
    {
        actionName = ReInput.UserData.GetActionById(elementMap.actionId).name;
        controllerType = elementMap.controllerMap.controllerType;
        controllerId = elementMap.controllerMap.controllerId;

        keyCode = elementMap.keyCode;
        modifierKeys = elementMap.modifierKeyFlags;
        elementIdentifier = elementMap.elementIdentifierId;
        axisContribution = elementMap.axisContribution;
        elementType = elementMap.elementType;
        axisRange = elementMap.axisRange;
        invert = elementMap.invert;
    }
}

public static partial class RewiredCupheadManager
{
    internal static List<ActionElementMap> GetElements(Player player)
    {
        var list = new List<ActionElementMap>();
        foreach (var map in player.controllers.maps.GetAllMaps())
            map.AllMaps.DoIf(x => x.actionId > ReInput.UserData.actions.Count, list.Add); // DO NOT ADD IN INPUTS TO THIS LIST, THESE ARE LEFT BY DEFAULT.
        return list;
    }
    internal static void Save(Player player)
    {
        string pathToFile = Path.Combine(Application.persistentDataPath, $"customRewiredInput_player{player.id}.json");
        List<RewiredCupheadData> inputs;
        if (File.Exists(pathToFile))
            inputs = JsonHelper.FromJson<RewiredCupheadData>(File.ReadAllText(pathToFile)).ToList();
        else
            inputs = new List<RewiredCupheadData>();
        var list = GetElements(player);
        foreach (var action in actions)
        {
            foreach (var act in list.FindAll(x => x.actionId == action.Value.id))
            {
                inputs.RemoveAll(x => x.actionName == action.Value.name && x.controllerId == act.controllerMap.controllerId && x.controllerType == act.controllerMap.controllerType && x.axisRange == act.axisRange && x.axisContribution == act.axisContribution);
                inputs.Add(new(act));
            }
            for (int i = 0; i < inputs.Count; i++)
            { // Makes unassigned inputs "unassigned" so that it will not reload to default bindings again.
                if (inputs[i].actionName == action.Value.name && !list.Exists(j => j.actionId == action.Value.id && inputs[i].controllerId == j.controllerMap.controllerId && inputs[i].controllerType == j.controllerMap.controllerType && j.axisRange == inputs[i].axisRange && j.axisContribution == inputs[i].axisContribution))
                {
                    inputs[i] = new RewiredCupheadData()
                    {
                        actionName = action.Value.name,
                        controllerType = inputs[i].controllerType,
                        elementType = inputs[i].elementType,
                        controllerId = inputs[i].controllerId,
                        axisRange = inputs[i].axisRange,
                        axisContribution = inputs[i].axisContribution,

                        elementIdentifier = -1,
                    };
                }
            }
        }
        var json = JsonHelper.ToJson(inputs.ToArray(), true);
        File.WriteAllText(pathToFile, json);
    }
    private static void SetBind(Player player, InputAction action, KeyCode keycode, Pole pole = Pole.Positive)
    {
        player.controllers.maps.GetFirstMapInCategory(ControllerType.Keyboard, 0, action.categoryId)?.CreateElementMap(action.id, pole, keycode, ModifierKeyFlags.None);
    }
    private static void SetBind(Player player, InputAction action, int elementIdent, ControllerType type, Pole pole = Pole.Positive, bool fullRange = false)
    {
        if (type == ControllerType.Joystick && player.controllers.joystickCount > 0)
            player.controllers.maps.GetFirstMapInCategory(type, 0, action.categoryId)?.CreateElementMap(action.id, pole, elementIdent, (ControllerElementType)action.type, fullRange ? AxisRange.Full : (AxisRange)(pole + 1), false);
    }
    internal static void RestoreDefaults(Player player)
    {
        UserDataStore save = (UserDataStore)ReInput.userDataStore;
        bool saveNow = false;
        foreach (var action in actions)
        {
            if (player.controllers.maps.GetFirstMapInCategory(ControllerType.Keyboard, 0, action.Value.categoryId)?.DeleteElementMapsWithAction(action.Value.id) == true)
                saveNow = true;
            if (player.controllers.maps.GetFirstMapInCategory(ControllerType.Joystick, 0, action.Value.categoryId)?.DeleteElementMapsWithAction(action.Value.id) == true)
                saveNow = true;
            if (defaultKeyboardBinds.ContainsKey(action.Value))
            {
                KeyCode keycode = defaultKeyboardBinds[action.Value];
                SetBind(player, action.Value, keycode);
                if (keycode != KeyCode.None)
                    saveNow = true;
            }
            if (defaultJoystickBinds.ContainsKey(action.Value))
            {
                SetBind(player, action.Value, defaultJoystickBinds[action.Value], ControllerType.Joystick);
                if (defaultJoystickBinds[action.Value] != -1)
                    saveNow = true;
            }
            /*if (defaultMouseBinds.ContainsKey(action.Value))
            {
                if (string.IsNullOrEmpty(action.Value.key))
                {
                    if (defaultMouseBinds[action.Value].Item1 != -1)
                        SetBind(player, action.Value, defaultMouseBinds[action.Value].Item1, ControllerType.Mouse, Pole.Positive, false);
                    if (defaultMouseBinds[action.Value].Item2 != -1)
                        SetBind(player, action.Value, defaultMouseBinds[action.Value].Item2, ControllerType.Mouse, Pole.Negative, false);
                }
                else if (defaultMouseBinds[action.Value].Item1 != -1)
                    SetBind(player, action.Value, defaultMouseBinds[action.Value].Item1, ControllerType.Mouse);
            }*/
        }
        if (saveNow)
            Save(player);
    }
    internal static void Load(Player player)
    {
        string pathToFile = Path.Combine(Application.persistentDataPath, $"customRewiredInput_player{player.id}.json");
        if (!File.Exists(pathToFile))
        {
            RestoreDefaults(player);
            return;
        }
        UserDataStore save = (UserDataStore)ReInput.userDataStore;
        bool saveNow = false;
        List<RewiredCupheadData> inputs = JsonHelper.FromJson<RewiredCupheadData>(File.ReadAllText(pathToFile)).ToList();
        foreach (var input in inputs.Where(x => x.elementIdentifier != -1))
        {
            if (actions.ContainsKey(input.actionName))
            {
                ActionElementMap map = null;
                player.controllers.maps.GetFirstMapInCategory(input.controllerType, input.controllerId, actions[input.actionName].categoryId)?.ReplaceOrCreateElementMap(new(input.controllerType, input.elementType, input.elementIdentifier, input.axisRange, input.keyCode, input.modifierKeys, actions[input.actionName].id, input.axisContribution, input.invert), out map);
                if (map != null)
                    map._actionCategoryId = 0;
            }
        }
        foreach (var action in actions.Where(x => defaultKeyboardBinds.ContainsKey(x.Value) && !inputs.Exists(j => x.Key == j.actionName && j.controllerType == ControllerType.Keyboard)))
        {
            KeyCode keycode = defaultKeyboardBinds[action.Value];
            SetBind(player, action.Value, keycode);
            if (keycode != KeyCode.None)
                saveNow = true;
        }
        foreach (var action in actions.Where(x => defaultJoystickBinds.ContainsKey(x.Value) && !inputs.Exists(j => x.Key == j.actionName && j.controllerType == ControllerType.Joystick)))
        {
            if (player.controllers.joystickCount > 0)
            {
                SetBind(player, action.Value, defaultJoystickBinds[action.Value], ControllerType.Joystick);
                if (defaultJoystickBinds[action.Value] != -1)
                    saveNow = true;
            }
        }
        /*foreach (var action in actions.Where(x => defaultMouseBinds.ContainsKey(x.Value)))
        {
            if (!inputs.Exists(j => action.Key == j.actionName && j.controllerType == ControllerType.Mouse))
            {
                saveNow = true;
                if (defaultMouseBinds[action.Value].Item1 != -1)
                    SetBind(player, action.Value, defaultMouseBinds[action.Value].Item1, ControllerType.Mouse);
            }
        }*/
        if (saveNow)
            Save(player);
    }
    private static readonly Dictionary<string, Rewired.InputAction> actions = new Dictionary<string, Rewired.InputAction>();
    private static readonly Dictionary<Rewired.InputAction, int> defaultJoystickBinds = new Dictionary<Rewired.InputAction, int>();
    private static readonly Dictionary<InputAction, KeyCode> defaultKeyboardBinds = new Dictionary<InputAction, KeyCode>();
    internal static Dictionary<string, Rewired.InputAction> Actions => actions;
    public enum InputMapCategory
    {
        Default = 0,
        Gameplay = 1,
        Menu = 2,
    }
    public enum InputBehaviorID
    {
        Default = 0, // Only defined behavior I believe.
    }
    private static void DoInsertsToRewired(Rewired.InputAction action, Rewired.InputBehavior behavior)
    {
        ReInput.tdTWtspcdVUsJxDobyFshlWZZIL.JhFZnlMQZcqqLYFqLvbSEVdmnjk = ReInput.tdTWtspcdVUsJxDobyFshlWZZIL.JhFZnlMQZcqqLYFqLvbSEVdmnjk.AddToArray(action);
        ReInput.tdTWtspcdVUsJxDobyFshlWZZIL.GGbAAbYzHgJMXoYwauUBzwKpqyS = ReInput.tdTWtspcdVUsJxDobyFshlWZZIL.JhFZnlMQZcqqLYFqLvbSEVdmnjk.Length;
        if (action.id > ReInput.tdTWtspcdVUsJxDobyFshlWZZIL.yBtGRlaotyblazOPhBnpecuykzrO)
            ReInput.tdTWtspcdVUsJxDobyFshlWZZIL.yBtGRlaotyblazOPhBnpecuykzrO = action.id;
        var data = new CLzPFzWvgKOhZUdeCaluIJOyLQaD.OvzMWrXrNTMbzNpCKoiHERLRJOL(action, ReInput.tdTWtspcdVUsJxDobyFshlWZZIL.GGbAAbYzHgJMXoYwauUBzwKpqyS - 1);
        ReInput.tdTWtspcdVUsJxDobyFshlWZZIL.weedBjfEsbwnTSxubTBXfOblyzS = ReInput.tdTWtspcdVUsJxDobyFshlWZZIL.weedBjfEsbwnTSxubTBXfOblyzS.AddToArray(data);
        ReInput.tdTWtspcdVUsJxDobyFshlWZZIL.sqtGZrTZHRHnZZzZnSIcRahaLtH.Add(action.name, data);
        ReInput.tdTWtspcdVUsJxDobyFshlWZZIL.klRnveTjMcXQtieIxHxXqsTEtnc = new System.Collections.ObjectModel.ReadOnlyCollection<InputAction>(ReInput.tdTWtspcdVUsJxDobyFshlWZZIL.JhFZnlMQZcqqLYFqLvbSEVdmnjk);

        ReInput.aWJpTgzRccgkwdHZpFdZdXEMAQnY.BqsVjuAsWZQAzBfjxkcKefOkUjR = ReInput.tdTWtspcdVUsJxDobyFshlWZZIL.GGbAAbYzHgJMXoYwauUBzwKpqyS;
        MqAVPVNyaeLuVfMyAzTjbrPDxyU inputdata = new MqAVPVNyaeLuVfMyAzTjbrPDxyU(ReInput.players.SystemPlayer.id, action.id, action.name, behavior, ReInput.configVars);
        ReInput.aWJpTgzRccgkwdHZpFdZdXEMAQnY.UeyOxJZOezuYwYdlglPxoumllql = ReInput.aWJpTgzRccgkwdHZpFdZdXEMAQnY.UeyOxJZOezuYwYdlglPxoumllql.AddToArray(inputdata);
        ReInput.aWJpTgzRccgkwdHZpFdZdXEMAQnY.vufaaCdSmYAndFDIKAhyopjAsCU.AAGZHimCJtDQUeCeajoDdiVSVCQs = ReInput.aWJpTgzRccgkwdHZpFdZdXEMAQnY.vufaaCdSmYAndFDIKAhyopjAsCU.AAGZHimCJtDQUeCeajoDdiVSVCQs.AddToArray(0);
        var listofactiondatas = ReInput.aWJpTgzRccgkwdHZpFdZdXEMAQnY.HhpDMCrWHGHkiHcRAmyEshxkxCZ.ToList();
        listofactiondatas.Insert(ReInput.aWJpTgzRccgkwdHZpFdZdXEMAQnY.BqsVjuAsWZQAzBfjxkcKefOkUjR - 1, inputdata);
        var players = ReInput.players.GetPlayers(false);
        if (ReInput.aWJpTgzRccgkwdHZpFdZdXEMAQnY.fItxDGqNrhAtlrnLNKQODEHbEtc.GetLength(1) < ReInput.aWJpTgzRccgkwdHZpFdZdXEMAQnY.BqsVjuAsWZQAzBfjxkcKefOkUjR)
        {
            var twodarray = new MqAVPVNyaeLuVfMyAzTjbrPDxyU[players.Count, ReInput.aWJpTgzRccgkwdHZpFdZdXEMAQnY.BqsVjuAsWZQAzBfjxkcKefOkUjR];
            int exactlength = ReInput.aWJpTgzRccgkwdHZpFdZdXEMAQnY.fItxDGqNrhAtlrnLNKQODEHbEtc.GetLength(1) - 1;
            for (int i = 0; i < players.Count; i++)
            {
                for (int j = 0; j < ReInput.aWJpTgzRccgkwdHZpFdZdXEMAQnY.BqsVjuAsWZQAzBfjxkcKefOkUjR; j++)
                {
                    if (j < exactlength)
                        twodarray[i, j] = ReInput.aWJpTgzRccgkwdHZpFdZdXEMAQnY.fItxDGqNrhAtlrnLNKQODEHbEtc[i, j];
                    else
                    {
                        inputdata = new MqAVPVNyaeLuVfMyAzTjbrPDxyU(players[i].id, action.id, action.name, behavior, ReInput.configVars);
                        twodarray[i, j] = inputdata;
                        listofactiondatas.Insert((i + 2) * j, inputdata);
                        ReInput.aWJpTgzRccgkwdHZpFdZdXEMAQnY.scnyWrZHdTnAboBswvsMmmfOjaD[i].AAGZHimCJtDQUeCeajoDdiVSVCQs = ReInput.aWJpTgzRccgkwdHZpFdZdXEMAQnY.scnyWrZHdTnAboBswvsMmmfOjaD[i].AAGZHimCJtDQUeCeajoDdiVSVCQs.AddToArray(0);
                    }
                }
            }
            ReInput.aWJpTgzRccgkwdHZpFdZdXEMAQnY.fItxDGqNrhAtlrnLNKQODEHbEtc = twodarray;
        }
        ReInput.aWJpTgzRccgkwdHZpFdZdXEMAQnY.HhpDMCrWHGHkiHcRAmyEshxkxCZ = listofactiondatas.ToArray();
    }
    /// <summary>
    /// Creates a brand new button Rewired input.
    /// </summary>
    /// <param name="name">The object-like name of this input</param>
    /// <param name="descriptionName">The localization-like name of this input</param>
    /// <param name="categoryID">The category ID for this input</param>
    /// <param name="key">The default input for this key</param>
    /// <param name="joystickElementId">The default input id for this joystick input</param>
    /// <returns></returns>
    public static bool CreateNewInput(string name, string descriptionName, InputMapCategory categoryID = InputMapCategory.Gameplay,
        KeyCode key = KeyCode.None, int joystickElementId = -1)
    {
        if (name.IsNullOrWhiteSpace()) return false;
        var userData = ReInput.UserData;
        if (actions.ContainsKey(name) || userData.actions.Exists(x => x.name == name)) return false;
        try
        {
            var action = new Rewired.InputAction()
            {
                id = userData.GetNewActionId(),
                name = name,
                descriptiveName = descriptionName,
                type = InputActionType.Button,
                userAssignable = true,
                behaviorId = (int)InputBehaviorID.Default,
                categoryId = (int)categoryID,
            };
            if (key != KeyCode.None)
                defaultKeyboardBinds.Add(action, key);
            if (joystickElementId != -1)
                defaultJoystickBinds.Add(action, joystickElementId);
            actions.Add(name, action);
            userData.actions.Add(action);
            var behavior = userData.GetInputBehaviorById((int)InputBehaviorID.Default);
            if (behavior == null)
                throw new NullReferenceException("Behavior is null");
            userData.actionCategoryMap.list.Add(new Rewired.Data.Mapping.ActionCategoryMap.Entry((int)categoryID));
            userData.actionCategoryMap.AddAction((int)categoryID, action.id);
            DoInsertsToRewired(action, behavior);
            return true;
        }
        catch (Exception ex)
        {
            RewiredCupheadPlugin.Logger.LogError(ex);
            return false;
        }
    }
    /*/// <summary>
    /// Creates a brand new axis Rewired input.
    /// </summary>
    /// <param name="name">The object-like name of this input</param>
    /// <param name="descriptionName">The localization-like name of this input</param>
    /// <param name="behaviorID">The behavior ID for this input<para>Already defined ids are 0 (default) and 1 (snap, which all base game inputs uses)</para></param>
    /// <param name="categoryID">The category ID for this input</param>
    /// <param name="key">The default inputs for this key<para>As in order: PosX, NegX, PosY, NegY</para></param>
    /// <param name="joystickElementId">The default inputs id for this joystick input<para>As in order: PosX, NegX, PosY, NegY</para></param>
    /// <param name="mouseElementId">The default inputs id for this mouse input<para>As in order: PosX, NegX, PosY, NegY</para></param>
    /// <returns></returns>
    public static bool CreateNewInput(string name, string descriptionName, InputBehaviorID behaviorID, InputMapCategory categoryID,
        (KeyCode, KeyCode, KeyCode, KeyCode)? key = null, (int, int, int, int)? joystickElementId = null, (int, int, int, int)? mouseElementId = null)
    {
        var type = InputActionType.Axis;
        if (string.IsNullOrWhiteSpace(name)) return false;
        var userData = ReInput.UserData;
        if (actions.ContainsKey(name + "X") || actions.ContainsKey(name + "Y")
            || userData.actions.Exists(x => x.name == name + "X") || userData.actions.Exists(x => x.name == name + "Y")) return false;
        if (key == null) key = new(KeyCode.None, KeyCode.None, KeyCode.None, KeyCode.None);
        if (joystickElementId == null) joystickElementId = new(-1, -1, -1, -1);
        if (mouseElementId == null) mouseElementId = new(-1, -1, -1, -1);
        try
        {
            if (behaviorID > InputBehaviorID.Snap)
            {
                RewiredCupheadPlugin.Logger.LogWarning("Behavior ID has exceeded past 1, binding to 1! (Snap behavior)");
                behaviorID = InputBehaviorID.Snap;
            }
            for (int step = 0; step < 2; step++)
            {
                string xy = step == 0 ? "X" : "Y";
                var action = new Rewired.InputAction()
                {
                    id = userData.GetNewActionId(),
                    name = name + xy,
                    descriptiveName = descriptionName + " " + xy,
                    positiveDescriptiveName = descriptionName + " " + xy + "+",
                    negativeDescriptiveName = descriptionName + " " + xy + "-",
                    type = type,
                    userAssignable = true,
                    behaviorId = (int)behaviorID,
                    categoryId = (int)categoryID,
                };
                action.DJYOTBmJjtfSTvwDqbRIgptIirJuA();
                action.positiveKey = Keyboard.GetKeyName(step == 0 ? key.Value.Item1 : key.Value.Item3);
                action.negativeKey = Keyboard.GetKeyName(step == 0 ? key.Value.Item2 : key.Value.Item4);
                actions.Add(name + xy, action);
                userData.NUOyBxwHYBZqYgECChWsabGDMOVS.Add(action);
                var behavior = userData.GetInputBehaviorById((int)behaviorID); // Just do not go above 0 to 1, those are the already defined ones. (Especially when most uses the number 1)
                if (behavior == null)
                    throw new NullReferenceException("Behavior is null");
                userData.actionCategoryMap.list.Add(new Rewired.Data.Mapping.ActionCategoryMap.Entry((int)categoryID));
                userData.actionCategoryMap.AddAction((int)categoryID, action.id);
                DoInsertsToRewired(action, behavior);
                actionIsDigital.Add(name + xy, type == InputActionType.Button);
                if ((step == 0 ? (joystickElementId.Value.Item1, joystickElementId.Value.Item3) : (joystickElementId.Value.Item2, joystickElementId.Value.Item4)) != (-1, -1))
                    defaultJoystickBinds.Add(action, (step == 0 ? (joystickElementId.Value.Item1, joystickElementId.Value.Item3) : (joystickElementId.Value.Item2, joystickElementId.Value.Item4)));
                if ((step == 0 ? (mouseElementId.Value.Item1, mouseElementId.Value.Item3) : (mouseElementId.Value.Item2, mouseElementId.Value.Item4)) != (-1, -1))
                    defaultMouseBinds.Add(action, (step == 0 ? (mouseElementId.Value.Item1, mouseElementId.Value.Item3) : (mouseElementId.Value.Item2, mouseElementId.Value.Item4)));
            }
            return true;
        }
        catch (Exception ex)
        {
            RewiredCupheadPlugin.Logger.LogError(ex);
            return false;
        }
    }*/
    /// <summary>
    /// Grabs the input ID for this custom input
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static int GetInputID(string name)
    {
        if (!actions.ContainsKey(name)) return -1;
        return actions[name].id;
    }
    public static bool GetButton(this PlayerInput me, string inputName) => me.GetButton((CupheadButton)GetInputID(inputName));
    public enum InputMapPage
    {
        Default = 0, // There really is no way of creating a new category with one just taking over the main content.
    }
    internal static readonly Dictionary<InputMapCategory, InputMapPage> newPages = new Dictionary<InputMapCategory, InputMapPage>();
    /*/// <summary>
    /// Creates a brand new category to an existing Rewired Mapper Page
    /// </summary>
    /// <param name="name">The object-like name of this category</param>
    /// <param name="descriptionName">The localization-like name of this category</param>
    /// <param name="page">The existing page where this mapping category goes to</param>
    /// <returns></returns>
    public static InputMapCategory CreateNewCategory(string name, string descriptionName, InputMapPage page)
    {
        var userData = ReInput.UserData;
        try
        {
            InputActionCategory inputActionCategory = new InputActionCategory()
            {
                id = userData.GetNewActionCategoryId(),
                name = name,
            };
            inputActionCategory.descriptiveName = descriptionName;
            inputActionCategory.userAssignable = true;
            userData.actionCategories.Add(inputActionCategory);
            userData.actionCategoryMap.AddCategory(inputActionCategory.id);
            newPages.Add((InputMapCategory)inputActionCategory.id, page);
            return (InputMapCategory)inputActionCategory.id;
        }
        catch (Exception ex)
        {
            RewiredCupheadManager.Logger.LogError(ex);
            return (InputMapCategory)(-1);
        }
    }*/
    private static int GetCategoryID(Rewired.InputAction action)
    {
        if (newPages.ContainsKey((InputMapCategory)action.categoryId))
            return (int)newPages[(InputMapCategory)action.categoryId];
        return (InputMapCategory)action.categoryId switch
        {
            InputMapCategory.Default => -1, // Not implemented category
            InputMapCategory.Gameplay => 1,
            InputMapCategory.Menu => 2,
            _ => throw new Exception("Out of bound vanilla categories!!")
        };
    }
}