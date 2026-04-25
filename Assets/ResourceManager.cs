using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
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

    private float sessionStartTime;

    private float autoSaveTimer;
    private float autoSaveInterval = 30f;

    public delegate void UpgradePurchasedHandler(Upgrade upgrade);
    public event UpgradePurchasedHandler OnUpgradePurchased;

    public GameObject endGamePanel;
    public TMP_Text endGameText;
    private bool gameEnded = false;

    void Start()
    {
        resources.Add(ResourceType.CreativeEnergy, 0f);
        resources.Add(ResourceType.Paint, 10f);
        resources.Add(ResourceType.Reputation, 0f);

        LoadUpgradesFromJSON();

        StartCoroutine(ResourceTick());

        generators.Add(new GraffitiSprayer(2f, 1.2f));
        generators.Add(new PaintMixer(1f, 1.5f));

        StartCoroutine(GeneratorTick());

        LoadGame();

        sessionStartTime = Time.time;

        OnUpgradePurchased += HandleUpgradePurchased;
    }
    void InitializeGame()
    {
        // Make sure dictionary exists safely
        if (resources.Count == 0)
        {
            resources.Add(ResourceType.CreativeEnergy, 0f);
            resources.Add(ResourceType.Paint, 10f);
            resources.Add(ResourceType.Reputation, 0f);
        }

        // Reset upgrade states safely
        foreach (Upgrade u in upgrades)
        {
            u.state = UpgradeState.Locked;
        }

        // Reset generators list safety (optional but recommended)
        if (generators.Count == 0)
        {
            generators.Add(new GraffitiSprayer(2f, 1.2f));
            generators.Add(new PaintMixer(1f, 1.5f));
        }

        // Restart loops safely
        StartCoroutine(ResourceTick());
        StartCoroutine(GeneratorTick());

        sessionStartTime = Time.time;
        gameEnded = false;
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

        if (!gameEnded && resources[ResourceType.Reputation] >= 100f)
        {
            TriggerEndGame();
        }

        // SHIFT + R RESET (FULL RESTART)
        if (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.R))
        {
            ResetGame();
            Debug.Log("Shift+R: Game fully reset");
        }
        if (Keyboard.current != null)
        {
            bool shiftHeld = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
            bool rPressed = Keyboard.current.rKey.wasPressedThisFrame;

            if (shiftHeld && rPressed)
            {
                Debug.Log("SHIFT + R detected (New Input System)");
                ResetGame();
            }
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

    public void TagWall()
    {
        AddResource(ResourceType.CreativeEnergy, 1f);
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
    }

    void CheckUpgrades()
    {
        foreach (Upgrade upgrade in upgrades)
        {
            if (upgrade.state == UpgradeState.Locked &&
                resources[upgrade.effect.targetResource] >= upgrade.cost)
            {
                upgrade.state = UpgradeState.Available;

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
                throw new System.Exception("Upgrade is null");

            if (upgrade.state != UpgradeState.Available)
            {
                message = "Not available";
                return false;
            }

            if (!resources.ContainsKey(upgrade.effect.targetResource))
                throw new System.Exception("Missing resource type");

            if (resources[upgrade.effect.targetResource] < upgrade.cost)
            {
                message = "Not enough resources";
                return false;
            }

            resources[upgrade.effect.targetResource] -= upgrade.cost;

            ApplyUpgrade(upgrade.effect);

            upgrade.state = UpgradeState.Purchased;

            OnUpgradePurchased?.Invoke(upgrade);

            message = "Purchased " + upgrade.name;
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError(e.Message);
            message = "Error purchasing upgrade";
            return false;
        }
    }

    void ApplyUpgrade(UpgradeEffect effect)
    {
        if (effect.targetResource == ResourceType.CreativeEnergy)
            muralEnergyRate *= effect.multiplier;

        if (effect.targetResource == ResourceType.Paint)
            assistantPaintRate *= effect.multiplier;
    }

    void SaveGame()
    {
        SaveData data = new SaveData();

        data.creativeEnergy = resources[ResourceType.CreativeEnergy];
        data.paint = resources[ResourceType.Paint];
        data.reputation = resources[ResourceType.Reputation];

        string path = Application.persistentDataPath + "/save.xml";

        System.Xml.Serialization.XmlSerializer serializer =
            new System.Xml.Serialization.XmlSerializer(typeof(SaveData));

        FileStream stream = new FileStream(path, FileMode.Create);
        serializer.Serialize(stream, data);
        stream.Close();
    }

    void LoadGame()
    {
        string path = Application.persistentDataPath + "/save.xml";

        if (File.Exists(path))
        {
            System.Xml.Serialization.XmlSerializer serializer =
                new System.Xml.Serialization.XmlSerializer(typeof(SaveData));

            FileStream stream = new FileStream(path, FileMode.Open);
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
                TryPurchaseUpgrade(upgrade, out feedback);
                Debug.Log(feedback);
            }
        }
    }

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
        }
    }

    void HandleUpgradePurchased(Upgrade upgrade)
    {
        if (upgrade.name == "Better Spray Cans")
            sprayUpgradeButton.SetActive(false);
    }

    void TriggerEndGame()
    {
        gameEnded = true;

        StopAllCoroutines();

        if (endGamePanel != null)
            endGamePanel.SetActive(true);

        if (endGameText != null)
            endGameText.text = "You turned the city into a living artwork.";
    }

    public void ResetGame()
    {
        StopAllCoroutines();

        // reset resources
        resources[ResourceType.CreativeEnergy] = 0f;
        resources[ResourceType.Paint] = 10f;
        resources[ResourceType.Reputation] = 0f;

        // reset production values
        murals = 1;
        artAssistants = 1;

        muralEnergyRate = 2f;
        assistantPaintRate = 1f;

        gameEnded = false;

        if (endGamePanel != null)
            endGamePanel.SetActive(false);

        // reset upgrades
        foreach (Upgrade u in upgrades)
            u.state = UpgradeState.Locked;

        if (sprayUpgradeButton != null)
            sprayUpgradeButton.SetActive(false);

        // Restart systems so values update again
        StartCoroutine(ResourceTick());
        StartCoroutine(GeneratorTick());

        Debug.Log("FULL RESET COMPLETE + SYSTEMS RESTARTED");
    }

    public void DevJumpToEnd()
    {
        resources[ResourceType.CreativeEnergy] = 500f;
        resources[ResourceType.Paint] = 500f;
        resources[ResourceType.Reputation] = 100f;
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