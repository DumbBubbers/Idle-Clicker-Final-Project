using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;

[System.Serializable] // Move data collection to file
public class UpgradeData
{
    public string name;
    public float cost;
    public float multiplier;
    public ResourceType targetResource;
    public int tier;
}

[System.Serializable]
public class UpgradeDataList
{
    public List<UpgradeData> upgrades;
}

[System.Serializable] // Save data
public class SaveData
{
    public float creativeEnergy;
    public float paint;
    public float reputation;
}

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

    // Step E — Play session tracking
    private float sessionStartTime;

    // Auto-save timer
    private float autoSaveTimer;
    private float autoSaveInterval = 30f;

    // EVENT SYSTEM
    public delegate void UpgradePurchasedHandler(Upgrade upgrade);
    public event UpgradePurchasedHandler OnUpgradePurchased;

    void Start()
    {
        // Initialize resources
        resources.Add(ResourceType.CreativeEnergy, 0f);
        resources.Add(ResourceType.Paint, 10f);
        resources.Add(ResourceType.Reputation, 0f);

        // Load upgrades from JSON file (Step C)
        LoadUpgradesFromJSON();

        // Start passive income loop
        StartCoroutine(ResourceTick());

        // Add generators
        generators.Add(new GraffitiSprayer(2f, 1.2f));
        generators.Add(new PaintMixer(1f, 1.5f));

        StartCoroutine(GeneratorTick());

        // Step D — Load saved game
        LoadGame();

        // Step E — Track session start time
        sessionStartTime = Time.time;

        // EVENT LISTENER
        OnUpgradePurchased += HandleUpgradePurchased;
    }

    void Update()
    {
        autoSaveTimer += Time.deltaTime;

        if (autoSaveTimer >= autoSaveInterval)
        {
            SaveGame();
            autoSaveTimer = 0f;

            Debug.Log("Auto-saved game");
        }
    }

    IEnumerator ResourceTick()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);

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

    void AddResource(ResourceType type, float amount)
    {
        float current = resources[type];
        current += amount;
        resources[type] = current;
    }

    void DisplayResources()
    {
        creativeEnergyText.text = "Creative Energy: " + resources[ResourceType.CreativeEnergy].ToString("F1");
        paintText.text = "Paint: " + Mathf.FloorToInt(resources[ResourceType.Paint]);
        reputationText.text = "Reputation: " + Mathf.FloorToInt(resources[ResourceType.Reputation]);

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
        try
        {
            if (upgrade == null)
            {
                throw new System.Exception("Upgrade reference is null");
            }

            if (upgrade.state != UpgradeState.Available)
            {
                message = "Upgrade not available";
                return false;
            }

            if (!resources.ContainsKey(upgrade.effect.targetResource))
            {
                throw new System.Exception("Resource type missing from dictionary");
            }

            if (resources[upgrade.effect.targetResource] < upgrade.cost)
            {
                message = "Not enough resources";
                return false;
            }

            resources[upgrade.effect.targetResource] -= upgrade.cost;

            ApplyUpgrade(upgrade.effect);

            upgrade.state = UpgradeState.Purchased;

            // EVENT TRIGGER
            OnUpgradePurchased?.Invoke(upgrade);

            message = "Purchased " + upgrade.name;
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError("Upgrade Purchase Error: " + e.Message);
            message = "Error purchasing upgrade";
            return false;
        }
    }

    void ApplyUpgrade(UpgradeEffect effect)
    {
        if (effect.targetResource == ResourceType.CreativeEnergy)
        {
            muralEnergyRate *= effect.multiplier;
        }

        if (effect.targetResource == ResourceType.Paint)
        {
            assistantPaintRate *= effect.multiplier;
        }
    }

    void SaveGame() // Save method
    {
        SaveData data = new SaveData();

        data.creativeEnergy = resources[ResourceType.CreativeEnergy];
        data.paint = resources[ResourceType.Paint];
        data.reputation = resources[ResourceType.Reputation];

        string path = Application.persistentDataPath + "/save.xml";

        System.Xml.Serialization.XmlSerializer serializer =
            new System.Xml.Serialization.XmlSerializer(typeof(SaveData));

        System.IO.FileStream stream = new System.IO.FileStream(path, System.IO.FileMode.Create);
        serializer.Serialize(stream, data);
        stream.Close();
    }

    void LoadGame() // Load method
    {
        string path = Application.persistentDataPath + "/save.xml";

        if (System.IO.File.Exists(path))
        {
            System.Xml.Serialization.XmlSerializer serializer =
                new System.Xml.Serialization.XmlSerializer(typeof(SaveData));

            System.IO.FileStream stream =
                new System.IO.FileStream(path, System.IO.FileMode.Open);

            SaveData data = serializer.Deserialize(stream) as SaveData;
            stream.Close();

            resources[ResourceType.CreativeEnergy] = data.creativeEnergy;
            resources[ResourceType.Paint] = data.paint;
            resources[ResourceType.Reputation] = data.reputation;
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
                    Debug.Log(feedback);
                }
                else
                {
                    Debug.Log(feedback);
                }
            }
        }
    }

    // Step C — Load upgrades from JSON file
    void LoadUpgradesFromJSON()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "upgrades.json");

        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            UpgradeDataList data = JsonUtility.FromJson<UpgradeDataList>(json);

            upgrades.Clear();

            foreach (UpgradeData u in data.upgrades)
            {
                upgrades.Add(new Upgrade(
                    u.name,
                    u.cost,
                    new UpgradeEffect(u.multiplier, u.targetResource),
                    u.tier
                ));
            }

            Debug.Log("Upgrades loaded from JSON");
        }
    }

    void HandleUpgradePurchased(Upgrade upgrade)
    {
        Debug.Log("EVENT: Purchased " + upgrade.name);

        if (upgrade.name == "Better Spray Cans")
        {
            sprayUpgradeButton.SetActive(false);
        }
    }

    void OnApplicationQuit()
    {
        SaveGame();

        float sessionLength = Time.time - sessionStartTime;

        string path = Application.persistentDataPath + "/playtime.txt";

        string logEntry = "Session Time: " + sessionLength + " seconds\n";

        File.AppendAllText(path, logEntry);
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