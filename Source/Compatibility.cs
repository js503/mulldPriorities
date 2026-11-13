using System;
using System.Reflection;
using Verse;

namespace Mulld.Priorities
{
    public static class Compatibility
    {
        // Optional integration: do not reference or modify AutoPriorities' DLL.
        // Its per-map timer is public in the installed 1.7 version.
        public static string AutomaticManager(Map map)
        {
            foreach (var component in map.components)
            {
                var type = component.GetType();
                if (type.FullName != "AutoPriorities.Core.MapSpecificData") continue;
                try
                {
                    var timer = type.GetProperty("RunOnTimer", BindingFlags.Instance | BindingFlags.Public);
                    if (timer == null || timer.PropertyType != typeof(bool)) return "AutoPriorities (timer state unavailable)";
                    return (bool)timer.GetValue(component) ? "AutoPriorities" : null;
                }
                catch (Exception) { return "AutoPriorities (timer state unavailable)"; }
            }
            return null;
        }
    }
}
