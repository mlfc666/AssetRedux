using AssetRedux.Services;
using BepInEx.Unity.IL2CPP.Utils;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AssetRedux.Tools;

/// <summary>
/// 纹理管理中心：负责 3D 贴图与材质纹理的映射、缓存及生命周期管理
/// </summary>
public static class TextureManager
{
    private static readonly Dictionary<string, string> PathMap = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Texture2D> InstanceCache = new();

    public static void RegisterTexture(string originalName, string absolutePath)
    {
        if (string.IsNullOrEmpty(originalName) || string.IsNullOrEmpty(absolutePath)) return;
        PathMap[originalName] = absolutePath;
    }

    /// <summary>
    /// 同步获取纹理（用于 Harmony Patch 拦截）
    /// </summary>
    public static bool TryGetTexture(string originalName, out Texture2D? texture)
    {
        texture = null;
        if (!PathMap.TryGetValue(originalName, out string? fullPath)) return false;

        if (InstanceCache.TryGetValue(fullPath, out var cachedTex) && cachedTex != null)
        {
            texture = cachedTex;
            return true;
        }

        texture = TextureService.LoadTexture(fullPath);
        if (texture != null)
        {
            texture.name = originalName;
            InstanceCache[fullPath] = texture;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 异步获取纹理（用于 AssetReduxController 批量扫描 3D Renderer）
    /// </summary>
    public static void GetTextureAsync(MonoBehaviour runner, string originalName, Action<Texture2D?>? callback)
    {
        if (TryGetTexture(originalName, out var cached) && cached != null)
        {
            callback?.Invoke(cached);
            return;
        }

        if (!PathMap.TryGetValue(originalName, out string? fullPath))
        {
            callback?.Invoke(null);
            return;
        }

        runner.StartCoroutine(TextureService.LoadTextureAsync(fullPath, (tex) =>
        {
            if (tex != null)
            {
                tex.name = originalName;
                InstanceCache[fullPath] = tex;
                callback?.Invoke(tex);
            }
            else
            {
                callback?.Invoke(null);
            }
        }));
    }

    public static void Clear()
    {
        PathMap.Clear();
        foreach (var tex in InstanceCache.Values)
        {
            if (tex != null) Object.Destroy(tex);
        }
        InstanceCache.Clear();
        Plugin.Log.LogInfo("[TextureManager] 纹理管理中心已完成内存清理。");
    }
}