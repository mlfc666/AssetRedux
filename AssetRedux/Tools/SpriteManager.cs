using UnityEngine;
using AssetRedux.Services;
using BepInEx.Unity.IL2CPP.Utils;
using Object = UnityEngine.Object;

namespace AssetRedux.Tools;

public static class SpriteManager
{
    // 映射表：Key = 原始资源名, Value = 物理绝对路径
    private static readonly Dictionary<string, string?> PathMap = new(StringComparer.OrdinalIgnoreCase);

    // 实例缓存：Key = 物理绝对路径, Value = 已创建的 Sprite
    private static readonly Dictionary<string, Sprite> SpriteCache = new();

    public static void RegisterSprite(string name, string path) => PathMap[name] = path;

    /// <summary>
    /// 同步应用 Sprite（用于补丁拦截，逻辑需要即时返回）
    /// </summary>
    public static void ApplySprite(string? originalName, Action<Sprite> applyAction)
    {
        if (string.IsNullOrEmpty(originalName)) return;

        if (TryGetSprite(originalName, out var customSprite) && customSprite != null)
        {
            applyAction(customSprite);
        }
    }

    /// <summary>
    /// 异步获取 Sprite（用于 AssetReduxController 全域刷新，分帧加载）。
    /// </summary>
    /// <remarks>
    /// 该方法优先检查内存缓存，若无缓存则根据注册的路径映射启动分帧异步 IO 加载。
    /// 旨在解决全域资源替换时磁盘 IO 导致的主线程卡顿。
    /// </remarks>
    /// <param name="runner">驱动协程的 MonoBehaviour 实例</param>
    /// <param name="name">原始资源的名称（查找 Key）</param>
    /// <param name="callback">加载完成后的回调函数。如果资源不存在或加载失败，返回 null</param>
    public static void GetSpriteAsync(MonoBehaviour runner, string name, Action<Sprite?>? callback)
    {
        // 检查缓存
        if (TryGetSprite(name, out var cached) && cached != null)
        {
            callback?.Invoke(cached);
            return;
        }

        // 检查路径映射
        if (!PathMap.TryGetValue(name, out string? fullPath) || string.IsNullOrEmpty(fullPath))
        {
            callback?.Invoke(null);
            return;
        }

        // 开启异步 IO 加载
        runner.StartCoroutine(TextureService.LoadTextureAsync(fullPath, (tex) =>
        {
            if (tex != null)
            {
                // 创建 Sprite
                var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                sprite.name = name;

                // 存入缓存（使用物理路径作为 Key 以防多对一映射）
                SpriteCache[fullPath] = sprite;
                callback?.Invoke(sprite);
            }
            else
            {
                callback?.Invoke(null);
            }
        }));
    }

    /// <summary>
    /// 同步尝试获取或加载（内部逻辑）
    /// </summary>
    private static bool TryGetSprite(string name, out Sprite? sprite)
    {
        sprite = null;
        if (!PathMap.TryGetValue(name, out string? fullPath) || string.IsNullOrEmpty(fullPath)) return false;

        // 检查缓存
        if (SpriteCache.TryGetValue(fullPath, out var cached) && cached != null)
        {
            sprite = cached;
            return true;
        }

        // 同步加载磁盘纹理
        Texture2D? tex = TextureService.LoadTexture(fullPath);
        if (tex != null)
        {
            sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            sprite.name = name;
            SpriteCache[fullPath] = sprite;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 彻底释放 Native 资源，防止 IL2CPP 内存泄漏
    /// </summary>
    public static void Clear()
    {
        PathMap.Clear();
        foreach (var s in SpriteCache.Values)
        {
            if (s != null)
            {
                // 销毁 Sprite 必须同时销毁它持有的 Texture，除非 Texture 被多个 Sprite 共享
                if (s.texture != null) Object.Destroy(s.texture);
                Object.Destroy(s);
            }
        }

        SpriteCache.Clear();
    }
}