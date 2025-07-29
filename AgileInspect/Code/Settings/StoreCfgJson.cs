using System.Collections.Generic;

namespace AgileInspect
{
    public class StoreCfgJson
    {
        #region Singleton
        public static StoreCfgJson Instance { get; set; }
        public StoreCfgJson()
        {
            Instance = this;
        }
        #endregion
        public static void SetCurrentStoreConfig(StoreCfgJson config)
        {
            Instance ??= new StoreCfgJson();

            Instance.customerID = config.customerID;
            Instance.serverUrl = config.serverUrl;
            Instance.hash = config.hash;
            Instance.deployType = config.deployType;

            Instance.logRotation = config.logRotation ?? new LogRotation();
            Instance.eventConfig = config.eventConfig ?? new EventConfig();
            Instance.ruleConfig = config.ruleConfig ?? new RuleConfig();
            Instance.ruleConditionQueueConfig = config.ruleConditionQueueConfig ?? new RuleConditionQueueConfig();
            Instance.eventQueueConfig = config.eventQueueConfig ?? new EventQueueConfig();
        }
        public static StoreCfgJson GetCurrentStoreConfig()
        {
            return Instance;
        }
        public string customerID { get; set; } = "60f773a842963f002e73a25b";
        public string serverUrl { get; set; } = "https://a320-171-240-159-247.ngrok-free.app";
        public string hash { get; set; } = "60f773a842963f002e73a25b";
        public string deployType { get; set; } = "serverless";
        public LogRotation logRotation { get; set; } = new LogRotation();
        public EventConfig eventConfig { get; set; } = new EventConfig();
        public RuleConfig ruleConfig { get; set; } = new RuleConfig();
        public RuleConditionQueueConfig ruleConditionQueueConfig { get; set; } = new RuleConditionQueueConfig();
        public EventQueueConfig eventQueueConfig { get; set; } = new EventQueueConfig();

    }
    public class EventQueueConfig
    {
        public int interval { get; set; } = 10;
    }
    public class RuleConditionQueueConfig
    {
        public int interval { get; set; } = 10;
    }
    public class LogRotation
    {
        public bool enable { get; set; } = true;
        public int size { get; set; } = 10 * 1024 * 1024;
        public int rotate { get; set; } = 5;
    }
    public class EventConfig
    {
        public string settingHash { get; set; } = "abc";
        public int settingPullInterval { get; set; } = 30000;
        public List<EventSetting> eventSettings { get; set; } = new();
    }
    public class EventSetting
    {
        public string eventType { get; set; } // Plugin Name
        public EventParams eventParams { get; set; }
        public string triggerType { get; set; } // interval or Realtime
        public TriggerParams triggerParams { get; set; }
    }
    public class EventParams
    {
        public string[] paths { get; set; } = [];
        public string[] filters { get; set; } = [];
        public string[] processes { get; set; } = [];
        public string[] services { get; set; } = [];
    }

    public class TriggerParams
    {
        public int interval { get; set; }
    }
    public class RuleConfig
    {
        public List<RuleSetting> ruleSettings { get; set; } = new();
    }
    public class RuleSetting
    {
        public List<string> plugins { get; set; } = new();
        public List<Condition> conditions { get; set; } = new();
        public string action { get; set; } = "none";
    }

    public class Condition
    {
        public string field { get; set; }
        public string expected { get; set; }
    }
}
