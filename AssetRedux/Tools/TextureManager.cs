using AssetRedux.Services;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AssetRedux.Tools;

/// <summary>
/// 纹理管理中心：负责 3D 贴图与材质纹理的映射、缓存及生命周期管理
/// </summary>
public static class TextureManager
{
    // 映射表：Key = 原始贴图名称, Value = 自定义图片绝对路径
    private static readonly Dictionary<string, string> PathMap = new(StringComparer.OrdinalIgnoreCase);

    // 运行时实例缓存：Key = 物理文件路径, Value = 已加载的 Texture2D 对象
    // 生产环境说明：TextureService 不再持有缓存，此处为该纹理在内存中的唯一引用点
    private static readonly Dictionary<string, Texture2D> InstanceCache = new();

    /// <summary>
    /// 注册纹理路径（由 ModuleRegistry 调用）
    /// </summary>
    public static void RegisterTexture(string originalName, string absolutePath)
    {
        if (string.IsNullOrEmpty(originalName) || string.IsNullOrEmpty(absolutePath)) return;

        // 高优先级模块通过覆盖路径来实现资源替换
        PathMap[originalName] = absolutePath;
    }

    /// <summary>
    /// 尝试获取自定义纹理（由 MaterialPatch 或 Controller 调用）
    /// </summary>
    public static bool TryGetTexture(string originalName, out Texture2D? texture)
    {
        texture = null;

        // 1. 查找是否有路径映射
        if (!PathMap.TryGetValue(originalName, out string? fullPath))
        {
            return false;
        }

        // 2. 检查实例缓存
        if (InstanceCache.TryGetValue(fullPath, out var cachedTex) && cachedTex != null)
        {
            texture = cachedTex;
            return true;
        }

        // 3. 缓存未命中，从磁盘加载（TextureService 现在是无状态的工具类）
        texture = TextureService.LoadTexture(fullPath);

        if (texture != null)
        {
            // 保持名称一致，确保后续逻辑（如根据名称二次查找）不会中断
            texture.name = originalName; 
            InstanceCache[fullPath] = texture;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 彻底释放内存（由 ModuleRegistry.Clear 调用）
    /// </summary>
    public static void Clear()
    {
        PathMap.Clear();
        
        // 生产环境关键：显式销毁 Unity Native 对象
        foreach (var tex in InstanceCache.Values)
        {
            if (tex != null)
            {
                Object.Destroy(tex);
            }
        }
        
        InstanceCache.Clear();
        Plugin.Log.LogInfo("[TextureManager] 纹理管理中心已完成内存清理。");
    }
}