using Core.Economy;
using Core.Utilities;
using System.Collections.Generic;
using System.Linq;
using TowerDefense.Level;
using TowerDefense.Towers.Placement;
using TowerDefense.Towers;
using TowerDefense.UI.HUD;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using Unity.MLAgents;
using TowerDefense.Agents.Data;
using TowerDefense.Economy;
using TowerDefense.Nodes;
using Unity.MLAgents.Policies;
using Assets.Scripts.Events;

public enum Team
{
    Attack = 0,
    Defend = 1
}

public class TowerDefenseAgent : Agent
{
    struct PlacedTowerData
    {
        public int towerType;
        public IntVector2 placementGridCoordinates;
        public int gridTileNumber;
    }

    public Team team;

    private Dictionary<int, Tower> towersDictionary;

    [SerializeField] private List<TowerPlacementGrid> placementGrids;
    private TowerPlacementGrid placementArea;


    [SerializeField] private PlayerHomeBase homeBase;
    [SerializeField] private float baseHealth;

    [SerializeField] BufferSensorComponent m_BufferSensor_Towers;
    [SerializeField] BufferSensorComponent m_BufferSensor_Units;

    private List<PlacedTowerData> m_GridTowerOccupationRepresentative = new List<PlacedTowerData>();
    public List<SpawnInstruction> UnitsSpawnInstructionsList;

    public Dictionary<int, string> typeToNameDictTowers = new() { { 0, "Plasma Lance" }, { 1, "Assault Cannon" }, { 2, "Rocket Platform" } };

    BehaviorParameters behaviorParameters;

    public WaveManager waveManager;
    Wave currentWave;

    private int waveCounter;
    protected List<TowerDefense.Agents.Agent> spawnedAgents;
    private SpawnInstruction unitToSend;

    private Tower towertoPlace;
    private int towerIndex;

    private Currency currency;
    private CurrencyGainer currencyGainer;

    private int gridXCoordinateConvertedToContinuousActionScale;
    private int gridYCoordinateConvertedToContinuousActionScale;

    private int HighestPlacementGridPosition;
    [SerializeField] private int maxUnitCap;

    public bool initializeWave = true;

    public float incomeEventTimer = 5;
    public float incomeEventcountdown;

    public override void Initialize()
    {

        HighestPlacementGridPosition = m_BufferSensor_Towers.MaxNumObservables;

        LevelManager.instance.ResetDefender();
        LevelManager.instance.ResetAttacker();

        behaviorParameters = GetComponent<BehaviorParameters>();

        if (behaviorParameters.TeamId == (int)Team.Attack)
        {
            team = Team.Attack;
        }
        else
        {
            team = Team.Defend;
        }
    
        if(team == Team.Attack)
        {
            unitToSend = null;
        }

        if(team == Team.Defend)
        {
            LevelManager.instance.BuildingCompleted();

            //currencyDefender = LevelManager.instance.currencyDefender;
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

        LevelManager.instance.InitializeCurrency(team);
        incomeEventcountdown = incomeEventTimer;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation((float)currency.currentCurrency / 500f);
        sensor.AddObservation(homeBase.configuration.currentHealth / baseHealth);
        if(team == Team.Attack)
        {
            sensor.AddObservation((float)currencyGainer.constantCurrencyAddition / 250f);
        }

        //Tower Observations
        for (int i = 0; i < m_GridTowerOccupationRepresentative.Count; i++)
        {
            //HighestPlacementGridPosition + 3 
            float[] towerObservation = new float[HighestPlacementGridPosition + 3];

            try
            {
                towerObservation[m_GridTowerOccupationRepresentative[i].gridTileNumber] = (float)m_GridTowerOccupationRepresentative[i].towerType / 3f;
            }
            catch (System.IndexOutOfRangeException)
            {
                Debug.Log("Index" + (m_GridTowerOccupationRepresentative[i].gridTileNumber) + "out of range");
            }
            towerObservation[HighestPlacementGridPosition] = (float)m_GridTowerOccupationRepresentative[i].placementGridCoordinates.x / (float)(placementGrids[0].dimensions.x - 1);
            towerObservation[HighestPlacementGridPosition + 1] = (float)m_GridTowerOccupationRepresentative[i].placementGridCoordinates.y / (float)(placementGrids[0].dimensions.y - 1);
            towerObservation[HighestPlacementGridPosition + 2] = (float)towersDictionary[m_GridTowerOccupationRepresentative[i].towerType].purchaseCost / 15f; //highest tower cost

            m_BufferSensor_Towers.AppendObservation(towerObservation);
        }

        //Unit Observations
        for (int i = 0; i < LevelManager.instance.activeUnitsWithType.Count; i++)
        {
            float[] unitObservation = new float[maxUnitCap + 3];

            try
            {
                unitObservation[i] = (float)LevelManager.instance.activeUnitsWithType[i]/3f; //Divided by MAX number type of units
            }
            catch (System.IndexOutOfRangeException)
            {
                Debug.Log("Index :" + i + " out of range of " + LevelManager.instance.activeUnitsWithType.Count);
            }

            unitObservation[maxUnitCap] = (float)LevelManager.instance.activeUnits[i].purchaseCost/30f;
            unitObservation[maxUnitCap + 1] = (float)LevelManager.instance.activeUnits[i].extraIncome/5f;

            m_BufferSensor_Units.AppendObservation(unitObservation);
        }
    }

    public void Update()
    {
        //Income Events
        incomeEventcountdown -= Time.deltaTime;

        if(incomeEventcountdown <= 0)
        {
            if (team == Team.Attack)
            {
                EventStore.Add(new IncomeEvent
                {
                    Timestamp = Time.realtimeSinceStartup,
                    team = "Attacker",
                    currency = currency.currentCurrency,
                    income = currencyGainer.constantCurrencyAddition
                });
            }

            if (team == Team.Defend)
            {
                EventStore.Add(new IncomeEvent
                {
                    Timestamp = Time.realtimeSinceStartup,
                    team = "Defender",
                    currency = currency.currentCurrency,
                    income = 0
                });
            }
            incomeEventcountdown = incomeEventTimer;
        }
    }

    public override void OnEpisodeBegin()
    {
        LevelManager.instance.ResetCurrency(team);
        LevelManager.instance.homeBases[0].configuration.SetHealth(baseHealth);

        //Reset all Agents, Currency and Lists
        if (team == Team.Attack)
        {
            if (waveManager == null) { return; }
            
            currency = LevelManager.instance.currencyAttacker;
            currencyGainer = LevelManager.instance.currencyGainerAttacker;

            waveCounter = 0;

            initializeWave = true;

            EventStore.Add(new NewRoundEvent
            {
                Timestamp = Time.realtimeSinceStartup,
                GameCount = LevelManager.instance.gameCount
            });
        }

        if (team == Team.Defend)
        {
            if (GameUI.instanceExists) { GameUI.instance.m_CurrentArea = placementArea; }
            m_GridTowerOccupationRepresentative.Clear();
            currency = LevelManager.instance.currencyDefender;
        }
    }

    #region Actions
    public void BuildTower(ActionBuffers actions)
    {
        var discreteTowerTypeSelector = actions.DiscreteActions[0];
        //var discreteplacementGridSelector = actions.DiscreteActions[1];
        var continuousGridXCoordinate = actions.ContinuousActions[0];
        var continuousGridYCoordinate = actions.ContinuousActions[1];

        var tower = towersDictionary[discreteTowerTypeSelector];

        towerIndex = discreteTowerTypeSelector;

        switch (discreteTowerTypeSelector)
        {
            case 0:
                towertoPlace = towersDictionary[0];
                break;
            case 1:
                towertoPlace = towersDictionary[1];
                break;
            case 2:
                towertoPlace = towersDictionary[2];
                break;
            default:
                break;
        }

        //Enable Building
        if (!GameUI.instance.isBuilding) GameUI.instance.SetToBuildMode(towertoPlace);

        //Transform action to grid coordinates
        gridXCoordinateConvertedToContinuousActionScale = Mathf.FloorToInt(Mathf.Abs(continuousGridXCoordinate) * (placementArea.dimensions.x));
        gridYCoordinateConvertedToContinuousActionScale = Mathf.FloorToInt(Mathf.Abs(continuousGridYCoordinate) * (placementArea.dimensions.y));

        var placementGridCoordinate = new IntVector2(gridXCoordinateConvertedToContinuousActionScale, gridYCoordinateConvertedToContinuousActionScale);
        GameUI.instance.m_GridPosition = placementGridCoordinate;
        var tempGridTileNumber = placementGridCoordinate.x + placementGridCoordinate.y * placementArea.dimensions.x + 1;

        //Check for valid placing
        if (!m_GridTowerOccupationRepresentative.Any(c => c.gridTileNumber == tempGridTileNumber) && GameUI.instance.BuyTower())
        {
            m_GridTowerOccupationRepresentative.Add(new PlacedTowerData
            {
                towerType = towerIndex,
                placementGridCoordinates = placementGridCoordinate,
                gridTileNumber = tempGridTileNumber
            });

            EventStore.Add(new TowerBuildingEvent
            {
                Timestamp = Time.realtimeSinceStartup,
                Tower = typeToNameDictTowers[discreteTowerTypeSelector],
                GridXCoords = placementGridCoordinate.x,
                GridYCoords = placementGridCoordinate.y
            });
        }
        else
        {
            GameUI.instance.CancelGhostPlacement();
        }
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
            case 2:
                unitToSend = UnitsSpawnInstructionsList[2];
                break;
            default:
                break;
        }

        //create a new wave
        if ((waveManager.waves.Count == 0 || currentWave.spawnInstructions.Count == maxUnitCap) && waveCounter < waveManager.randomMaxWaves)
        {
            HandleNewWave();
        }

        //Buy units
        if (unitToSend != null)
        {
            if (currency.TryPurchase(unitToSend.agentConfiguration.purchaseCost) && waveCounter <= waveManager.randomMaxWaves && currentWave.spawnInstructions.Count != maxUnitCap)
            {
                currentWave.spawnInstructions.Add(unitToSend);
                currencyGainer.constantCurrencyAddition += unitToSend.agentConfiguration.extraIncome;
                EventStore.Add(new UnitBuyingEvent
                {
                    Timestamp = Time.realtimeSinceStartup,
                    UnitType = unitToSend.agentConfiguration.agentName,
                    CurrentIncome = currencyGainer.constantCurrencyAddition
                });
            }
        }
         //initialize the waves
        if (initializeWave == true && currentWave.spawnInstructions.Count == maxUnitCap && waveManager.waves.Count > 0)
        {
            waveManager.StartWaves();
            initializeWave = false;
        }
    }

    //function to create new waves
    protected void HandleNewWave()
    {
        GameObject waveObject = new("Wave");
        waveObject.transform.parent = waveManager.gameObject.transform;

        currentWave = waveObject.AddComponent<Wave>();
        currentWave.spawnInstructions = new List<SpawnInstruction>();

        waveManager.waves.Add(currentWave);
        waveCounter++;
    }
    #endregion

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (team == Team.Defend)
        {
            BuildTower(actions);
        }

        if (team == Team.Attack)
        {
            SendUnits(actions);
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        base.Heuristic(actionsOut);
    }
}
