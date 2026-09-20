using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class BossBar : MonoBehaviour
{
    [SerializeField] private DepletingBar healthBar;
    private float maxHP; 
    private float currentHP; 
    private RectTransform healthBarRT;
    [SerializeField] private RectTransform healthFillRT; //!!getcomponentsofchildren() returns parent too for some reason, so i just split these all up and made them serialize fields for my convenience
    [SerializeField] private RectTransform healthDepleteRT; //if we had a bunch of children, it'd be cleaner to figure out a way for get components of children to work, refer to farm project farmtile for 'foreach'
    [SerializeField] private BossDamageable bD;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        healthBarRT = gameObject.GetComponent<RectTransform>(); //get the parent game objects rectTransform 

        Debug.Assert(healthBarRT, "a rect transform for health bar is missing! bossbar");
        Debug.Assert(healthFillRT, "a rect transform for health bar fill is missing! bossbar");
        Debug.Assert(healthDepleteRT, "a rect transform for health bar deplete is missing! bossbar");
        Debug.Assert(bD, "bossdamageable is missing in bossbar!");
        
        maxHP = bD.maxHealth;
        currentHP = bD.currentHealth;
    }

    private void OnEnable()
    {
        bD.OnHealthChanged += HandleHealthChanged; // If player takes damage call this
    }

    private void OnDisable()
    {
        bD.OnHealthChanged -= HandleHealthChanged; 
    }
    // This handles the health update from the damaged event on playerdamageable
    private void HandleHealthChanged(float current, float max)
    {
        maxHP = max;
        ChangeCurrentHP(current);
    }

    
    private void ChangeCurrentHP(float newHP)
    {
        //Debug.Log("changing current hp");
            StartCoroutine(HealthBarRolldown(healthBar, currentHP, newHP));
            StopCoroutine(HealthBarRolldown(healthBar, currentHP, newHP));
            currentHP = newHP;
    }
    private IEnumerator HealthBarRolldown(DepletingBar hp, float originalValue, float newValue)
    {
        //Debug.Log("rolling first bar");
        float oV2 = originalValue;
        float nV2 = newValue; //make clones of these for the second bar

        while (originalValue != newValue) //first bar depletion
        {
            originalValue = Mathf.MoveTowards(originalValue, newValue, Time.fixedDeltaTime * 40f);
            hp.SetFillA(originalValue / maxHP); //needs to be fixed later !!TODO: make a formula that clamps our health to an equivalent value between 0 and 1 (fillamount only work with values between 0 and 1)!!
            yield return null;
        }

        //Debug.Log("delay start");
        yield return new WaitForSeconds(1.3f);
        //Debug.Log("delay end");

        //Debug.Log("rolling second bar");
        StartCoroutine(HealthBarRolldownPart2(healthBar, oV2, nV2));
        StopCoroutine(HealthBarRolldownPart2(healthBar, oV2, nV2));
    }

    private IEnumerator HealthBarRolldownPart2(DepletingBar hp, float originalValue, float newValue)
    {
        while (originalValue != newValue) //second bar depletion
        {
            originalValue = Mathf.MoveTowards(originalValue, newValue, Time.fixedDeltaTime * 7f);
            hp.SetFillB(originalValue / maxHP); //needs to be fixed later !!TODO: make a formula that clamps our health to an equivalent value between 0 and 1 (fillamount only work with values between 0 and 1)!!
            yield return null;
        }  
    }
}

