using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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
    public GameObject vibrantUpgradeButton;

    // Tag Wall button movement
    public RectTransform tagWallButtonRect;

    // ORIGINAL POSITION (NEW)
    private Vector2 tagWallOriginalPos;

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

    // Win UI Reference
    public GameObject winPanel;
    public TMP_Text winText;

    private bool winTriggered = false;

    // Tag Wall Button
    public GameObject tagWallButton;

    // Camera background
    private Camera mainCamera;

    void Start()
    {
        resources.Add(ResourceType.CreativeEnergy, 0f);
        resources.Add(ResourceType.Paint, 10f);
        resources.Add(ResourceType.Reputation, 0f);

        LoadUpgradesFromJSON();

        generators.Add(new GraffitiSprayer(2f, 1.2f));
        generators.Add(new PaintMixer(1f, 1.5f));

        LoadGame();

        sessionStartTime = Time.time;

        OnUpgradePurchased += HandleUpgradePurchased;

        if (tagWallButtonRect != null)
        {
            tagWallOriginalPos = tagWallButtonRect.anchoredPosition;
        }
        // Ensure win UI starts hidden
        if (winText != null)
        {
            winText.gameObject.SetActive(false);
        }

        if (winPanel != null)
        {
            winPanel.SetActive(false);
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

    void Update()
    {
        float dt = Time.deltaTime;

        autoSaveTimer += dt;

        if (autoSaveTimer >= autoSaveInterval)
        {
            SaveGame();
            autoSaveTimer = 0f;
            Debug.Log("Auto-saved game");
        }

        if (!gameEnded && !winTriggered && resources[ResourceType.Reputation] >= 100f)
        {
            winTriggered = true;

            if (mainCamera != null)
            {
                mainCamera.clearFlags = CameraClearFlags.SolidColor;
                mainCamera.backgroundColor = Color.green;
                RenderSettings.skybox = null;
            }

            StartCoroutine(WinSequence());
        }

        // STOP GAME LOOP DURING WIN SCREEN
        if (winTriggered)
            return;

        if (Keyboard.current != null)
        {
            bool shiftHeld = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
            bool rPressed = Keyboard.current.rKey.wasPressedThisFrame;

            if (shiftHeld && rPressed)
            {
                ResetGame();

                winTriggered = false;
                gameEnded = false;

                if (mainCamera != null)
                    mainCamera.backgroundColor = Color.white;

                Debug.Log("SHIFT + R detected");

                if (sprayUpgradeButton != null)
                    sprayUpgradeButton.SetActive(false);

                if (vibrantUpgradeButton != null)
                    vibrantUpgradeButton.SetActive(false);
            }
            else if (rPressed)
            {
                ResetTagWallPosition();
                Debug.Log("R pressed: Tag Wall reset");
            }

            bool plusPressed =
                Keyboard.current.equalsKey.wasPressedThisFrame ||
                Keyboard.current.numpadPlusKey.wasPressedThisFrame;

            if (shiftHeld && plusPressed)
            {
                resources[ResourceType.Reputation] = 99f;
                Debug.Log("SHIFT + +: Reputation set to 99");
            }

            CheckUpgrades();
        }

        AddResource(ResourceType.CreativeEnergy, murals * muralEnergyRate * dt);
        AddResource(ResourceType.Paint, artAssistants * assistantPaintRate * dt);

        foreach (Generator gen in generators)
        {
            if (gen is GraffitiSprayer)
            {
                float current = resources[ResourceType.CreativeEnergy];
                gen.Produce(ref current, dt);
                resources[ResourceType.CreativeEnergy] = current;
            }
            else if (gen is PaintMixer)
            {
                float current = resources[ResourceType.Paint];
                gen.Produce(ref current, dt);
                resources[ResourceType.Paint] = current;
            }
        }

        DisplayResources();
        UpdateUpgradeUI();
    }

    // ================= WIN SEQUENCE =================
    private IEnumerator WinSequence()
    {
        yield return new WaitForSeconds(1f);

        if (creativeEnergyText) creativeEnergyText.text = "";
        if (paintText) paintText.text = "";
        if (reputationText) reputationText.text = "";
        if (upgradeText) upgradeText.text = "";

        if (sprayUpgradeButton) sprayUpgradeButton.SetActive(false);
        if (vibrantUpgradeButton) vibrantUpgradeButton.SetActive(false);

        if (tagWallButton != null)
            tagWallButton.SetActive(false);

        if (endGamePanel) endGamePanel.SetActive(false);

        if (winPanel != null)
            winPanel.SetActive(true);

        if (winText != null)
        {
            winText.gameObject.SetActive(true);
            winText.enabled = true;
            winText.text = "You're popular now! Congratulations!";
        }
        else
        {
            Debug.LogWarning("winText is NOT assigned in Inspector");
        }
    }

    public void ResetTagWallPosition()
    {
        if (tagWallButtonRect != null)
        {
            tagWallButtonRect.anchoredPosition = tagWallOriginalPos;
        }
    }

    public void TagWall()
    {
        PointerEventData eventData = new PointerEventData(EventSystem.current);

        AddResource(ResourceType.CreativeEnergy, 1f);
        AddResource(ResourceType.Reputation, 0.1f);

        if (tagWallButtonRect != null)
        {
            MoveButtonRandomly(tagWallButtonRect);
        }
    }

    void MoveButtonRandomly(RectTransform button)
    {
        RectTransform parent = button.parent as RectTransform;
        if (parent == null) return;

        float width = parent.rect.width;
        float height = parent.rect.height;

        float safeMarginX = width * 0.35f;
        float safeMarginY = height * 0.35f;

        float minX = -width / 2f + 50f;
        float maxX = width / 2f - 50f;
        float minY = -height / 2f + 50f;
        float maxY = height / 2f - 50f;

        Vector2 newPos;
        int safetyAttempts = 0;

        do
        {
            float randomX = Random.Range(minX, maxX);
            float randomY = Random.Range(minY, maxY);

            newPos = new Vector2(randomX, randomY);
            safetyAttempts++;

            bool inBlockedZone =
                newPos.x < -safeMarginX &&
                newPos.y > height / 2f - safeMarginY;

            if (!inBlockedZone)
                break;

        } while (safetyAttempts < 20);

        button.anchoredPosition = newPos;
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

                if (upgrade.name == "Vibrant Paint")
                    vibrantUpgradeButton.SetActive(true);
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
            text += upgrade.name + " - " + upgrade.state + "\n";

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

    public void BuyVibrantPaint()
    {
        foreach (Upgrade upgrade in upgrades)
        {
            if (upgrade.name == "Vibrant Paint")
            {
                string feedback;
                TryPurchaseUpgrade(upgrade, out feedback);
                Debug.Log(feedback);
            }
        }
    }

    void HandleUpgradePurchased(Upgrade upgrade)
    {
        if (upgrade.name == "Better Spray Cans")
            sprayUpgradeButton.SetActive(false);

        if (upgrade.name == "Vibrant Paint")
            vibrantUpgradeButton.SetActive(false);
    }

    void TriggerEndGame()
    {
        gameEnded = true;

        if (endGamePanel != null)
            endGamePanel.SetActive(true);

        if (endGameText != null)
            endGameText.text = "You're popular now!";
    }

    public void ResetGame()
    {
        resources[ResourceType.CreativeEnergy] = 0f;
        resources[ResourceType.Paint] = 10f;
        resources[ResourceType.Reputation] = 0f;

        murals = 1;
        artAssistants = 1;

        muralEnergyRate = 2f;
        assistantPaintRate = 1f;

        gameEnded = false;
        winTriggered = false;

        if (endGamePanel != null)
            endGamePanel.SetActive(false);

        if (winPanel != null)
            winPanel.SetActive(false);

        foreach (Upgrade u in upgrades)
            u.state = UpgradeState.Locked;

        if (sprayUpgradeButton != null)
            sprayUpgradeButton.SetActive(false);

        if (vibrantUpgradeButton != null)
            vibrantUpgradeButton.SetActive(false);

        if (tagWallButton != null)
            tagWallButton.SetActive(true);

        Debug.Log("FULL RESET COMPLETE");
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

    public abstract void Produce(ref float resourceAmount, float dt);
}

public class GraffitiSprayer : Generator
{
    public float efficiency;

    public GraffitiSprayer(float baseProduction, float efficiency = 1f)
        : base("Graffiti Sprayer", baseProduction)
    {
        this.efficiency = efficiency;
    }

    public override void Produce(ref float resourceAmount, float dt)
    {
        resourceAmount += baseProduction * efficiency * dt;
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

    public override void Produce(ref float resourceAmount, float dt)
    {
        resourceAmount += baseProduction * quality * dt;
    }
}