using System;
using System.Collections.Generic;
using System.Linq;
using EFT.UI;
using EFT.UI.DragAndDrop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace VaultOwnerFilter;

internal sealed class VaultOwnerFilterPanel : MonoBehaviour
{
    private const float SelectorWidth = 154f;
    private const float SelectorRightOffset = 80f;
    private const float RowHeight = 28f;
    private const int MaxVisibleOptions = 7;
    private const int MaxOffersPerSeller = 20;

    private readonly List<GameObject> _optionButtons = new();

    private GameObject _panel = null!;
    private GameObject _menu = null!;
    private RectTransform _optionsContent = null!;
    private TradingGridView _traderGridView = null!;
    private TextMeshProUGUI _selectedText = null!;
    private TextMeshProUGUI? _textTemplate;
    private bool _initialized;

    internal void Initialize(TradingGridView traderGridView, DefaultUIButton updateAssortButton)
    {
        if (_initialized)
        {
            return;
        }

        _traderGridView = traderGridView;
        _textTemplate = updateAssortButton.transform.Find("TextWhite")?.GetComponent<TextMeshProUGUI>();

        var gridContainer = transform.Find("Left Person/Possessions Grid");
        if (gridContainer is null)
        {
            Plugin.Log.LogError("Unable to find trader possessions grid for Vault owner filter");
            return;
        }

        _panel = new GameObject("VaultOwnerFilterPanel", typeof(RectTransform));
        _panel.transform.SetParent(gridContainer, false);
        _panel.SetActive(false);

        var panelRect = (RectTransform)_panel.transform;
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 0f);
        panelRect.anchoredPosition = new Vector2(-SelectorRightOffset, 4f);
        panelRect.sizeDelta = new Vector2(SelectorWidth, RowHeight);

        CreateSelector(panelRect);
        CreateMenu(panelRect);
        _initialized = true;
    }

    internal void ShowForTrader(TraderClass trader)
    {
        if (!_initialized)
        {
            return;
        }

        var isVault = trader.Id.ToString().Equals(VaultOwnerState.VaultTraderId, StringComparison.OrdinalIgnoreCase);
        VaultOwnerState.SetActive(isVault);
        _panel.SetActive(isVault);
        CloseMenu();

        if (isVault)
        {
            RefreshOwners(rebuildGrid: false);
        }
    }

    internal void RefreshIfActive()
    {
        if (_initialized && VaultOwnerState.IsVaultActive)
        {
            RefreshOwners(rebuildGrid: true);
        }
    }

    private void CreateSelector(RectTransform parent)
    {
        var selector = CreateButtonObject("OwnerSelector", parent, new Color(0.12f, 0.12f, 0.12f, 0.96f));
        Stretch(selector.transform);

        _selectedText = CreateText("SelectedOwner", selector.transform);
        _selectedText.alignment = TextAlignmentOptions.MidlineLeft;
        var selectedRect = (RectTransform)_selectedText.transform;
        selectedRect.offsetMin = new Vector2(10f, 1f);
        selectedRect.offsetMax = new Vector2(-28f, -1f);

        var arrow = CreateText("Arrow", selector.transform);
        arrow.text = "▼";
        arrow.alignment = TextAlignmentOptions.Center;
        var arrowRect = (RectTransform)arrow.transform;
        arrowRect.anchorMin = new Vector2(1f, 0f);
        arrowRect.anchorMax = Vector2.one;
        arrowRect.pivot = new Vector2(1f, 0.5f);
        arrowRect.anchoredPosition = Vector2.zero;
        arrowRect.sizeDelta = new Vector2(28f, 0f);

        selector.GetComponent<Button>().onClick.AddListener(() =>
        {
            _menu.SetActive(!_menu.activeSelf);
            if (_menu.activeSelf)
            {
                _menu.transform.SetAsLastSibling();
            }
        });
    }

    private void CreateMenu(RectTransform parent)
    {
        _menu = new GameObject(
            "OwnerDropdown",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(ScrollRect));
        _menu.transform.SetParent(parent, false);
        _menu.GetComponent<Image>().color = new Color(0.055f, 0.055f, 0.055f, 0.99f);

        var menuRect = (RectTransform)_menu.transform;
        menuRect.anchorMin = new Vector2(0f, 1f);
        menuRect.anchorMax = new Vector2(1f, 1f);
        menuRect.pivot = new Vector2(0.5f, 1f);
        menuRect.anchoredPosition = new Vector2(0f, -2f);
        menuRect.sizeDelta = new Vector2(0f, RowHeight);

        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(_menu.transform, false);
        Stretch(viewport.transform, new Vector2(2f, 2f), new Vector2(-2f, -2f));
        viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        var content = new GameObject("Options", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        _optionsContent = (RectTransform)content.transform;
        _optionsContent.anchorMin = new Vector2(0f, 1f);
        _optionsContent.anchorMax = Vector2.one;
        _optionsContent.pivot = new Vector2(0.5f, 1f);
        _optionsContent.anchoredPosition = Vector2.zero;
        _optionsContent.sizeDelta = Vector2.zero;

        var layout = content.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 1f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = _menu.GetComponent<ScrollRect>();
        scroll.viewport = (RectTransform)viewport.transform;
        scroll.content = _optionsContent;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;

        _menu.SetActive(false);
    }

    private void RefreshOwners(bool rebuildGrid)
    {
        VaultOwnerState.Refresh();
        RebuildOptions();
        UpdateSelectionText();
        RefreshStackCounts();
        if (rebuildGrid)
        {
            RefreshGrid();
        }
    }

    private void RebuildOptions()
    {
        foreach (var option in _optionButtons)
        {
            Object.Destroy(option);
        }
        _optionButtons.Clear();

        AddOption($"All owners ({VaultOwnerState.Owners.Sum(owner => owner.OfferIds.Count)})", null);

        var duplicateNames = VaultOwnerState.Owners
            .GroupBy(owner => owner.SellerNickname, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var owner in VaultOwnerState.Owners)
        {
            AddOption(GetOwnerLabel(owner, duplicateNames), owner.SellerProfileId);
        }

        var rowCount = Math.Min(MaxVisibleOptions, _optionButtons.Count);
        ((RectTransform)_menu.transform).sizeDelta = new Vector2(
            0f,
            rowCount * RowHeight + Math.Max(0, rowCount - 1) + 4f);
        LayoutRebuilder.ForceRebuildLayoutImmediate(_optionsContent);
    }

    private void AddOption(string label, string? ownerId)
    {
        var selected = string.Equals(ownerId, VaultOwnerState.SelectedOwnerId, StringComparison.OrdinalIgnoreCase);
        var option = CreateButtonObject(
            $"Owner_{ownerId ?? "All"}",
            _optionsContent,
            selected ? new Color(0.15f, 0.43f, 0.68f, 0.98f) : new Color(0.12f, 0.12f, 0.12f, 0.96f));
        option.GetComponent<LayoutElement>().preferredHeight = RowHeight;

        var text = CreateText("Label", option.transform);
        text.text = label;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        var textRect = (RectTransform)text.transform;
        textRect.offsetMin = new Vector2(10f, 1f);
        textRect.offsetMax = new Vector2(-10f, -1f);

        option.GetComponent<Button>().onClick.AddListener(() =>
        {
            VaultOwnerState.SelectOwner(ownerId);
            UpdateSelectionText();
            CloseMenu();
            RefreshGrid();
        });

        _optionButtons.Add(option);
    }

    private void UpdateSelectionText()
    {
        if (VaultOwnerState.SelectedOwnerId is null)
        {
            _selectedText.text = $"Owner: All ({VaultOwnerState.Owners.Sum(owner => owner.OfferIds.Count)})";
            return;
        }

        var owner = VaultOwnerState.Owners.FirstOrDefault(candidate =>
            candidate.SellerProfileId.Equals(VaultOwnerState.SelectedOwnerId, StringComparison.OrdinalIgnoreCase));
        _selectedText.text = owner is null ? "Owner: All" : $"Owner: {GetOwnerLabel(owner, null)}";
    }

    private static string GetOwnerLabel(VaultOwner owner, HashSet<string>? duplicateNames)
    {
        var name = string.IsNullOrWhiteSpace(owner.SellerNickname)
            ? $"Player {ShortId(owner.SellerProfileId)}"
            : owner.SellerNickname;
        if (duplicateNames?.Contains(name) == true)
        {
            name = $"{name} {ShortId(owner.SellerProfileId)}";
        }
        if (owner.SellerProfileId.Equals(VaultOwnerState.CurrentProfileId, StringComparison.OrdinalIgnoreCase))
        {
            name = $"Mine: {name}";
        }
        return $"{name} ({owner.OfferIds.Count}/{MaxOffersPerSeller})";
    }

    private GameObject CreateButtonObject(string name, Transform parent, Color color)
    {
        var buttonObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);
        var image = buttonObject.GetComponent<Image>();
        image.color = color;
        buttonObject.GetComponent<Button>().targetGraphic = image;
        return buttonObject;
    }

    private TextMeshProUGUI CreateText(string name, Transform parent)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        Stretch(textObject.transform);

        var text = textObject.GetComponent<TextMeshProUGUI>();
        if (_textTemplate is not null)
        {
            text.font = _textTemplate.font;
            text.fontSharedMaterial = _textTemplate.fontSharedMaterial;
        }
        text.fontSize = 13f;
        text.color = Color.white;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.richText = false;
        text.raycastTarget = false;
        return text;
    }

    private void CloseMenu()
    {
        if (_initialized)
        {
            _menu.SetActive(false);
        }
    }

    private void RefreshGrid()
    {
        _traderGridView.method_16();
        _traderGridView.method_18();
    }

    private void RefreshStackCounts()
    {
        foreach (var itemView in _traderGridView.GetComponentsInChildren<TradingItemView>(includeInactive: true))
        {
            itemView.UpdateInfo();
        }
    }

    private static void Stretch(Transform transform, Vector2? offsetMin = null, Vector2? offsetMax = null)
    {
        var rect = (RectTransform)transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin ?? Vector2.zero;
        rect.offsetMax = offsetMax ?? Vector2.zero;
    }

    private static string ShortId(string id)
    {
        return string.IsNullOrEmpty(id) ? "unknown" : id[..Math.Min(8, id.Length)];
    }
}
