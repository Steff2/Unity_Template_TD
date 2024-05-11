using Assets.Scripts.Events;
using System;
using System.Collections.Generic;
using TMPro;
using TowerDefense.Agents;
using TowerDefense.Level;
using TowerDefense.Towers;
using UnityEngine;
using Core.Utilities;

namespace Core.Health
{
	/// <summary>
	/// Abstract class for any MonoBehaviours that can take damage
	/// </summary>
	public class DamageableBehaviour : MonoBehaviour
	{
		/// <summary>
		/// The Damageable object
		/// </summary>
		public Damageable configuration;

		/// <summary>
		/// Gets whether this <see cref="DamageableBehaviour" /> is dead.
		/// </summary>
		/// <value>True if dead</value>
		public bool isDead
		{
			get { return configuration.isDead; }
		}

		/// <summary>
		/// The position of the transform
		/// </summary>
		public virtual Vector3 position
		{
			get { return transform.position; }
		}

		private Dictionary<float, String> damageToTowerNameDict = new() { { 0.25f, "Assault Cannon" }, { 3f, "Rocket Platform" }, { 2f, "Rocket Platform" }, { 1.5f, "Rocket Platform" }, { 1f, "Rocket Platform" }, { 12f, "Plasma Lance" } };

		/// <summary>
		/// Occurs when damage is taken
		/// </summary>
		public event Action<HitInfo> hit;
		
		/// <summary>
		/// Event that is fired when this instance is removed, such as when pooled or destroyed
		/// </summary>
		public event Action<DamageableBehaviour> removed;
		public event Action<Agent> removedAgent;
		
		/// <summary>
		/// Event that is fired when this instance is killed
		/// </summary>
		public event Action<DamageableBehaviour> died;

		public event Action resetbaseHealth;
		
		/// <summary>
		/// Takes the damage and also provides a position for the damage being dealt
		/// </summary>
		/// <param name="damageValue">Damage value.</param>
		/// <param name="damagePoint">Damage point.</param>
		/// <param name="alignment">Alignment value</param>
		public virtual void TakeDamage(float damageValue, Vector3 damagePoint, IAlignmentProvider alignment)
		{
			HealthChangeInfo info;
			configuration.TakeDamage(damageValue, alignment, out info);

            var agent = gameObject.GetComponent<Agent>();
            if (agent != null)
			{
                EventStore.Add(new UnitDamageEvent
                {
					Timestamp = Time.realtimeSinceStartup,
                    UnitType = gameObject.name,
                    DamageAmount = damageValue,
                    HP = info.newHealth,
					AttackerName = damageToTowerNameDict[damageValue]
                });

				if(damageValue >= info.newHealth && info.newHealth > 0)
				{
					EventStore.Add(new UnitDeathEvent
                    {
						Timestamp = Time.realtimeSinceStartup,
                        UnitType = gameObject.name,
                        OverKillDamageAmount = damageValue - info.newHealth,
                        KillTower = damageToTowerNameDict[damageValue]
                    });
                }
            }

			var tower = gameObject.GetComponent<Tower>();
			if (tower != null)
			{
                EventStore.Add(new TowerDamageEvent
                {
                    Timestamp = Time.realtimeSinceStartup,
                    TowerType = gameObject.name,
                    GridXCoords = tower.placementArea.WorldToGrid(gameObject.transform.position, new IntVector2(1, 1)).x,
                    GridYCoords = tower.placementArea.WorldToGrid(gameObject.transform.position, new IntVector2(1, 1)).y,
                    DamageAmount = damageValue,
                    HP = info.newHealth,
                });

                if (damageValue >= info.newHealth && info.newHealth > 0)
                {
                    EventStore.Add(new TowerDestroyedEvent
                    {
                        Timestamp = Time.realtimeSinceStartup,
                        TowerType = gameObject.name,
                        GridXCoords = tower.placementArea.WorldToGrid(gameObject.transform.position, new IntVector2(1, 1)).x,
                        GridYCoords = tower.placementArea.WorldToGrid(gameObject.transform.position, new IntVector2(1, 1)).y,

                    });
                }
            }

            var damageInfo = new HitInfo(info, damagePoint);
			if (hit != null)
			{
				hit(damageInfo);
			}
		}

		protected virtual void Awake()
		{
			configuration.Init();
			configuration.died += OnConfigurationDied;
		}

		/// <summary>
		/// Kills this damageable
		/// </summary>
		protected virtual void Kill()
		{
			HealthChangeInfo healthChangeInfo;
			configuration.TakeDamage(configuration.currentHealth, null, out healthChangeInfo);
		}


		/// <summary>
		/// Removes this damageable without killing it
		/// </summary>
		public virtual void Remove()
		{
			// Set health to zero so that this behaviour appears to be dead. This will not fire death events

			configuration.SetHealth(0); 

            OnRemoved();
		}

		/// <summary>
		/// Fires kill events
		/// </summary>
		void OnDeath()
		{
			if (died != null)
			{
                died(this);
            }

            // Fire the agent death event
        }
		
		/// <summary>
		/// Fires the removed event
		/// </summary>
		void OnRemoved()
		{
			if (removed != null)
			{
                var agent = gameObject.GetComponent<Agent>();
                if (agent != null)
				{
					removedAgent(agent);
                }
                removed(this);
			}
		}
		
		/// <summary>
		/// Event fired when Damageable takes critical damage
		/// </summary>
		void OnConfigurationDied(HealthChangeInfo changeInfo)
		{
			OnDeath();
			Remove();
        }
	}
}