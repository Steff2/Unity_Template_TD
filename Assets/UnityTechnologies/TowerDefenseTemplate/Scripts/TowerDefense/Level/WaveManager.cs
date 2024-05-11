using System;
using System.Collections;
using System.Collections.Generic;
using Core.Extensions;
using Unity.VisualScripting;
using UnityEngine;
using Assets.Scripts.Events;
using Random = UnityEngine.Random;

namespace TowerDefense.Level
{
	/// <summary>
	/// WaveManager - handles wave initialisation and completion
	/// </summary>
	public class WaveManager : MonoBehaviour
	{
		/// <summary>
		/// Current wave being used
		/// </summary>
		protected int m_CurrentIndex;

		/// <summary>
		/// Whether the WaveManager starts waves on Awake - defaulted to null since the LevelManager should call this function
		/// </summary>
		public bool startWavesOnAwake;

		/// <summary>
		/// The waves to run in order
		/// </summary>
		[Tooltip("Specify this list in order")]
		public List<Wave> waves = new List<Wave>();

		[SerializeField] TowerDefenseAgent selfPlayAttackAgent;

		Coroutine nextWaveReady;

		/// <summary>
		/// The current wave number
		/// </summary>
		public int waveNumber
		{
			get { return m_CurrentIndex + 1; }
		}

		/// <summary>
		/// The total number of waves
		/// </summary>
		public int totalWaves
		{
			get { return waves.Count; }
		}

		/// <summary>
		/// Get the current amount of waves running
		/// </summary>
		public Wave activeWave;

		public float waveProgress
		{
			get
			{
				if (waves == null || waves.Count <= m_CurrentIndex)
				{
					return 0;
				}
				return waves[m_CurrentIndex].progress;
			}
		}

		[SerializeField] CreateSetup setup;

        public bool setRandomWaves;

        public int randomMaxWaves;

        /// <summary>
        /// Called when a wave begins
        /// </summary>
        public event Action waveChanged;

		/// <summary>
		/// Called when all waves are finished
		/// </summary>
		public event Action spawningCompleted;

		/// <summary>
		/// Starts the waves
		/// </summary>
		public virtual void StartWaves()
		{
            if (setRandomWaves)
            {
                setup.WaveSetup();
            }

            if (waves.Count > 0)
			{
				InitCurrentWave();
			}
			else
			{
				Debug.LogWarning("[LEVEL] No Waves on wave manager. Calling spawningCompleted");
				SafelyCallSpawningCompleted();
			}
		}

		/// <summary>
		/// Inits the first wave
		/// </summary>
		protected virtual void Awake()
		{
            LevelManager.instance.resetWaves += ResetWaves;
            randomMaxWaves = Random.Range(2, 5);
			if (startWavesOnAwake)
			{
				StartWaves();
			}
		}

		/// <summary>
		/// Sets up the next wave
		/// </summary>
		protected virtual void NextWave()
		{
            if (waves[m_CurrentIndex] != null)
            {
                waves[m_CurrentIndex].waveCompleted -= NextWave;
            }

            if (m_CurrentIndex + 1 < waves.Count)
			{
                nextWaveReady = StartCoroutine(WaitUntilNextWaveReady());
            }
            else
			{
                SafelyCallSpawningCompleted();
            }
        }

		/// <summary>
		/// Initialize the current wave
		/// </summary>
		protected virtual void InitCurrentWave()
		{
			Wave wave = null;

            try
			{
                wave = waves[m_CurrentIndex];
            }
			catch (Exception e)
			{
				Debug.LogError("Wave Index out of bounds: Index " + m_CurrentIndex + " out of " + (waves.Count - 1));
                return;
			}

            List<string> waveUnits = new List<string>();
            foreach (var instruction in wave.spawnInstructions)
            {
                waveUnits.Add(instruction.agentConfiguration.agentName);
            }

            EventStore.Add(new WaveQueuedEvent
            {
                UnitList = waveUnits,
                WaveNumber = waveNumber
            });

            wave.waveCompleted += NextWave;
			activeWave = wave;
			wave.Init();
            waveChanged?.Invoke();
        }

		/// <summary>
		/// Calls spawningCompleted event
		/// </summary>
		protected virtual void SafelyCallSpawningCompleted()
		{
			StartCoroutine(WaitUntilAllUnitDead());
        }

		public void ResetWaves()
		{
            //Generate for loop in waves
            if(nextWaveReady != null)
			{
				StopCoroutine(nextWaveReady);
			}

            m_CurrentIndex = 0;
            randomMaxWaves = Random.Range(2, 5);
            if (activeWave != null)
			{
                if (setRandomWaves)
                {
                    setup.WaveSetup();
                }

                activeWave.waveCompleted -= NextWave;
				activeWave.ResetWave();
				activeWave = null;

			}

			foreach(Wave wave in waves)
			{
				if(wave != null)
				{
                    Destroy(wave.gameObject);
                }
            }

            waves.Clear();

        }

        IEnumerator WaitUntilNextWaveReady()
		{
            yield return new WaitUntil(() => waves[m_CurrentIndex + 1].spawnInstructions.Count == 15);
			if(waves.Count <= 0)
			{
				yield break;
			}
            m_CurrentIndex++;

            InitCurrentWave();
        }

		IEnumerator WaitUntilAllUnitDead()
		{
            yield return new WaitUntil(() => LevelManager.instance.activeUnitAgents.Count == 0);

            spawningCompleted?.Invoke();

        }
    }
}