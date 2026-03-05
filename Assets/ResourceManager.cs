using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class ResourceManager : MonoBehaviour
{
    // Dictionary to store resources

    public Dictionary<string, float> resources = new Dictionary<string, float>();

    // Passive income sources

    public int murals = 1;
    public int artAssistants = 1;
    public float muralEnergyRate = 2f;
    public float assistantPaintRate = 1f;

    // Upgrade list

    public List<Upgrade> upgrades = new List<Upgrade>();
    void Start()
    {
        // Initialize resources at the beginning

        resources.Add("CreativeEnergy", 0f);
        resources.Add("Paint", 10f);
        resources.Add("Reputation", 0f);

        // Some example upgrades

        upgrades.Add(new Upgrade("Better Spray Cans", 50f, "CreativeEnergy"));
        upgrades.Add(new Upgrade("Vibrant Colors", 75f, "Paint"));

        // Start passive income loop that updates every second

        StartCoroutine(ResourceTick());
    }
    IEnumerator ResourceTick()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);

            // Passive income

            resources["CreativeEnergy"] += murals * muralEnergyRate;
            resources["Paint"] += artAssistants * assistantPaintRate;

            DisplayResources();
            CheckUpgrades();
        }
    }
    // LMB click on button that says "Tag Wall"
    public void TagWall()
    {
        resources["CreativeEnergy"] += 1f;
        Debug.Log("Tagged a wall! +1 Creative Energy");
    }
    // Display resources in console
    void DisplayResources()
    {
        Debug.Log("Creative Energy: " + resources["CreativeEnergy"]);
        Debug.Log("Paint: " + resources["Paint"]);
        Debug.Log("Reputation: " + resources["Reputation"]);
        Debug.Log("----------------------");
    }
    void CheckUpgrades()
    {
        foreach (Upgrade upgrade in upgrades)
        {
            if (resources[upgrade.resourceType] >= upgrade.cost)
            {
                Debug.Log("Upgrade Available: " + upgrade.name);
            }
        }
    }
}
// Upgrade class

[System.Serializable]
public class Upgrade
{
    public string name;
    public float cost;
    public string resourceType;
    public Upgrade(string name, float cost, string resourceType)
    {
        this.name = name;
        this.cost = cost;
        this.resourceType = resourceType;
    }
}