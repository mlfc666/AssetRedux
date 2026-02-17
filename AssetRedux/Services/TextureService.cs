using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace AssetRedux.Services;

public static class TextureService
{
    /// <summary>
    /// 同步加载：仅负责从磁盘创建 Texture2D 对象，不进行缓存。
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
            Plugin.Log.LogError($"[TextureService] 磁盘同步读取异常: {fullPath} - {e.Message}");
            if (texture != null) Object.Destroy(texture);
        }

        return null;
    }

    /// <summary>
    /// 异步加载纹理：通过协程和 UnityWebRequest 避免 IO 阻塞主线程
    /// </summary>
    /// <param name="fullPath">物理路径</param>
    /// <param name="callback">加载完成后的回调，返回 Texture2D 或 null</param>
    public static IEnumerator LoadTextureAsync(string fullPath, Action<Texture2D?>? callback)
    {
        if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
        {
            callback?.Invoke(null);
            yield break;
        }

        // 使用 file:// 协议进行本地异步读取
        string uri = "file://" + Path.GetFullPath(fullPath);
        UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(uri);
        
        // 开启非阻塞请求
        yield return uwr.SendWebRequest();

        if (uwr.result != UnityWebRequest.Result.Success)
        {
            Plugin.Log.LogError($"[TextureService] 异步加载失败: {uwr.error} | Path: {fullPath}");
            callback?.Invoke(null);
        }
        else
        {
            // 获取下载后的纹理（DownloadHandlerTexture 会在底层自动处理解码）
            Texture2D tex = DownloadHandlerTexture.GetContent(uwr);
            if (tex != null)
            {
                tex.name = Path.GetFileNameWithoutExtension(fullPath);
                callback?.Invoke(tex);
            }
            else
            {
                callback?.Invoke(null);
            }
        }
    }
}