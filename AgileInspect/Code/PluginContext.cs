using AgileInspect;
using AgileInspect.Code.PluginContracts;
using Newtonsoft.Json.Linq;
using System;

public static class PluginContext
{
    public static IAppCallback Callback { get; private set; }

    public static void SetCallback(IAppCallback callback)
    {
        Callback = callback;
    }

    public static void Log(string pluginName, string message)
    {
        if (Callback != null)
        {
            try
            {
                Callback.OnLog(pluginName, message);
            }
            catch (Exception ex)
            {
                DebugLog.WriteLine($"[{pluginName}] Failed to send log callback: {ex}");
            }
        }
    }

    public static void SendDetectionResult(string pluginName, JToken jsonResult)
    {
        if (Callback != null)
        {
            try
            {
                Callback.OnDetectionResult(pluginName, jsonResult);
            }
            catch (Exception ex)
            {
                Log(pluginName, $"Failed to send detection result: {ex}");
            }
        }
    }
}
