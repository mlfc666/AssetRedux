using UnityEngine;

namespace AssetRedux.Services;

/// <summary>
/// 纹理服务：负责从磁盘加载图片并转化为 Unity 纹理对象
/// </summary>
public static class TextureService
{
    // 缓存已经加载过的纹理，Key 为文件完整路径
    private static readonly Dictionary<string, Texture2D> TextureCache = new();

    /// <summary>
    /// 从指定路径加载纹理
    /// </summary>
    /// <param name="fullPath">磁盘上的完整路径</param>
    /// <returns>加载成功的 Texture2D，失败则返回 null</returns>
    public static Texture2D? LoadTexture(string fullPath)
    {
        if (string.IsNullOrEmpty(fullPath)) return null;

        // 1. 检查缓存
        if (TextureCache.TryGetValue(fullPath, out var cachedTex) && cachedTex != null)
        {
            return cachedTex;
        }

        // 2. 检查文件是否存在
        if (!File.Exists(fullPath))
        {
            Plugin.Log.LogWarning($"[TextureService] 文件不存在: {fullPath}");
            return null;
        }

        try
        {
            // 3. 读取字节并加载
            byte[] fileData = File.ReadAllBytes(fullPath);

            // 创建一个临时的 Texture (2x2 是为了 LoadImage 会自动根据数据调整大小)
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);

            // 使用 ImageConversion.LoadImage (IL2CPP 下通常映射到这个方法)
            if (texture.LoadImage(fileData))
            {
                // 设置资源名称，方便调试和 Patch 识别
                texture.name = Path.GetFileNameWithoutExtension(fullPath);

                // 4. 存入缓存
                TextureCache[fullPath] = texture;
                return texture;
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"[TextureService] 加载纹理异常: {fullPath} - {e.Message}");
        }

        return null;
    }

    /// <summary>
    /// 清理缓存，通常在切换场景或内存吃紧时调用
    /// </summary>
    public static void ClearCache()
    {
        foreach (var tex in TextureCache.Values)
        {
            if (tex != null)
            {
                UnityEngine.Object.Destroy(tex);
            }
        }

        TextureCache.Clear();
        Plugin.Log.LogInfo("[TextureService] 纹理缓存已清理");
    }
}