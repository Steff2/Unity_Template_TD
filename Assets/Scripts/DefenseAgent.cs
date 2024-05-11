using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using TowerDefense.Towers;
using TowerDefense.Level;
using TowerDefense.Towers.Placement;
using Core.Utilities;
using TowerDefense.UI.HUD;
using Core.Economy;
using System.Linq;

public class DefenseAgent : Agent
{

    protected int fileNumber = 1;
    private struct PlacedTowerData
    {
        public int towerType;
        public IntVector2 placementGridCoordinates;
        public int gridTileNumber;
    }

    private Dictionary<int, Tower> towersDictionary;

    [SerializeField] private List<TowerPlacementGrid> placementGrids;
    private TowerPlacementGrid placementArea;


    [SerializeField] private PlayerHomeBase homeBase;
    [SerializeField] private float baseHealth;

    [SerializeField] BufferSensorComponent m_BufferSensor_Towers;
    [SerializeField] BufferSensorComponent m_BufferSensor_Units;

    private List<PlacedTowerData> m_GridTowerOccupationRepresentative = new List<PlacedTowerData>();

    private Tower towertoPlace;
    private int towerIndex;
    private Currency currency;
    private int gridXCoordinateConvertedToContinuousActionScale;
    private int gridYCoordinateConvertedToContinuousActionScale;

    public Team team = Team.Defend;

    private int HighestPlacementGridPosition;

    public override void Initialize()
    {

        HighestPlacementGridPosition = m_BufferSensor_Towers.MaxNumObservables;

        LevelManager.instance.resetLose += Loss;
        LevelManager.instance.resetWin += Win;
        LevelManager.instance.homeBases[0].resetbaseHealth += ResetBaseHealth;

        LevelManager.instance.BuildingCompleted();

        LevelManager.instance.InitializeCurrency(team);
        currency = LevelManager.instance.currencyDefender;
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

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(currency.currentCurrency / 500);
        sensor.AddObservation(homeBase.configuration.currentHealth / baseHealth);

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
            //towerObservation[HighestPlacementGridPosition + 2] = (float)towersDictionary[m_GridTowerOccupationRepresentative[i].towerType].purchaseCost / 15f; //highest tower cost

            Debug.Log(i + " Element: " + "Grid Number: " + m_GridTowerOccupationRepresentative[i].gridTileNumber + " Coords: x: " + m_GridTowerOccupationRepresentative[i].placementGridCoordinates.x + ", y: " + m_GridTowerOccupationRepresentative[i].placementGridCoordinates.y + " TowerType: " + m_GridTowerOccupationRepresentative[i].towerType);
            m_BufferSensor_Towers.AppendObservation(towerObservation);
        }

        //Unit Observations
        for(int i = 0; i < LevelManager.instance.activeUnitsWithType.Count; i++)
        {
            float[] unitObservation = new float[15 + 3];

            try
            {
                unitObservation[i] = LevelManager.instance.activeUnitsWithType[i]; //Divided by MAX number type of units
            }
            catch (System.IndexOutOfRangeException)
            {
                Debug.Log("Index" + (LevelManager.instance.activeUnits[i]) + "out of range");
            }


            m_BufferSensor_Units.AppendObservation(unitObservation);
        }
        //!!WICHTIG!!
        //!!Neuen Buffersensor mit allen Einheiten auf dem Feld!!
        //!!Kommen Einheiten in Waves, einfach in nem Zeitintervall oder je nach wie man Währung hat?!!
        //!!Wichtig für die Beobachtung der Einheiten!!
    }

    public override void OnEpisodeBegin()
    {
        if (GameUI.instanceExists) { GameUI.instance.m_CurrentArea = placementArea; }

        m_GridTowerOccupationRepresentative.Clear();
        LevelManager.instance.ResetCurrency(team);
        LevelManager.instance.ResetDefender();
        LevelManager.instance.ResetAttacker();
        ResetBaseHealth();
    }
    public void BuildTower(ActionBuffers actions)
    {
        var discreteTowerTypeSelector = actions.DiscreteActions[0];
        //var discreteplacementGridSelector = actions.DiscreteActions[1];
        var continuousGridXCoordinate = actions.ContinuousActions[0];
        var continuousGridYCoordinate = actions.ContinuousActions[1];
        //var GridXCoordinateRandomizer = Random.Range(0, placementArea.dimensions.x - 1);
        //var GridYCoordinateRandomizer = Random.Range(0, placementArea.dimensions.y - 1);

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

        //Multiple Grid Placements
        /*TowerPlacementGrid placementGrid = null;
        //Generate switch case of discreteplacementGridSelector
        switch (discreteplacementGridSelector)
        {
            case 0:
                placementGrid = placementGrids[0];
                break;
            case 1:
                placementGrid = placementGrids[1];
                break;
            case 2:
                placementGrid = placementGrids[2];
                break;
            default:
                break;
        }*/


        if (!GameUI.instance.isBuilding) GameUI.instance.SetToBuildMode(towertoPlace);

        gridXCoordinateConvertedToContinuousActionScale = Mathf.RoundToInt (Mathf.Abs (continuousGridXCoordinate) * (placementArea.dimensions.x - 1));
        gridYCoordinateConvertedToContinuousActionScale = Mathf.RoundToInt (Mathf.Abs (continuousGridYCoordinate) * (placementArea.dimensions.y - 1));

        var placementGridCoordinate = new IntVector2(gridXCoordinateConvertedToContinuousActionScale, gridYCoordinateConvertedToContinuousActionScale);

        //var placeGridCoordinateRandom = new IntVector2(GridXCoordinateRandomizer, GridYCoordinateRandomizer);

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
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        BuildTower(actions);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        base.Heuristic(actionsOut);
    }

    public void Win()
    {
        SetReward(1f);
        EndEpisode();
    }

    public void Loss()
    {
        SetReward(-1f);
        EndEpisode();
    }

    public void ResetBaseHealth()
    {         
        homeBase.configuration.SetHealth(baseHealth);
    }

    /*public void OnApplicationQuit()
    {
        streamWriter.WriteString();
    }*/
}
