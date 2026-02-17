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
    public static void Postfix(object __instance, ref Il2CppList __result)
    {
        if (string.IsNullOrEmpty(_cachedModPath))
        {
            var dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            _cachedModPath = Path.Combine(dllDir ?? string.Empty, "InjectData", "blueprint");
        }

        var traverse = Traverse.Create(__instance);
        string configFileName = traverse.Field("config_file_name").GetValue<string>() ?? "build_blueprint_bin.sav";

        var existingGuids = new HashSet<string>();
        foreach (var bp in __result) existingGuids.Add(bp.guid);

        // --- 逻辑 A：保留传统文件夹扫描 ---
        ProcessFolderInjection(_cachedModPath, configFileName, __result, existingGuids);

        // --- 逻辑 B：模块化系统注入 ---
        // 注意：这里我们只读取 JSON 内容。路径解析已在 ModuleRegistry 中完成，存入了模块的字典里。
        foreach (var module in ModuleRegistry.ActiveModules)
        {
            foreach (var kvp in module.Blueprints)
            {
                string guid = kvp.Key;
                string absoluteJsonPath = kvp.Value; // 这里存的已经是绝对路径了

                if (existingGuids.Contains(guid)) continue;

                try
                {
                    if (!File.Exists(absoluteJsonPath)) continue;
                    string txt = File.ReadAllText(absoluteJsonPath);
                    BuildBlueprint blueprint = JsonUtility.FromJson<BuildBlueprint>(txt);
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
                    Plugin.Log.LogError($"[Module: {module.ModuleName}] 加载蓝图 {guid} 失败: {e.Message}");
                }
            }
        }

        try
        {
            traverse.Method("SortByName", new object[] { __result }).GetValue();
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"蓝图列表排序失败: {e.Message}");
        }
    }

    private static void ProcessFolderInjection(string path, string fileName, Il2CppList resultList, HashSet<string> existingGuids)
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
                BuildBlueprint blueprint = JsonUtility.FromJson<BuildBlueprint>(txt);
                if (blueprint != null)
                {
                    blueprint.guid = d.Name;
                    if (existingGuids.Add(blueprint.guid)) resultList.Add(blueprint);
                }
            }
            catch (Exception) { /* 忽略单个失败 */ }
        }
    }
}