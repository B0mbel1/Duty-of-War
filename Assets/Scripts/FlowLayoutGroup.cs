using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[AddComponentMenu("Layout/Flow Layout Group")]
public class FlowLayoutGroup : LayoutGroup
{
    public float SpacingX = 0f;
    public float SpacingY = 0f;
    public bool ChildForceExpandWidth = false;
    public bool ChildForceExpandHeight = false;

    public override void CalculateLayoutInputHorizontal()
    {
        base.CalculateLayoutInputHorizontal();
        float minWidth = GetGreatestMinimumChildWidth() + padding.horizontal;
        float preferredWidth = GetGreatestPreferredChildWidth() + padding.horizontal;
        SetLayoutInputForAxis(minWidth, preferredWidth, -1, 0);
    }

    public override void CalculateLayoutInputVertical()
    {
        float minHeight = GetGreatestMinimumChildHeight() + padding.vertical;
        float preferredHeight = GetGreatestPreferredChildHeight() + padding.vertical;
        SetLayoutInputForAxis(minHeight, preferredHeight, -1, 1);
    }

    public override void SetLayoutHorizontal()
    {
        SetLayout(0, false);
    }

    public override void SetLayoutVertical()
    {
        SetLayout(1, false);
    }

    public float GetGreatestMinimumChildWidth()
    {
        float max = 0;
        foreach (RectTransform child in rectChildren)
        {
            float w = LayoutUtility.GetMinWidth(child);
            max = Mathf.Max(w, max);
        }
        return max;
    }

    public float GetGreatestPreferredChildWidth()
    {
        float max = 0;
        foreach (RectTransform child in rectChildren)
        {
            float w = LayoutUtility.GetPreferredWidth(child);
            max = Mathf.Max(w, max);
        }
        return max;
    }

    public float GetGreatestMinimumChildHeight()
    {
        float max = 0;
        foreach (RectTransform child in rectChildren)
        {
            float h = LayoutUtility.GetMinHeight(child);
            max = Mathf.Max(h, max);
        }
        return max;
    }

    public float GetGreatestPreferredChildHeight()
    {
        float max = 0;
        foreach (RectTransform child in rectChildren)
        {
            float h = LayoutUtility.GetPreferredHeight(child);
            max = Mathf.Max(h, max);
        }
        return max;
    }

    public void SetLayout(int axis, bool layoutInput)
    {
        float groupHeight = rectTransform.rect.height;
        float groupWidth = rectTransform.rect.width;

        float xOffset = padding.left;
        float yOffset = padding.top;
        float currentRowHeight = 0f;
        float currentWidth = 0f;

        // 1. Calculate logic
        for (int i = 0; i < rectChildren.Count; i++)
        {
            RectTransform child = rectChildren[i];
            float childWidth = LayoutUtility.GetPreferredWidth(child);
            float childHeight = LayoutUtility.GetPreferredHeight(child);

            // Jeśli element wychodzi poza szerokość kontenera -> Nowa linia
            if (xOffset + childWidth > groupWidth - padding.right && xOffset > padding.left)
            {
                xOffset = padding.left;
                yOffset += currentRowHeight + SpacingY;
                currentRowHeight = 0f;
            }

            if (axis == 0) // Horizontal pass
            {
                SetChildAlongAxis(child, 0, xOffset, childWidth);
            }
            else // Vertical pass
            {
                SetChildAlongAxis(child, 1, yOffset, childHeight);
            }

            xOffset += childWidth + SpacingX;
            currentRowHeight = Mathf.Max(currentRowHeight, childHeight);
        }
        
        // Opcjonalnie: Powiększanie kontenera w pionie
        if (axis == 1)
        {
            float totalHeight = yOffset + currentRowHeight + padding.bottom;
            SetLayoutInputForAxis(totalHeight, totalHeight, -1, 1);
        }
    }
}