using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ResourceManager : MonoBehaviour
{
    // Text on screen
    public TMP_Text upgradeText;

    // Resource UI texts
    public TMP_Text creativeEnergyText;
    public TMP_Text paintText;
    public TMP_Text reputationText;

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

    // Generators
    public List<Generator> generators = new List<Generator>();

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

        // Add generators
        generators.Add(new GraffitiSprayer(2f, 1.2f));
        generators.Add(new PaintMixer(1f, 1.5f));

        StartCoroutine(GeneratorTick());
    }

    IEnumerator ResourceTick()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);

            // Legacy passive income
            AddResource(ResourceType.CreativeEnergy, murals * muralEnergyRate);
            AddResource(ResourceType.Paint, artAssistants * assistantPaintRate);

            CheckUpgrades();
            DisplayResources();
            UpdateUpgradeUI();
        }
    }

    IEnumerator GeneratorTick()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);

            foreach (Generator gen in generators)
            {
                if (gen is GraffitiSprayer)
                {
                    float current = resources[ResourceType.CreativeEnergy];
                    gen.Produce(ref current);
                    resources[ResourceType.CreativeEnergy] = current;
                }
                else if (gen is PaintMixer)
                {
                    float current = resources[ResourceType.Paint];
                    gen.Produce(ref current);
                    resources[ResourceType.Paint] = current;
                }
            }
        }
    }

    // Manual click action
    public void TagWall()
    {
        AddResource(ResourceType.CreativeEnergy, 1f);
        Debug.Log("Tagged wall! +1 Creative Energy");
    }

    // Helper method
    void AddResource(ResourceType type, float amount)
    {
        float current = resources[type];
        current += amount;
        resources[type] = current;
    }

    // Display resources in UI and console
    void DisplayResources()
    {
        // Creative Energy shows 1 decimal place (tenths)
        creativeEnergyText.text = "Creative Energy: " + resources[ResourceType.CreativeEnergy].ToString("F1");

        // Paint and Reputation remain integers
        paintText.text = "Paint: " + Mathf.FloorToInt(resources[ResourceType.Paint]);
        reputationText.text = "Reputation: " + Mathf.FloorToInt(resources[ResourceType.Reputation]);

        // Optional console logging
        foreach (KeyValuePair<ResourceType, float> resource in resources)
        {
            Debug.Log(resource.Key + ": " + resource.Value);
        }
        Debug.Log("----------------------");
    }

    void CheckUpgrades()
    {
        foreach (Upgrade upgrade in upgrades)
        {
            if (upgrade.state == UpgradeState.Locked &&
                resources[upgrade.effect.targetResource] >= upgrade.cost)
            {
                upgrade.state = UpgradeState.Available;
                Debug.Log(upgrade.name + " AVAILABLE");

                if (upgrade.name == "Better Spray Cans")
                    sprayUpgradeButton.SetActive(true);
            }
        }
    }

    public bool TryPurchaseUpgrade(Upgrade upgrade, out string message)
    {
        if (upgrade.state != UpgradeState.Available)
        {
            message = "Upgrade not available";
            return false;
        }

        if (resources[upgrade.effect.targetResource] < upgrade.cost)
        {
            message = "Not enough resources";
            return false;
        }

        resources[upgrade.effect.targetResource] -= upgrade.cost;
        ApplyUpgrade(upgrade.effect);
        upgrade.state = UpgradeState.Purchased;

        message = "Purchased " + upgrade.name;
        return true;
    }

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

    void UpdateUpgradeUI()
    {
        string text = "Upgrades:\n";

        foreach (Upgrade upgrade in upgrades)
        {
            text += upgrade.name + " - " + upgrade.state + "\n";
        }

        upgradeText.text = text;
    }

    public void BuySprayUpgrade()
    {
        foreach (Upgrade upgrade in upgrades)
        {
            if (upgrade.name == "Better Spray Cans")
            {
                string feedback;
                if (TryPurchaseUpgrade(upgrade, out feedback))
                {
                    sprayUpgradeButton.SetActive(false);
                    Debug.Log(feedback);
                }
                else
                {
                    Debug.Log(feedback);
                }
            }
        }
    }
}

// -----------------------------
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

public enum UpgradeState
{
    Locked,
    Available,
    Purchased
}

public enum ResourceType
{
    CreativeEnergy,
    Paint,
    Reputation
}

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

// -----------------------------
// Abstract Generator class
public abstract class Generator
{
    public string generatorName;
    public float baseProduction;

    public Generator(string name, float baseProduction)
    {
        generatorName = name;
        this.baseProduction = baseProduction;
    }

    public abstract void Produce(ref float resourceAmount);
}

public class GraffitiSprayer : Generator
{
    public float efficiency;

    public GraffitiSprayer(float baseProduction, float efficiency = 1f)
        : base("Graffiti Sprayer", baseProduction)
    {
        this.efficiency = efficiency;
    }

    public override void Produce(ref float resourceAmount)
    {
        resourceAmount += baseProduction * efficiency;
    }
}

public class PaintMixer : Generator
{
    public float quality;

    public PaintMixer(float baseProduction, float quality = 1f)
        : base("Paint Mixer", baseProduction)
    {
        this.quality = quality;
    }

    public override void Produce(ref float resourceAmount)
    {
        resourceAmount += baseProduction * quality;
    }
}