using UnityEngine;
using TMPro;
using Unity.Properties;

public class PlayerStatsUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerAttributes attributes;
    [SerializeField] private PlayerLevel playerLevel;
    [SerializeField] private PlayerInventory inventory;

    [Header("Stats")]
    [SerializeField] private TextMeshProUGUI conText;
    [SerializeField] private TextMeshProUGUI strText;
    [SerializeField] private TextMeshProUGUI endText;

    [Header("Currency")]
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private TextMeshProUGUI fragmentsText;


    void OnEnable()
    {
        attributes.OnAttributesChanged += UpdateUI;
        UpdateUI();
    }


    void OnDisable()
    {
        attributes.OnAttributesChanged -= UpdateUI;
    }


    void UpdateUI()
    {
        UpdateStatText(conText, attributes.constitution, attributes.tempCON);
        UpdateStatText(strText, attributes.strength, attributes.tempSTR);
        UpdateStatText(endText, attributes.endurance, attributes.tempEND);

        costText.text = $"Cost: {playerLevel.GetTotalPendingCost()}";
        fragmentsText.text = $"Fragments: {inventory.fragments}";
    }


    void UpdateStatText(TextMeshProUGUI text, int baseValue, int temp)
    {
        if (temp > 0)
        {
            text.text = $"{baseValue} -> {baseValue + temp}";
        }
        else
        {
            text.text = baseValue.ToString();
        }
    }
}
