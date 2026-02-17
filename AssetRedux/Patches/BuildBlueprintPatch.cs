using System.Diagnostics.CodeAnalysis;
using AssetRedux.Models;
using HarmonyLib;
using UnityEngine;
using Il2CppList = Il2CppSystem.Collections.Generic.List<BuildBlueprint>;

namespace AssetRedux.Patches;

[HarmonyPatch(typeof(BuildBlueprintHelper), nameof(BuildBlueprintHelper.GetBuildBlueprintList))]
public static class BuildBlueprintPatch
{
    [HarmonyPostfix]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public static void Postfix(object __instance, ref Il2CppList __result)
    {
        if (__result == null!) __result = new Il2CppList();

        var existingGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var bp in __result)
        {
            if (bp != null && !string.IsNullOrEmpty(bp.guid))
                existingGuids.Add(bp.guid);
        }

        foreach (var module in ModuleRegistry.ActiveModules)
        {
            // 访问由框架预扫描好的 LoadedBlueprints 缓存
            foreach (var kvp in module.LoadedBlueprints)
            {
                string guid = kvp.Key;
                string jsonContent = kvp.Value;

                if (existingGuids.Contains(guid)) continue;

                try
                {
                    BuildBlueprint blueprint = JsonUtility.FromJson<BuildBlueprint>(jsonContent);
                    if (blueprint != null)
                    {
                        blueprint.guid = guid;
                        if (existingGuids.Add(guid)) __result.Add(blueprint);
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"[AssetRedux] 蓝图注入失败 {guid}: {e.Message}");
                }
            }
        }
    }
}