using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ResourceManager : MonoBehaviour
{
    // Text on screen
    public TMP_Text upgradeText;

    // Resource storage
    public Dictionary<ResourceType, float> resources = new Dictionary<ResourceType, float>();

    // Passive income sources
    public int murals = 1;
    public int artAssistants = 1;

    public float muralEnergyRate = 2f;
    public float assistantPaintRate = 1f;

    // Upgrade storage
    public List<Upgrade> upgrades = new List<Upgrade>();

    // UI Buttons for upgrades
    public GameObject sprayUpgradeButton;

    void Start()
    {
        // Initialize resources
        resources.Add(ResourceType.CreativeEnergy, 0f);
        resources.Add(ResourceType.Paint, 10f);
        resources.Add(ResourceType.Reputation, 0f);

        // Create upgrades
        upgrades.Add(new Upgrade(
            "Better Spray Cans",
            50,
            new UpgradeEffect(1.5f, ResourceType.CreativeEnergy),
            1
        ));

        upgrades.Add(new Upgrade(
            "Vibrant Paint",
            75,
            new UpgradeEffect(2f, ResourceType.Paint),
            1
        ));

        // Start passive income loop
        StartCoroutine(ResourceTick());
    }

    IEnumerator ResourceTick()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);

            resources[ResourceType.CreativeEnergy] += murals * muralEnergyRate;
            resources[ResourceType.Paint] += artAssistants * assistantPaintRate;

            CheckUpgrades();
            DisplayResources();
            UpdateUpgradeUI();
        }
    }

    // Manual click action
    public void TagWall()
    {
        resources[ResourceType.CreativeEnergy] += 1f;
        Debug.Log("Tagged wall! +1 Creative Energy");
    }

    // Show resources in console
    void DisplayResources()
    {
        Debug.Log("Creative Energy: " + resources[ResourceType.CreativeEnergy]);
        Debug.Log("Paint: " + resources[ResourceType.Paint]);
        Debug.Log("Reputation: " + resources[ResourceType.Reputation]);
        Debug.Log("----------------------");

        foreach (KeyValuePair<ResourceType, float> resource in resources)
        {
            Debug.Log(resource.Key + ": " + resource.Value);
        }

        Debug.Log("----------------------");
    }


    // Check if upgrades become available
    void CheckUpgrades()
    {
        foreach (Upgrade upgrade in upgrades)
        {
            if (upgrade.state == UpgradeState.Locked &&
                resources[upgrade.effect.targetResource] >= upgrade.cost)
            {
                upgrade.state = UpgradeState.Available;

                Debug.Log(upgrade.name + "AVAILABLE");

                if (upgrade.name == "Better Spray Cans")
                {
                    sprayUpgradeButton.SetActive(true);
                }
            }
        }
    }

    // Purchase upgrade
    public void PurchaseUpgrade(Upgrade upgrade)
    {
        if (upgrade.state != UpgradeState.Available)
            return;

        if (resources[upgrade.effect.targetResource] >= upgrade.cost)
        {
            resources[upgrade.effect.targetResource] -= upgrade.cost;

            ApplyUpgrade(upgrade.effect);

            upgrade.state = UpgradeState.Purchased;

            Debug.Log("Purchased upgrade: " + upgrade.name);
            Debug.Log("Effect Multiplier: " + upgrade.effect.multiplier);
            Debug.Log("Effect Target Resource: " + upgrade.effect.targetResource);
        }
    }

    // Apply upgrade multiplier
    void ApplyUpgrade(UpgradeEffect effect)
    {
        if (effect.targetResource == ResourceType.CreativeEnergy)
        {
            muralEnergyRate *= effect.multiplier;

            Debug.Log("Creative Energy rate increased to: " + muralEnergyRate);
        }

        if (effect.targetResource == ResourceType.Paint)
        {
            assistantPaintRate *= effect.multiplier;

            Debug.Log("Paint generation rate increased to: " + assistantPaintRate);
        }
    }
    
    // Apply Upgrade text
    void UpdateUpgradeUI()
    {
        string text = "Upgrades:\n";

        foreach (Upgrade upgrade in upgrades)
        {
            text += upgrade.name + " - " + upgrade.state + "\n";
        }

        upgradeText.text = text;
    }
    
    // Button function
    public void BuySprayUpgrade()
    {
        foreach (Upgrade upgrade in upgrades)
        {
            if (upgrade.name == "Better Spray Cans")
            {
                PurchaseUpgrade(upgrade);
                sprayUpgradeButton.SetActive(false);
            }
        }
    }
 }

// Upgrade class
public class Upgrade
{
    public string name;
    public float cost;
    public UpgradeEffect effect;
    public int tier;
    public UpgradeState state;

    public Upgrade(string name, float cost, UpgradeEffect effect, int tier)
    {
        this.name = name;
        this.cost = cost;
        this.effect = effect;
        this.tier = tier;
        this.state = UpgradeState.Locked;
    }
}

// Upgrade states
public enum UpgradeState
{
    Locked,
    Available,
    Purchased
}

// Resource types (replaces magic strings)
public enum ResourceType
{
    CreativeEnergy,
    Paint,
    Reputation
}

// Upgrade effect structure
public struct UpgradeEffect
{
    public float multiplier;
    public ResourceType targetResource;

    public UpgradeEffect(float multiplier, ResourceType targetResource)
    {
        this.multiplier = multiplier;
        this.targetResource = targetResource;
    }

}

