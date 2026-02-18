using System.Reflection;
using UnityEngine;

namespace AssetRedux.Services;

public static class VersionValidator
{
    private static readonly Dictionary<string, bool> ValidationCache = new();
    private static string? _cachedGameVersion;

    /// <summary>
    /// 获取并校验游戏版本
    /// </summary>
    public static void CheckGameVersion()
    {
        try
        {
            // 获取游戏版本，这个在实际运行中能取到
            // ReSharper disable once Unity.UnknownResource
            var versionAsset = Resources.Load<TextAsset>("version");
            if (versionAsset == null)
            {
                Plugin.Log.LogWarning("[VersionCheck] 未能读取到游戏 version 文件，跳过环境校验");
                return;
            }

            _cachedGameVersion = versionAsset.text.Trim();
            // 校验：插件核心 vs 游戏版本
            // 假设 PluginInfo.TargetVersion 是插件定义的兼容游戏版本
            if (_cachedGameVersion != AssetReduxInfo.TargetVersion)
            {
                Plugin.Log.LogWarning(
                    $"[VersionCheck] 游戏版本不匹配：核心支持 [{AssetReduxInfo.TargetVersion}]，当前环境 [{_cachedGameVersion}]。程序将继续运行，但可能存在不稳定性。");
            }
            else
            {
                Plugin.Log.LogInfo($"[VersionCheck] 游戏环境校验通过: {_cachedGameVersion}");
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogDebug($"[VersionCheck] 校验过程异常: {e.Message}");
        }
    }

    /// <summary>
    /// 校验子模块 DLL 与插件的版本兼容性
    /// </summary>
    public static void ValidateModule(Assembly assembly)
    {
        string? asmName = assembly.FullName;
        if (asmName == null || ValidationCache.ContainsKey(asmName)) return;

        try
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch
            {
                return;
            }

            var configType = types.FirstOrDefault(t => t.Name == "AssetReduxConfig");
            if (configType == null) return;

            var field = configType.GetField("TargetVersion",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (field == null) return;

            string? targetVersion = field.GetValue(null)?.ToString();
            // 校验：子模块 vs 插件版本 (PluginInfo.PluginVersion)
            string currentPluginVersion = AssetReduxInfo.PluginVersion;

            if (!string.Equals(targetVersion, currentPluginVersion, StringComparison.OrdinalIgnoreCase))
            {
                Plugin.Log.LogWarning(
                    $"[VersionCheck] 模块 [{assembly.GetName().Name}] 针对版本 [{targetVersion}] 开发，当前插件版本为 [{currentPluginVersion}]。如果运行异常，请联系模块作者更新。");
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogDebug($"[VersionCheck] 模块 {assembly.GetName().Name} 校验异常: {e.Message}");
        }
        finally
        {
            ValidationCache[asmName] = true;
        }
    }

    public static void ClearCache()
    {
        ValidationCache.Clear();
        _cachedGameVersion = null;
    }
}