using UnityEngine;
using Object = UnityEngine.Object;

namespace AssetRedux.Services;

public static class TextureService
{
    /// <summary>
    /// 仅负责从磁盘创建 Texture2D 对象，不进行缓存。
    /// 生命周期由调用方（Manager）管理。
    /// </summary>
    public static Texture2D? LoadTexture(string fullPath)
    {
        if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath)) return null;

        // 创建临时纹理对象
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
        try
        {
            byte[] fileData = File.ReadAllBytes(fullPath);
            if (texture.LoadImage(fileData))
            {
                texture.name = Path.GetFileNameWithoutExtension(fullPath);
                return texture;
            }

            // 加载失败必须销毁，否则 native 句柄会残留
            Object.Destroy(texture);
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"[TextureService] 磁盘读取异常: {fullPath} - {e.Message}");
            if (texture != null) Object.Destroy(texture);
        }

        return null;
    }
}