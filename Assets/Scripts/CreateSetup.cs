using Core.Utilities;
using System.Collections;
using System.Collections.Generic;
using TowerDefense.Agents.Data;
using TowerDefense.Level;
using TowerDefense.Towers.Placement;
using TowerDefense.Towers;
using Unity.VisualScripting;
using UnityEngine;
using System.Linq;
using TowerDefense.UI.HUD;

public struct PlacedTowerData
{
    public int towerType;
    public IntVector2 placementGridCoordinate;
    public int gridTileNumber;
}

public class CreateSetup : MonoBehaviour
{
    public WaveManager waveManager;
    public List<SpawnInstruction> spawnInstructions;

    protected RepeatingTimer towerSpawnTimer;

    private Dictionary<int, Tower> towersDictionary;

    public List<TowerPlacementGrid> placementGrids;
    TowerPlacementGrid placementArea;
    private Tower towertoPlace;
    private int towerIndex;

    public void Awake()
    {
        towertoPlace = null;
        towersDictionary = new Dictionary<int, Tower>();
        placementArea = placementGrids[0];

        var i = 0;
        //Store items in LevelManager.instance.towerLibrary in towersDictionary
        foreach (var tower in LevelManager.instance.towerLibrary)
        {
            towersDictionary.TryAdd(i, tower);
            i++;
        }
    }

    public void Initialize(float buildingInterval)
    {
        towerSpawnTimer = new RepeatingTimer(buildingInterval, TowerSetup);
    }

    public void WaveSetup()
    {
        if(waveManager == null) { return; }

        waveManager.waves.Clear();

        LevelManager.instance.ResetAttacker();

        var waveLength = Random.Range(1, 5);

        //Filling Wavemanager with waves
        for(int i = 0; i <= waveLength; i++)
        {
            GameObject waveObject = new("Wave" + i);
            waveObject.transform.parent = waveManager.gameObject.transform;

            Wave wave = waveObject.AddComponent<Wave>();

            List<SpawnInstruction> spawnInstructionList = new List<SpawnInstruction>();

            //Filling Waves with Units
            for (int k = 0; k < 15; k++)
            {
                spawnInstructionList.Add(spawnInstructions[Random.Range(0, spawnInstructions.Count)]);
            }
            wave.spawnInstructions = spawnInstructionList;

            waveManager.waves.Add(wave);
        }
    }

    public void Update()
    {
        towerSpawnTimer?.Tick(Time.deltaTime);
    }

    protected void TowerSetup()
    {
        towerIndex = Random.Range(0, towersDictionary.Count - 1);

        if (!GameUI.instance.isBuilding) GameUI.instance.SetToBuildMode(towertoPlace);

        var GridXCoordinateRandomizer = Random.Range(0, placementArea.dimensions.x);
        var GridYCoordinateRandomizer = Random.Range(0, placementArea.dimensions.y);
        var placementGridCoordinate = new IntVector2(GridXCoordinateRandomizer, GridYCoordinateRandomizer);

        GameUI.instance.m_GridPosition = placementGridCoordinate;

        var tempGridTileNumber = placementGridCoordinate.x + placementGridCoordinate.y * placementArea.dimensions.x + 1;

        //Check if Placing is valid
        if (!LevelManager.instance.m_Grid_TowerOccupation_List.Any(c => c.placementGridCoordinate == placementGridCoordinate)) //&& GameUI.instance.BuyTower())
        {
            LevelManager.instance.m_Grid_TowerOccupation_List.Add(new PlacedTowerData
            {
                towerType = towerIndex,
                placementGridCoordinate = placementGridCoordinate,
                gridTileNumber = tempGridTileNumber
            });
            GameUI.instance.PlaceTower();
        }
    }
}
