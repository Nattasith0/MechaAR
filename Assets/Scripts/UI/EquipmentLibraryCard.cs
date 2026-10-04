using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A reusable catalog view bound to the existing equipment asset.</summary>
public sealed class EquipmentLibraryCard : MonoBehaviour
{
    [SerializeField] Button openButton;
    [SerializeField] TMP_Text nameText, thaiNameText, categoryText, indexText;
    Action<EquipmentData> onOpen;
    public EquipmentData Equipment { get; private set; }

    public void Bind(EquipmentData data, int index, Action<EquipmentData> callback)
    {
        Equipment = data;
        onOpen = callback;
        if (nameText != null) nameText.text = data != null ? data.equipmentName : "";
        if (thaiNameText != null) thaiNameText.text = data != null ? data.thaiName : "";
        if (categoryText != null) categoryText.text = data != null ? data.category : "";
        if (indexText != null) indexText.text = (index + 1).ToString("00");
        if (openButton != null)
        {
            openButton.onClick.RemoveListener(Open);
            openButton.onClick.AddListener(Open);
            openButton.interactable = data != null;
        }
    }

    void Open() { if (Equipment != null) onOpen?.Invoke(Equipment); }
    void OnDestroy() { if (openButton != null) openButton.onClick.RemoveListener(Open); }
}
