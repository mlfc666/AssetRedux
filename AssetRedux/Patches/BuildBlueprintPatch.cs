using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using AssetRedux.Models;
using HarmonyLib;
using UnityEngine;
using Il2CppList = Il2CppSystem.Collections.Generic.List<BuildBlueprint>;

namespace AssetRedux.Patches;

[HarmonyPatch(typeof(BuildBlueprintHelper), nameof(BuildBlueprintHelper.GetBuildBlueprintList))]
public static class BuildBlueprintPatch
{
    private static string? _cachedModPath;

    [HarmonyPostfix]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public static void Postfix(object __instance, ref Il2CppList __result)
    {
        // 确保结果列表不为 null
        if (__result == null!) __result = new Il2CppList();

        if (string.IsNullOrEmpty(_cachedModPath))
        {
            var dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            _cachedModPath = Path.Combine(dllDir ?? string.Empty, "InjectData", "blueprint");
        }

        var traverse = Traverse.Create(__instance);
        string configFileName = traverse.Field("config_file_name").GetValue<string>() ?? "build_blueprint_bin.sav";

        // 记录已有 GUID
        var existingGuids = new HashSet<string>();
        foreach (var bp in __result)
        {
            if (bp != null && !string.IsNullOrEmpty(bp.guid))
                existingGuids.Add(bp.guid);
        }

        // 保留传统文件夹扫描 
        ProcessFolderInjection(_cachedModPath, configFileName, __result, existingGuids);

        // 模块化系统注入
        foreach (var module in ModuleRegistry.ActiveModules)
        {
            foreach (var kvp in module.Blueprints)
            {
                string guid = kvp.Key;
                string jsonContent = kvp.Value; // 此时 Value 已经是文件内容字符串了

                if (existingGuids.Contains(guid)) continue;

                try
                {
                    // 直接从内存字符串解析，不再 File.ReadAllText
                    if (string.IsNullOrEmpty(jsonContent) || !jsonContent.StartsWith("{")) continue;

                    BuildBlueprint blueprint = JsonUtility.FromJson<BuildBlueprint>(jsonContent);
                    if (blueprint != null)
                    {
                        blueprint.guid = guid;
                        if (existingGuids.Add(guid))
                        {
                            __result.Add(blueprint);
                        }
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"[Module: {module.ModuleName}] 解析蓝图缓存 {guid} 失败: {e.Message}");
                }
            }
        }

        // 排序逻辑
        try
        {
            traverse.Method("SortByName", __result).GetValue();
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"蓝图列表排序失败: {e.Message}");
        }
    }

    /// <summary>
    /// 传统文件夹扫描逻辑抽取
    /// </summary>
    private static void ProcessFolderInjection(string path, string fileName, Il2CppList resultList,
        HashSet<string> existingGuids)
    {
        var rootDir = new DirectoryInfo(path);
        if (!rootDir.Exists) return;

        foreach (var d in rootDir.GetDirectories())
        {
            string fullPath = Path.Combine(d.FullName, fileName);
            if (!File.Exists(fullPath)) continue;

            try
            {
                string txt = File.ReadAllText(fullPath);
                if (string.IsNullOrEmpty(txt)) continue;

                BuildBlueprint blueprint = JsonUtility.FromJson<BuildBlueprint>(txt);
                if (blueprint != null)
                {
                    blueprint.guid = d.Name;
                    if (existingGuids.Add(blueprint.guid))
                        resultList.Add(blueprint);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"蓝图列表注入失败: {e.Message}");
            }
        }
    }
}