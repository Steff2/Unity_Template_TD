using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;
using Core.Utilities;
using TowerDefense.Level;

namespace Assets.Scripts.Events
{
    [Serializable]
    public class GenericEvent
    {
        public float Timestamp { get; set; }
        public string Name { get; set; }
        public IDictionary<string, object> Data { get; set; }
    }

    [Serializable]
    public abstract class BaseEvent
    {
        public abstract string Name { get; }
        public float Timestamp { get; set; }

        public GenericEvent ToGenericEvent() => new GenericEvent
        {
            Timestamp = Timestamp,
            Name = Name,
            Data = Data
        };

        public abstract Dictionary<string, object> Data { get; }
    }

    [Serializable]
    public class NewRoundEvent : BaseEvent
    {
        public override string Name => "RoundStart";
        public int GameCount { get; set; }
        public override Dictionary<string, object> Data => new Dictionary<string, object>
        {
            { "gameCount", GameCount },
        };
    }

    [Serializable]
    public class UnitSpawnEvent : BaseEvent
    {
        public override string Name => "UnitSpawn";
        public string UnitType { get; set; }
        public int UnitCount { get; set; }
        public override Dictionary<string, object> Data => new Dictionary<string, object>
        {
            { "unitSpawned", UnitType },
            { "unitCount", UnitCount }
        };
    }

    [Serializable]
    public class WaveQueuedEvent : BaseEvent
    {
        public override string Name => "WaveQueued";
        public List<string> UnitList { get; set; }
        public int WaveNumber { get; set; }
        public override Dictionary<string, object> Data => new Dictionary<string, object>
        {
            { "waveUnits", UnitList },
            { "waveNumber", WaveNumber }
        };
    }

    [Serializable]
    public class UnitBuyingEvent : BaseEvent
    {
        public override string Name => "UnitBuying";
        public string UnitType { get; set; }
        public int CurrentIncome { get; set; }
        public override Dictionary<string, object> Data => new Dictionary<string, object>
        {
            { "unitBought", UnitType },
            { "currentIncome", CurrentIncome }
        };
    }

    [Serializable]
    public class UnitDamageEvent : BaseEvent
    {
        public override string Name => "UnitDamage";
        public string UnitType { get; set; }
        public float DamageAmount { get; set; }
        public float HP { get; set; }
        public string AttackerName { get; set; }

        public override Dictionary<string, object> Data => new Dictionary<string, object>
        {
            { "unit", UnitType },
            { "damage", DamageAmount },
            { "hp", HP },
            { "attacker", AttackerName }

        };
    }

    [Serializable]
    public class UnitDeathEvent : BaseEvent
    {
        public override string Name => "UnitDeath";
        public string UnitType { get; set; }
        public float OverKillDamageAmount { get; set; }
        public string KillTower { get; set; }

        public override Dictionary<string, object> Data => new Dictionary<string, object>
        {
            { "unit", UnitType },
            { "overkillDamage", OverKillDamageAmount },
            { "killTower", KillTower },
        };
    }

    [Serializable]
    public class TowerBuildingEvent : BaseEvent
    {
        public override string Name => "TowerBuilding";
        public string Tower { get; set; }
        public int GridXCoords { get; set; }
        public int GridYCoords { get; set; }

        public override Dictionary<string, object> Data => new Dictionary<string, object>
        {
            { "tower", Tower },
            { "X", GridXCoords },
            { "Y", GridYCoords }
        };
    }

    [Serializable]
    public class TowerDamageEvent : BaseEvent
    {
        public override string Name => "TowerDamaged";
        public string TowerType { get; set; }
        public int GridXCoords { get; set; }
        public int GridYCoords { get; set; }
        public float DamageAmount { get; set; }
        public float HP { get; set; }

        public override Dictionary<string, object> Data => new Dictionary<string, object>
        {
            { "damagedTower", TowerType },
            { "X", GridXCoords },
            { "Y", GridYCoords },
            { "towerDamage", DamageAmount },
            { "hp", HP },
        };
    }

    [Serializable]
    public class TowerDestroyedEvent : BaseEvent
    {
        public override string Name => "TowerDestroyed";
        public string TowerType { get; set; }
        public int GridXCoords { get; set; }
        public int GridYCoords { get; set; }

        public override Dictionary<string, object> Data => new Dictionary<string, object>
        {
            { "destroyedTower", TowerType },
            { "X", GridXCoords },
            { "Y", GridYCoords }
        };
    }

    [Serializable]
    public class BaseDamageEvent : BaseEvent
    {
        public override string Name => "BaseDamage";

        public float Damage { get; set; }
        public string Unit { get; set; }

        public override Dictionary<string, object> Data => new Dictionary<string, object>
        {
            { "damage", Damage },
            { "unitDamaged", Unit },
        };
    }

    [Serializable]
    public class IncomeEvent : BaseEvent
    {
        public override string Name => "Economy";

        public string team { get; set; }
        public float currency { get; set; } 
        public float income { get; set; }

        public override Dictionary<string, object> Data => new Dictionary<string, object>
        {
            { "team", team },
            { "currency", currency },
            { "income", income },
        };
    }

    [Serializable]
    public class GameEndEvent : BaseEvent
    {
        public override string Name => "GameEnd";

        public int Winner { get; set; }

        public override Dictionary<string, object> Data => new Dictionary<string, object>
        {
            { "winner", Winner },
        };
    }

    public static class EventStore
    {
        private static List<GenericEvent> AllGameEvents = new List<GenericEvent>();
        private static List<GenericEvent> SingleGameEvents = new List<GenericEvent>();
        private static int fileCount = 1;

        public static DateTime StartTime;

        public static void Add(BaseEvent e)
        {
            AllGameEvents.Add(e.ToGenericEvent());
            SingleGameEvents.Add(e.ToGenericEvent());
        }

        public static void Clear(List<GenericEvent> Events)
        {
            Events.Clear();
        }

        public static string Json(List<GenericEvent> Events)
        {
            return JsonConvert.SerializeObject(Events);
        }

        // Returns path to logfile
        public static string WriteLog(bool singleGame)
        {
            var path = Directory.CreateDirectory(Path.Combine(Application.dataPath, "Gameplay_Logs")).FullName;

            var fileName = $"{StartTime:yyyy-MM-dd_HH-mm-ss}_Game_{fileCount}_3mil_Steps_WaveEvent_Try.json";

            var logPath = Path.Combine(path, fileName);

            var writer = new StreamWriter(logPath);

            if(singleGame)
            {
                writer.Write(Json(SingleGameEvents));

                writer.Flush();
                writer.Close();

                Clear(SingleGameEvents);
            }
            else
            {
                writer.Write(Json(AllGameEvents));

                writer.Flush();
                writer.Close();

                Clear(AllGameEvents);
            }

            fileCount++;

            Debug.Log($"Log written to {logPath}");

            return logPath;
        }
    }
}
