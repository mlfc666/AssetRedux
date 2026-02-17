namespace AssetRedux.Tools;

using System;
using System.IO;
using System.Reflection;

/// <summary>
/// 通用工具类：提供路径转换、日志格式化及基础验证功能
/// </summary>
public static class CommonTool
{
    /// <summary>
    /// 获取当前执行程序集（主插件）所在的文件夹路径
    /// </summary>
    private static string? MainPluginDir => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

    /// <summary>
    /// 将相对于插件目录的相对路径转换为绝对路径
    /// </summary>
    /// <param name="relativePath">相对路径，如 "Assets/hero.png"</param>
    /// <param name="sourceAssembly">提供该资源的程序集（用于定位子插件目录）</param>
    /// <returns>完整的磁盘绝对路径</returns>
    public static string GetAbsolutePath(string relativePath, Assembly? sourceAssembly = null)
    {
        if (Path.IsPathRooted(relativePath)) return relativePath;

        // 修复逻辑：优先用传入的程序集路径，如果没有，回退到主插件路径
        string? baseDir = sourceAssembly != null 
            ? Path.GetDirectoryName(sourceAssembly.Location) 
            : MainPluginDir; // 这里使用了你定义的静态属性

        // 极端保险处理：如果连 MainPluginDir 都是 null（这在某些嵌入式环境或内存加载中可能发生）
        return Path.Combine(baseDir ?? AppContext.BaseDirectory, relativePath);
    }

    /// <summary>
    /// 安全加载字节流，增加简单的校验
    /// </summary>
    public static byte[]? ReadFileBytes(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                Plugin.Log.LogWarning($"[CommonTool] 尝试读取不存在的文件: {path}");
                return null;
            }

            return File.ReadAllBytes(path);
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"[CommonTool] 读取文件失败: {path}, 错误: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 验证文件是否为支持的图片格式
    /// </summary>
    public static bool IsSupportedImage(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        string ext = Path.GetExtension(path).ToLower();
        return ext == ".png" || ext == ".jpg" || ext == ".jpeg";
    }

    /// <summary>
    /// 确保路径的分隔符符合当前系统（解决 Windows/Linux 路径斜杠不一致问题）
    /// </summary>
    public static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        return path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
    }
}