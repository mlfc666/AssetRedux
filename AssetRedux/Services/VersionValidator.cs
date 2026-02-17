using System.Reflection;

namespace AssetRedux.Services;

/// <summary>
/// 版本校验服务：负责检查第三方 DLL 是否与当前 AssetRedux 版本兼容
/// </summary>
public static class VersionValidator
{
    // 缓存校验结果：AssemblyFullName -> IsCompatible
    private static readonly Dictionary<string, bool> ValidationCache = new();

    /// <summary>
    /// 校验程序集是否兼容
    /// </summary>
    /// <param name="assembly">要检查的程序集</param>
    /// <returns>如果兼容或未定义版本则返回 true，版本不匹配返回 false</returns>
    public static bool IsCompatible(Assembly assembly)
    {
        string? asmName = assembly.FullName;

        // 检查缓存，避免重复反射
        if (asmName != null && ValidationCache.TryGetValue(asmName, out bool cachedResult))
        {
            return cachedResult;
        }

        bool isCompatible = PerformValidation(assembly);
        if (asmName != null) ValidationCache[asmName] = isCompatible;
        return isCompatible;
    }

    private static bool PerformValidation(Assembly assembly)
    {
        try
        {
            // 使用 Try-Catch 包裹 GetTypes，防止反射崩溃
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException)
            {
                // 如果类型加载失败，说明该 DLL 损坏或缺少依赖，通常不需要版本校验干预
                return true;
            }

            var configType = types.FirstOrDefault(t => t.Name == "AssetReduxConfig");

            if (configType == null) return true;

            var field = configType.GetField("TargetVersion",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

            if (field == null)
            {
                Plugin.Log.LogWarning($"[Validator] {assembly.GetName().Name} 缺少 TargetVersion 字段。");
                return true;
            }

            // 这里的 value 确实可能是 null，你的处理很棒
            string? targetVersion = field.GetValue(null)?.ToString();
            string currentVersion = PluginInfo.TargetVersion;

            // 字符串相等性检查（OrdinalIgnoreCase 可以处理大小写差异带来的误判）
            if (!string.Equals(targetVersion, currentVersion, StringComparison.OrdinalIgnoreCase))
            {
                Plugin.Log.LogError(
                    $"[Version Mismatch] {assembly.GetName().Name} (目标:{targetVersion}) 与核心版本({currentVersion})不符！");
                return false;
            }

            return true;
        }
        catch (Exception e)
        {
            Plugin.Log.LogDebug($"[Validator] {assembly.GetName().Name} 校验逻辑异常: {e.Message}");
            return true;
        }
    }

    /// <summary>
    /// 清除校验缓存
    /// </summary>
    public static void ClearCache()
    {
        ValidationCache.Clear();
    }
}