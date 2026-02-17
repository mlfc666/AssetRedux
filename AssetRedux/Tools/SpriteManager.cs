using UnityEngine;
using AssetRedux.Services;
using Object = UnityEngine.Object;

namespace AssetRedux.Tools;

public static class SpriteManager
{
    private static readonly Dictionary<string, string?> PathMap = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Sprite> SpriteCache = new();

    public static void RegisterSprite(string name, string path) => PathMap[name] = path;

    public static void ApplySprite(string? originalName, Action<Sprite> applyAction)
    {
        if (string.IsNullOrEmpty(originalName)) return;

        if (TryGetSprite(originalName, out var customSprite) && customSprite != null)
        {
            applyAction(customSprite);
        }
    }

    private static bool TryGetSprite(string name, out Sprite? sprite)
    {
        sprite = null;
        if (!PathMap.TryGetValue(name, out string? fullPath)) return false;
        if (fullPath != null && SpriteCache.TryGetValue(fullPath, out var cached) && cached != null)
        {
            sprite = cached;
            return true;
        }

        if (fullPath != null)
        {
            Texture2D? tex = TextureService.LoadTexture(fullPath);
            if (tex != null)
            {
                sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                sprite.name = name;
                SpriteCache[fullPath] = sprite;
                return true;
            }
        }

        return false;
    }

    public static void Clear()
    {
        PathMap.Clear();
        foreach (var s in SpriteCache.Values)
        {
            if (s != null)
            {
                if (s.texture != null) Object.Destroy(s.texture); // 销毁底层纹理
                Object.Destroy(s); // 销毁 Sprite 对象
            }
        }

        SpriteCache.Clear();
    }
}