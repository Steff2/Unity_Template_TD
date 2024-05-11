using System;
using System.Collections.Generic;
using Core.Economy;
using Core.Health;
using Core.Utilities;
using TowerDefense.Agents;
using TowerDefense.Agents.Data;
using TowerDefense.Economy;
using TowerDefense.Towers;
using TowerDefense.Towers.Data;
using TowerDefense.UI.HUD;
using Unity.MLAgents;
using Unity.VisualScripting;
using UnityEngine;
using Assets.Scripts.Events;
using Agent = TowerDefense.Agents.Agent;
using ActionGameFramework.Health;
using UnityEditor;

namespace TowerDefense.Level
{
	/// <summary>
	/// The level manager - handles the level states and tracks the player's currency
	/// </summary>
	[RequireComponent(typeof(WaveManager))]
	public class LevelManager : Core.Utilities.Singleton<LevelManager>
    {
		/// <summary>
		/// The configured level intro. If this is null the LevelManager will fall through to the gameplay state (i.e. SpawningEnemies)
		/// </summary>
		public LevelIntro intro;

		/// <summary>
		/// The tower library for this level
		/// </summary>
		public TowerLibrary towerLibrary;

		public SimpleMultiAgentGroup AttackAgent;
		public SimpleMultiAgentGroup DefendAgent;
		/// <summary>
		/// The currency that the player starts with
		/// </summary>
		public int startingCurrency;

		public int startingCurrencyAttacker;
		public int startingCurrencyDefender;

		/// <summary>
		/// The controller for gaining currency
		/// </summary>
		//public CurrencyGainer currencyGainer;

		public CurrencyGainer currencyGainerAttacker { get; protected set; }
		//public CurrencyGainer currencyGainerDefender;

		public int defaultConstantCurrencyAddition;
		public float defaultConstantCurrencyGainRate;

		/// <summary>
		/// Configuration for if the player gains currency even in pre-build phase
		/// </summary>
		[Header("Setting this will allow currency gain during the Intro and Pre-Build phase")]
		public bool alwaysGainCurrency;

		/// <summary>
		/// The home bases that the player must defend
		/// </summary>
		public PlayerHomeBase[] homeBases;

		[SerializeField] int baseHealth;
		public Collider[] environmentColliders;

		/// <summary>
		/// The attached wave manager
		/// </summary>
		public WaveManager waveManager { get; protected set; }

        public List<Tower> activeTowers = new();
        public List<float> activeTowersWithType = new();

        public Dictionary<string, int> nameToTypeDictTowers = new() { { "Assault Cannon", 1 }, { "Rocket Platform", 2 }, { "Plasma Lance", 3 } };
        public Dictionary<string, int> nameToTypeDictUnits = new() { { "Hoverbuggy", 1 }, { "Hovercopter", 2 }, { "Hovertank", 3 } };

        public List<AgentConfiguration> activeUnits = new List<AgentConfiguration>();
        public List<float> activeUnitsWithType = new List<float>();
		public List<Agent> activeUnitAgents = new List<Agent>();

		public List<TowerDefenseAgent> Agents = new List<TowerDefenseAgent>();

        public List<PlacedTowerData> m_Grid_TowerOccupation_List = new List<PlacedTowerData>();

        /// <summary>
        /// Number of enemies currently in the level
        /// </summary>
        public int numberOfEnemies { get; protected set; }

		public int unitCount;

		public int gameCount;

		public bool loggingInLines;
        public int loggingCount;

        /// <summary>
        /// The current state of the level
        /// </summary>
        public LevelState levelState { get; protected set; }

		/// <summary>
		/// The currency controller
		/// </summary>
		//public Currency currency { get; protected set; }
		public Currency currencyAttacker { get; protected set; }
		public Currency currencyDefender { get; protected set; }

        /// <summary>
        /// Number of home bases left
        /// </summary>
        public int numberOfHomeBasesLeft { get; protected set; }

		/// <summary>
		/// Starting number of home bases
		/// </summary>
		public int numberOfHomeBases { get; protected set; }

		/// <summary>
		/// An accessor for the home bases
		/// </summary>
		public PlayerHomeBase[] playerHomeBases
		{
			get { return homeBases; }
		}

		/// <summary>
		/// If the game is over
		/// </summary>
		public bool isGameOver
		{
			get { return (levelState == LevelState.Win) || (levelState == LevelState.Lose); }
		}

		/// <summary>
		/// Fired when all the waves are done and there are no more enemies left
		/// </summary>
		public event Action levelCompleted;

		/// <summary>
		/// Fired when all of the home bases are destroyed
		/// </summary>
		public event Action levelFailed;

		/// <summary>
		/// Fired when the level state is changed - first parameter is the old state, second parameter is the new state
		/// </summary>
		public event Action<LevelState, LevelState> levelStateChanged;

		/// <summary>
		/// Fired when the number of enemies has changed
		/// </summary>
		public event Action<int> numberOfEnemiesChanged;

		/// <summary>
		/// Event for home base being destroyed
		/// </summary>
		public event Action resetWaves;

        public event Action resetTowers;


        /// <summary>
        /// Win for AttackAgent/Loss for DefendAgent
        /// </summary>
        public event Action resetLose;

		/// <summary>
		/// Loss for AttackAgent/Win for DefendAgent
		/// </summary>
		public event Action resetWin;

		/// <summary>
		/// Increments the number of enemies. Called on Agent spawn
		/// </summary>
		public virtual void IncrementNumberOfEnemies()
		{
			numberOfEnemies++;
			SafelyCallNumberOfEnemiesChanged();
		}

		/// <summary>
		/// Returns the sum of all HomeBases' health
		/// </summary>
		public float GetAllHomeBasesHealth()
		{
			float health = 0.0f;
			foreach (PlayerHomeBase homebase in homeBases)
			{
				health += homebase.configuration.currentHealth;
			}
			return health;
		}

		/// <summary>
		/// Decrements the number of enemies. Called on Agent death
		/// </summary>
		public virtual void DecrementNumberOfEnemies()
		{
			numberOfEnemies--;
			SafelyCallNumberOfEnemiesChanged();
			if (numberOfEnemies < 0)
			{
				Debug.LogError("[LEVEL] There should never be a negative number of enemies. Something broke!");
				numberOfEnemies = 0;
			}

			if (numberOfEnemies == 0 && levelState == LevelState.AllEnemiesSpawned)
			{
				ChangeLevelState(LevelState.Win);
			}
		}

		/// <summary>
		/// Completes building phase, setting state to spawn enemies
		/// </summary>
		public virtual void BuildingCompleted()
		{
			ChangeLevelState(LevelState.SpawningEnemies);
		}

		public void InitializeCurrency(Team team)
		{
			if (team == Team.Attack)
			{
                currencyAttacker = new Currency(startingCurrencyAttacker);
                currencyGainerAttacker = new CurrencyGainer();
                currencyGainerAttacker.Initialize(currencyAttacker, defaultConstantCurrencyAddition, defaultConstantCurrencyGainRate);
            }

			if (team == Team.Defend)
			{
                currencyDefender = new Currency(startingCurrencyDefender);
            }
        }

        /// <summary>
        /// Caches the attached wave manager and subscribes to the spawning completed event
        /// Sets the level state to intro and ensures that the number of enemies is set to 0
        /// </summary>
        protected override void Awake()
		{
			base.Awake();

			AttackAgent = new SimpleMultiAgentGroup();
			DefendAgent = new SimpleMultiAgentGroup();

            waveManager = GetComponent<WaveManager>();
			//waveManager.spawningCompleted += OnSpawningCompleted;
			waveManager.spawningCompleted += SafelyCallLevelCompleted;

			unitCount = 1;
			gameCount = 1;

            // Does not use the change state function as we don't need to broadcast the event for this default value
            levelState = LevelState.Intro;
			numberOfEnemies = 0;

			// If there's an intro use it, otherwise fall through to gameplay
			if (intro != null)
			{
				intro.introCompleted += IntroCompleted;
			}
			else
			{
				IntroCompleted();
			}

			// Iterate through home bases and subscribe
			numberOfHomeBases = homeBases.Length;
			numberOfHomeBasesLeft = numberOfHomeBases;
			for (int i = 0; i < numberOfHomeBases; i++)
			{
				homeBases[i].removed += OnHomeBaseDestroyed;
			}

			homeBases[0].resetbaseHealth += ResetBaseHealth;
		}

        public void Start()
        {

            EventStore.StartTime = DateTime.UtcNow;

            foreach (TowerDefenseAgent item in Agents)
			{
				if (item != null)
				{
					if(item.team == Team.Attack) 
					{
						AttackAgent.RegisterAgent(item);
                    }
                    if (item.team == Team.Defend)
                    {
                        AttackAgent.RegisterAgent(item);
                    }
                }
			}
        }

        /// <summary>
        /// Updates the currency gain controller
        /// </summary>
        protected virtual void Update()
		{
			if ((alwaysGainCurrency ||
			    (!alwaysGainCurrency && levelState != LevelState.Building && levelState != LevelState.Intro)) && currencyGainerAttacker != null)
			{
				currencyGainerAttacker.Tick(Time.deltaTime);
            }
        }

		/// <summary>
		/// Unsubscribes from events
		/// </summary>
		protected override void OnDestroy()
		{
			base.OnDestroy();
			if (waveManager != null)
			{
				//waveManager.spawningCompleted -= OnSpawningCompleted;
                waveManager.spawningCompleted -= SafelyCallLevelCompleted;

            }
            if (intro != null)
			{
				intro.introCompleted -= IntroCompleted;
			}

			// Iterate through home bases and unsubscribe
			for (int i = 0; i < numberOfHomeBases; i++)
			{
				homeBases[i].died -= OnHomeBaseDestroyed;
			}
		}

		/// <summary>
		/// Fired when Intro is completed or immediately, if no intro is specified
		/// </summary>
		protected virtual void IntroCompleted()
		{
			ChangeLevelState(LevelState.Building);
		}

		/// <summary>
		/// Fired when the WaveManager has finished spawning enemies
		/// </summary>
		protected virtual void OnSpawningCompleted()
		{
			ChangeLevelState(LevelState.AllEnemiesSpawned);
		}

		/// <summary>
		/// Changes the state and broadcasts the event
		/// </summary>
		/// <param name="newState">The new state to transitioned to</param>
		protected virtual void ChangeLevelState(LevelState newState)
		{
			// If the state hasn't changed then return
			if (levelState == newState)
			{
				return;
			}

			LevelState oldState = levelState;
			levelState = newState;
			if (levelStateChanged != null)
			{
				levelStateChanged(oldState, newState);
			}
			
			switch (newState)
			{
				case LevelState.SpawningEnemies:
					//waveManager.StartWaves();
					break;
				case LevelState.AllEnemiesSpawned:
					// Win immediately if all enemies are already dead
					if (numberOfEnemies == 0)
					{
						ChangeLevelState(LevelState.Win);
					}
					break;
				case LevelState.Lose:
					SafelyCallLevelFailed();
					break;
				case LevelState.Win:
					SafelyCallLevelCompleted();
					break;
			}
		}

		/// <summary>
		/// Fired when a home base is destroyed
		/// </summary>
		protected virtual void OnHomeBaseDestroyed(DamageableBehaviour homeBase)
		{
			// Decrement the number of home bases
			if (numberOfHomeBasesLeft > 0) { numberOfHomeBasesLeft--; }

			// Call the destroyed event

			// If there are no home bases left and the level is not over then set the level to lost
			if ((numberOfHomeBasesLeft == 0) && !isGameOver)
			{
				EndGameResult(Team.Attack);
                resetLose?.Invoke();
				//ChangeLevelState(LevelState.Lose);
			}
		}

		/// <summary>
		/// Calls the <see cref="levelCompleted"/> event
		/// </summary>
		protected virtual void SafelyCallLevelCompleted()
		{
			EndGameResult(Team.Defend);
			resetWin?.Invoke();

			/*if (levelCompleted != null)
			{
				levelCompleted();
			}*/
		}

		/// <summary>
		/// Calls the <see cref="numberOfEnemiesChanged"/> event
		/// </summary>
		protected virtual void SafelyCallNumberOfEnemiesChanged()
		{
			if (numberOfEnemiesChanged != null)
			{
				numberOfEnemiesChanged(numberOfEnemies);
			}
		}

		/// <summary>
		/// Calls the <see cref="levelFailed"/> event
		/// </summary>
		protected virtual void SafelyCallLevelFailed()
		{
			if (levelFailed != null)
			{
				levelFailed();
			}
		}

        public void EndGameResult(Team winner)
        {
            if (winner == Team.Attack)
            {
				AttackAgent.AddGroupReward(1f);
				DefendAgent.AddGroupReward(-1f);
                EventStore.Add(new GameEndEvent
                {
                    Timestamp = Time.realtimeSinceStartup,
                    Winner = (int)winner
                });
            }
            else
            {
                DefendAgent.AddGroupReward(1f);
                AttackAgent.AddGroupReward(-1f);
                EventStore.Add(new GameEndEvent
                {
                    Timestamp = Time.realtimeSinceStartup,
                    Winner = (int)winner
                });
            }

            EventStore.WriteLog(true);

            ResetAttacker();
            ResetDefender();

            unitCount = 1;
            gameCount++;

            AttackAgent.EndGroupEpisode();
			DefendAgent.EndGroupEpisode();

			if(gameCount >= loggingCount)
			{
                EditorApplication.ExitPlaymode();
			}
        }

        public void ResetCurrency(Team team)
		{
			if (currencyAttacker != null && team == Team.Attack)
			{
                currencyAttacker = new Currency(startingCurrencyAttacker);
				currencyGainerAttacker = new CurrencyGainer();
                currencyGainerAttacker.Initialize(currencyAttacker, defaultConstantCurrencyAddition, defaultConstantCurrencyGainRate);
            }

			if (currencyDefender != null && team == Team.Defend)
			{
                currencyDefender = new Currency(startingCurrencyDefender);
            }

        }

        public void ResetAttacker()
        {
			if (activeUnits == null || activeTowersWithType == null) return;
            resetWaves?.Invoke();

			activeUnitAgents.Clear();
            activeUnits.Clear();
			activeUnitsWithType.Clear();
			activeUnitAgents = new List<Agent>();
            activeUnits = new List<AgentConfiguration>();
            activeUnitsWithType = new List<float>();
        }

        public void ResetDefender()
        {
			if (activeTowers == null || activeTowersWithType == null) return;

            resetTowers?.Invoke();

			activeTowers.Clear();
			activeTowersWithType.Clear();
            activeTowers = new List<Tower>();
            activeTowersWithType = new List<float>();
        }

		public void RemoveDeadAgent(Agent agent)
		{
			agent.removedAgent -= RemoveDeadAgent;
            activeUnitAgents.Remove(agent);
        }

		public void ResetBaseHealth()
		{
			homeBases[0].configuration.SetHealth(baseHealth);
			Debug.Log(homeBases[0].configuration.currentHealth);

        }

        public void OnApplicationQuit()
        {
            EventStore.WriteLog(false);
        }
    }
}