using System;
using System.Timers;
using System.Windows;
using System.Collections.Generic;
using Core.Extensions;
using Core.Utilities;
using TowerDefense.Agents;
using TowerDefense.Agents.Data;
using TowerDefense.Nodes;
using UnityEngine;
using System.Linq;
using Assets.Scripts.Events;

namespace TowerDefense.Level
{
	/// <summary>
	/// A Wave is a TimedBehaviour, that uses the RepeatingTimer to spawn enemies
	/// </summary>
	public class Wave : TimedBehaviour
	{
		/// <summary>
		/// A list of instructions on how to spawn enemies
		/// </summary>
		public List<SpawnInstruction> spawnInstructions;

		/// <summary>
		/// The index of the current enemy to spawn
		/// </summary>
		protected int m_CurrentIndex;

		/// <summary>
		/// The RepeatingTimer used to spawn enemies
		/// </summary>
		protected RepeatingTimer m_SpawnTimer;

		/// <summary>
		/// The event that is fired when a Wave is completed
		/// </summary>
		public event Action waveCompleted;

		public virtual float progress
		{
			get { return (float) (m_CurrentIndex) / spawnInstructions.Count; }
		}

		/// <summary>
		/// Initializes the Wave
		/// </summary>
		public virtual void Init()
		{

            // If the wave is empty then warn the level designer and fire complete event
            if (spawnInstructions.Count == 0)
			{
				Debug.LogWarning("[LEVEL] Empty Wave");
				//SafelyBroadcastWaveCompletedEvent();
				return;
			}
            m_SpawnTimer = new RepeatingTimer(spawnInstructions[0].delayToSpawn, SpawnCurrent);
            StartTimer(m_SpawnTimer);

		}

        /// <summary>
        /// Handles spawning the current agent and sets up the next agent for spawning
        /// </summary>
        protected virtual void SpawnCurrent()
		{
			Spawn();
			if (!TrySetupNextSpawn())
			{
				SafelyBroadcastWaveCompletedEvent();
				// this is required so wave progress is still accurate
				m_CurrentIndex = 0;
				StopTimer(m_SpawnTimer);
			}
		}

		/// <summary>
		/// Spawns the current agent
		/// </summary>
		protected void Spawn()
		{
			if(m_CurrentIndex < spawnInstructions.Count)
			{
                SpawnInstruction spawnInstruction = spawnInstructions[m_CurrentIndex];
                SpawnAgent(spawnInstruction.agentConfiguration, spawnInstruction.startingNode);
            }

		}

		/// <summary>
		/// Tries to setup the next spawn
		/// </summary>
		/// <returns>true if there is another spawn instruction, false if not</returns>
		protected bool TrySetupNextSpawn()
		{
			bool hasNext = spawnInstructions.Next(ref m_CurrentIndex);
			if (hasNext)
			{
				SpawnInstruction nextSpawnInstruction = spawnInstructions[m_CurrentIndex];
				if (nextSpawnInstruction.delayToSpawn <= 0f)
				{
					SpawnCurrent();
				}
				else
				{
					m_SpawnTimer.SetTime(nextSpawnInstruction.delayToSpawn);
				}
			}

			return hasNext;
		}

		/// <summary>
		/// Spawns the agent
		/// </summary>
		/// <param name="agentConfig">The agent to spawn</param>
		/// <param name="node">The starting node that the agent uses</param>
		protected virtual void SpawnAgent(AgentConfiguration agentConfig, Node node)
		{
			Vector3 spawnPosition = node.GetRandomPointInNodeArea();

			var poolable = Poolable.TryGetPoolable<Poolable>(agentConfig.agentPrefab.gameObject);
			if (poolable == null)
			{
				return;
			}

			EventStore.Add(new UnitSpawnEvent
			{
				Timestamp = Time.realtimeSinceStartup,
				UnitType = agentConfig.agentName,
				UnitCount = LevelManager.instance.unitCount
            });

			LevelManager.instance.unitCount++;
            LevelManager.instance.activeUnitsWithType.Add(LevelManager.instance.nameToTypeDictUnits[agentConfig.agentName]);
			var agentInstance = poolable.GetComponent<Agent>();
			agentInstance.transform.position = spawnPosition;
			agentInstance.Initialize();
			agentInstance.SetNode(node);
			agentInstance.transform.rotation = node.transform.rotation;
            LevelManager.instance.activeUnits.Add(agentConfig);
            LevelManager.instance.activeUnitAgents.Add(agentInstance);
            agentInstance.removedAgent += LevelManager.instance.RemoveDeadAgent;

        }

        /// <summary>
        /// Launch the waveCompleted event
        /// </summary>
        protected void SafelyBroadcastWaveCompletedEvent()
		{
            LevelManager.instance.activeUnits = new();
			LevelManager.instance.activeUnitsWithType = new();
            waveCompleted?.Invoke();
        }

		public virtual void ResetWave()
		{
            if (m_SpawnTimer != null)
            {
                StopTimer(m_SpawnTimer);
            }

			for (int i = LevelManager.instance.activeUnitAgents.Count - 1; i >= 0; i-- )
			{
				try
				{
                    if (LevelManager.instance.activeUnitAgents[i].gameObject.activeInHierarchy)
                    {
                        LevelManager.instance.activeUnitAgents[i].KillAgent();
                    }
					else
					{
						LevelManager.instance.activeUnitAgents.RemoveAt(i);
					}
                }
                catch(Exception e)
				{
					Debug.LogError("List is: " + LevelManager.instance.activeUnitAgents.Count + " long and counter is on " + i);
					continue;
				}
            }

			m_CurrentIndex = 0;

			Destroy(gameObject);
        }
	}
}