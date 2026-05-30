using Rewired.UI.ControlMapper;
using Rewired.Utils;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CupheadRewiredCompat;
// Information from Stack Overflow answers
internal class MapperScroller : MonoBehaviour
{
    private float scrollSpeed = 10f;
    internal ScrollRect scrollRect;
    internal List<ScrollRect> otherScrolls = new List<ScrollRect>();
    private RectTransform scrollViewport, scrollContent, m_SelectedRectTransform, refOfParent;
    private ControlMapper mapper;
    private float m_verticalPosition = 1f;
    private int prevIndex = 0;

    internal void Initialize(ScrollRect scrollRect, ControlMapper mapper)
    {
        this.scrollRect = scrollRect;
        scrollViewport = scrollRect.viewport;
        scrollContent = scrollRect.content;
        this.mapper = mapper;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = scrollSpeed;
        scrollRect.SetLayoutVertical();
        SetSets();
        refOfParent = scrollContent.Find("ControllerColumn") as RectTransform;
    }

    internal void SetSets()
    {
        if (mapper == null) return;
        OnDestroy();
        var fixedSelectableUIElements = mapper.inputGrid.list.entries.list[0].value.actionList.SelectMany(x => x.fieldSets.list.SelectMany(x => x.value.fields.list.Select(x => x.value.uiElementInfo))).ToArray();
        foreach (var info in fixedSelectableUIElements)
            info.OnSelectedEvent += UpdateScrollToSelected;
        for (int i = fixedSelectableUIElements.Length - 1; i >= 0; i--)
            fixedSelectableUIElements[i].transform.parent.SetAsFirstSibling();
        for (int i = mapper.axisToggleObjects.Count - 1; i >= 0; i--)
        {
            var toggle = mapper.axisToggleObjects[i];
            if (toggle.transform.IsChildOf(scrollContent))
            {
                toggle.transform.parent.SetAsLastSibling();
                var element = UnityTools.GetComponent<UIElementInfo>(toggle);
                if (element != null)
                    element.OnSelectedEvent += UpdateScrollToSelected;
            }
        }
    }

    private void OnDisable()
    {
        m_verticalPosition = 1f;
        prevIndex = 0;
    }

    private void OnDestroy()
    {
        if (mapper == null) return;
        var fixedSelectableUIElements = mapper.inputGrid.list.entries.list[0].value.actionList.SelectMany(x => x.fieldSets.list.SelectMany(x => x.value.fields.list.Select(x => x.value.uiElementInfo)));
        foreach (var info in fixedSelectableUIElements)
            info.OnSelectedEvent -= UpdateScrollToSelected;
        foreach (var toggle in mapper.axisToggleObjects)
        {
            if (toggle.transform.IsChildOf(scrollContent))
            {
                var element = UnityTools.GetComponent<UIElementInfo>(toggle);
                if (element != null)
                    element.OnSelectedEvent -= UpdateScrollToSelected;
            }
        }
    }

    private void LateUpdate()
    {
        if (refOfParent == null)
            refOfParent = scrollContent.Find("ControllerColumn") as RectTransform;
        scrollRect.verticalNormalizedPosition = Mathf.Lerp(scrollRect.verticalNormalizedPosition, m_verticalPosition, Time.unscaledDeltaTime * scrollSpeed);
        foreach (var rect in otherScrolls)
        {
            rect.content.sizeDelta = scrollRect.content.sizeDelta;
            rect.verticalNormalizedPosition = Mathf.Lerp(rect.verticalNormalizedPosition, m_verticalPosition, Time.unscaledDeltaTime * scrollSpeed);
        }
    }

    private void UpdateScrollToSelected(GameObject selected)
    {
        m_SelectedRectTransform = selected.transform.parent.GetComponent<RectTransform>();
        var scrollRect = this.scrollRect;
        var contentRectTransform = this.scrollContent;
        var viewportRectTransform = this.scrollViewport;
        if (!m_SelectedRectTransform.IsChildOf(contentRectTransform)) return;
        var index = m_SelectedRectTransform.GetSiblingIndex();

        if (index == 0)
        {
            m_verticalPosition = 1f;
            prevIndex = index;
        }
        else if (index == (refOfParent.childCount - 1))
        {
            m_verticalPosition = 0f;
            prevIndex = index;
        }
        else if (index >= (prevIndex + 14) || index <= (prevIndex - 14)) // I could not math it out.
        {
            m_verticalPosition = 1f - ((float)index / (float)(refOfParent.childCount - 1));
            prevIndex = index;
        }
        //m_SelectedRectTransform.position.y < viewportRectTransform.position.y || m_SelectedRectTransform.position.y > viewportRectTransform.position.y
        /*else if (childrect.y < viewrect.yMin
            || childrect.y > -Mathf.Abs(viewrect.yMax))
            m_verticalPosition = 1f - ((float)index / (float)(refOfParent.childCount - 1));*/
        //m_verticalPosition = 1f - ((float)m_SelectedRectTransform.GetSiblingIndex() / (m_SelectedRectTransform.parent.childCount - 1));
        /*Canvas.ForceUpdateCanvases();
        contentRectTransform.anchoredPosition = new(
            contentRectTransform.anchoredPosition.x,
            ((Vector2)scrollRect.transform.InverseTransformPoint(contentRectTransform.position)
            - (Vector2)scrollRect.transform.InverseTransformPoint(m_SelectedRectTransform.position)).y
            );*/
    }
}