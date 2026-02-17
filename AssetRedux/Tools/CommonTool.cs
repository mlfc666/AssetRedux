using System.Reflection;

namespace AssetRedux.Tools;

public static class CommonTool
{
    private static string? MainPluginDir => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

    /// <summary>
    /// 将相对路径转换为绝对路径，并增加前置存在性校验
    /// </summary>
    public static string GetAbsolutePath(string relativePath, Assembly? sourceAssembly = null)
    {
        if (string.IsNullOrEmpty(relativePath)) return string.Empty;
        if (Path.IsPathRooted(relativePath)) return relativePath;

        string? sourceDir = sourceAssembly != null && !string.IsNullOrEmpty(sourceAssembly.Location)
            ? Path.GetDirectoryName(sourceAssembly.Location)
            : MainPluginDir;

        string finalBase = sourceDir ?? AppContext.BaseDirectory;
        string fullPath = Path.Combine(finalBase, relativePath);

        // --- 新增：防御性路径校验 ---
        try
        {
            // 检查文件或目录是否存在
            if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
            {
                string modName = sourceAssembly?.GetName().Name ?? "Unknown Mod";
                Plugin.Log.LogWarning($"[PathCheck] 模块 '{modName}' 注册的资源路径不存在: {relativePath}");
                Plugin.Log.LogDebug($"[PathCheck] 尝试定位的完整路径为: {fullPath}");
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogDebug($"[PathCheck] 路径格式无效: {fullPath}, Error: {e.Message}");
        }

        return fullPath;
    }
}