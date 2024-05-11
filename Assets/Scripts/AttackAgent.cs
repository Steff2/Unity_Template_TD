using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using TowerDefense.Level;
using TowerDefense.Towers.Placement;
using Core.Utilities;
using TowerDefense.UI.HUD;
using Core.Economy;
using TowerDefense.Agents.Data;
using TowerDefense.Nodes;
using TowerDefense.Economy;

public class AttackAgent : Unity.MLAgents.Agent
{

    //FileWriter streamWriter = new FileWriter("DefenseAgent", "txt");
    //protected int fileNumber = 1

    public WaveManager waveManager;
    Wave currentWave;

    private List<SpawnInstruction> currentSpawnInstructionList;
    private int waveCounter;

    public List<SpawnInstruction> UnitsSpawnInstructionsList;

    private TowerPlacementGrid placementArea;

    protected List<TowerDefense.Agents.Agent> spawnedAgents;

    [SerializeField] CreateSetup setup;

    [SerializeField] private PlayerHomeBase homeBase;
    [SerializeField] private float baseHealth;

    [SerializeField] BufferSensorComponent m_BufferSensor_Towers;
    [SerializeField] BufferSensorComponent m_BufferSensor_Units;

    [SerializeField] private List<TowerPlacementGrid> placementGrids;

    private SpawnInstruction unitToSend;

    private Currency currency;
    private CurrencyGainer currencyGainer;

    public Team team = Team.Attack;

    private const int spawnDelayTime = 2;
    //private float spawnDelayTimer = 0;

    const int HighestPlacementGridPosition = 42;

    bool initializeWave = true;

    public override void Initialize()
    {
        currentSpawnInstructionList = new List<SpawnInstruction>();
        spawnedAgents = new List<TowerDefense.Agents.Agent> ();

        LevelManager.instance.resetLose += Win;
        LevelManager.instance.resetWin += Loss;
        LevelManager.instance.homeBases[0].resetbaseHealth += ResetBaseHealth;

        LevelManager.instance.InitializeCurrency(team);
        unitToSend = null;

        //spawnDelayTimer = spawnDelayTime;
    }


    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(currency.currentCurrency / 500);
        sensor.AddObservation(homeBase.configuration.currentHealth / baseHealth);

        //Tower Observations
        for (int i = 0; i < LevelManager.instance.m_Grid_TowerOccupation_List.Count; i++)
        {
            //HighestPlacementGridPosition + 3 
            float[] towerObservation = new float[HighestPlacementGridPosition + 3];

            try
            {
                towerObservation[LevelManager.instance.m_Grid_TowerOccupation_List[i].gridTileNumber] = (float)LevelManager.instance.m_Grid_TowerOccupation_List[i].towerType / 3f;
            }
            catch (System.IndexOutOfRangeException)
            {
                Debug.Log("Index" + (LevelManager.instance.m_Grid_TowerOccupation_List[i].gridTileNumber) + "out of range");
            }
            towerObservation[HighestPlacementGridPosition] = (float)LevelManager.instance.m_Grid_TowerOccupation_List[i].placementGridCoordinate.x / (float)(placementGrids[0].dimensions.x - 1);
            towerObservation[HighestPlacementGridPosition + 1] = (float)LevelManager.instance.m_Grid_TowerOccupation_List[i].placementGridCoordinate.y / (float)(placementGrids[0].dimensions.y - 1);
            //towerObservation[HighestPlacementGridPosition + 2] = (float)towersDictionary[m_GridTowerOccupationRepresentative[i].towerType].purchaseCost / 15f; //highest tower cost

            Debug.Log(i + " Element: " + "Grid Number: " + LevelManager.instance.m_Grid_TowerOccupation_List[i].gridTileNumber + " Coords: x: " + LevelManager.instance.m_Grid_TowerOccupation_List[i].placementGridCoordinate.x + ", y: " + LevelManager.instance.m_Grid_TowerOccupation_List[i].placementGridCoordinate.y + " TowerType: " + LevelManager.instance.m_Grid_TowerOccupation_List[i].towerType);
            m_BufferSensor_Towers.AppendObservation(towerObservation);
        }

        //Unit Observations
        for (int i = 0; i < LevelManager.instance.activeUnitsWithType.Count; i++)
        {
            float[] unitObservation = new float[15 + 3];

            try
            {
                unitObservation[i] = LevelManager.instance.activeUnitsWithType[i]; //Divided by MAX number type of units
            }
            catch (System.IndexOutOfRangeException)
            {
                Debug.Log("Index" + LevelManager.instance.activeUnits[i] + "out of range");
            }
            //unitObservation[LevelManager.instance.activeUnitsWithType.Count] = LevelManager.instance.activeUnits[i].purchaseCost / 20f;

            m_BufferSensor_Units.AppendObservation(unitObservation);
        }
    }


    public void Update()
    {
        /*if (spawnDelayTimer > 0)
        {
            spawnDelayTimer -= Time.deltaTime;
        }

        Debug.Log("Gold: " + currency.currentCurrency);
        Debug.Log("Income: " + currencyGainer.constantCurrencyAddition);*/
    }


    public override void OnEpisodeBegin()
    {
        if (GameUI.instanceExists) { GameUI.instance.m_CurrentArea = placementArea; }

        if (waveManager == null) { return; }
        waveManager.waves.Clear();


        if(LevelManager.instance.m_Grid_TowerOccupation_List.Count <= 0) 
        {
            setup.Initialize(1);
        }
        else
        {
            LevelManager.instance.ResetDefender();
        }


        if (spawnedAgents.Count > 0)
        {
            for (int i = 0; i < spawnedAgents.Count; i++)
            {
                spawnedAgents[i].KillAgent();
            }

            spawnedAgents.Clear();
            LevelManager.instance.ResetAttacker();
        }


        LevelManager.instance.ResetCurrency(team);
        currency = LevelManager.instance.currencyAttacker;
        currencyGainer = LevelManager.instance.currencyGainerAttacker;

        waveCounter = 0;
    }


    public void SendUnits(ActionBuffers actions)
    {
        var discreteUnitTypeSelector = actions.DiscreteActions[0];

        switch (discreteUnitTypeSelector)
        {
            case 0:
                unitToSend = UnitsSpawnInstructionsList[0];
                break;
            case 1:
                unitToSend = UnitsSpawnInstructionsList[1];
                break;
            default:
                break;
        }

        if (currentWave.spawnInstructions.Count == 15 && initializeWave == true)
        {
            waveManager.StartWaves();
            initializeWave = false;
        }

        //All of this needs to be synched with Timer to give money for other waves
        if ((waveManager.waves.Count == 0 || currentWave.spawnInstructions.Count == 15) && waveCounter <= waveManager.waves.Count)
        {
            HandleNewWave();
        }

        if(currency.TryPurchase(unitToSend.agentConfiguration.purchaseCost) && unitToSend != null)
        {
            currentSpawnInstructionList.Add(unitToSend);

            SpawnAgent(unitToSend.agentConfiguration, unitToSend.startingNode);

            currencyGainer.constantCurrencyAddition += unitToSend.agentConfiguration.extraIncome;
        }



        /*if (spawnDelayTimer <= 0 && currency.TryPurchase(unitToSend.agentConfiguration.purchaseCost) && unitToSend != null)
        {
            spawnDelayTimer = spawnDelayTime;

            SpawnAgent(unitToSend.agentConfiguration, unitToSend.startingNode);

            currencyGainer.constantCurrencyAddition += unitToSend.agentConfiguration.extraIncome;
        }*/
    }


    /// <summary>
    /// Same as in Wave Script
    /// </summary>
    /// <param name="agentConfig"></param>
    /// <param name="node"></param>
    protected virtual void SpawnAgent(AgentConfiguration agentConfig, Node node)
    {
        Vector3 spawnPosition = node.GetRandomPointInNodeArea();

        var poolable = Poolable.TryGetPoolable<Poolable>(agentConfig.agentPrefab.gameObject);
        if (poolable == null)
        {
            return;
        }
        var agentInstance = poolable.GetComponent<TowerDefense.Agents.Agent>();
        agentInstance.transform.position = spawnPosition;
        agentInstance.Initialize();
        agentInstance.SetNode(node);
        agentInstance.transform.rotation = node.transform.rotation;
        spawnedAgents.Add(agentInstance);
    }


    protected void HandleNewWave()
    {
        GameObject waveObject = new("Wave");
        waveObject.transform.parent = waveManager.gameObject.transform;

        currentWave = waveObject.AddComponent<Wave>();

        currentSpawnInstructionList.Clear();
        currentWave.spawnInstructions = currentSpawnInstructionList;

        waveManager.waves[waveCounter] = currentWave;
        waveCounter++;
    }


    public override void OnActionReceived(ActionBuffers actions)
    {
        SendUnits(actions);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        base.Heuristic(actionsOut);
    }

    protected void Win()
    {
        SetReward(1f);

        EndEpisode();
    }

    protected void Loss()
    {
        SetReward(-1f);

        EndEpisode();
    }

    public void ResetBaseHealth()
    {         
        homeBase.configuration.SetHealth(baseHealth);
    }
}


