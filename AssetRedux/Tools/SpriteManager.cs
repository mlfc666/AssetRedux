using AssetRedux.Services;
using UnityEngine;

namespace AssetRedux.Tools;

/// <summary>
    /// Sprite 管理中心：负责将资源名称映射为实际的 Sprite 对象
    /// </summary>
    public static class SpriteManager
    {
        // 映射表：Key = 原始资源名, Value = 物理文件绝对路径
        private static readonly Dictionary<string, string> PathMap = new(StringComparer.OrdinalIgnoreCase);

        // 运行时缓存：Key = 物理文件绝对路径, Value = 已生成的 Sprite 对象
        // 这样可以确保同一个物理文件在内存中只存在一个 Sprite 实例
        private static readonly Dictionary<string, Sprite> SpriteCache = new();

        /// <summary>
        /// 注册资源路径（由 ModuleRegistry 调用）
        /// </summary>
        /// <param name="originalName">游戏内原始 Sprite 的名称</param>
        /// <param name="absolutePath">自定义文件的绝对路径</param>
        public static void RegisterSprite(string originalName, string absolutePath)
        {
            if (string.IsNullOrEmpty(originalName) || string.IsNullOrEmpty(absolutePath)) return;

            // 如果已有记录，新的会覆盖旧的（实现模块优先级）
            PathMap[originalName] = absolutePath;
        }

        /// <summary>
        /// 尝试获取自定义 Sprite（由 Patch 调用）
        /// </summary>
        /// <param name="originalName">原始资源名</param>
        /// <param name="sprite">输出的自定义 Sprite</param>
        /// <returns>是否成功匹配并加载</returns>
        public static bool TryGetSprite(string originalName, out Sprite? sprite)
        {
            sprite = null;

            // 1. 查找路径映射表
            if (!PathMap.TryGetValue(originalName, out string? fullPath))
            {
                return false;
            }

            // 2. 检查 Sprite 缓存
            if (SpriteCache.TryGetValue(fullPath, out var cachedSprite) && cachedSprite != null)
            {
                sprite = cachedSprite;
                return true;
            }

            // 3. 缓存未命中，调用 TextureService 加载纹理
            Texture2D tex = TextureService.LoadTexture(fullPath);
            if (tex != null)
            {
                // 4. 将 Texture2D 包装成 Sprite
                // 设置 Pivot 为中心 (0.5, 0.5)，PixelsPerUnit 默认为 100
                sprite = Sprite.Create(
                    tex, 
                    new Rect(0, 0, tex.width, tex.height), 
                    new Vector2(0.5f, 0.5f), 
                    100.0f, 
                    0, 
                    SpriteMeshType.FullRect
                );
                
                sprite.name = originalName; // 保持名称一致
                SpriteCache[fullPath] = sprite; // 存入缓存
                return true;
            }

            return false;
        }

        /// <summary>
        /// 清理所有 Sprite 缓存及映射关系
        /// </summary>
        public static void Clear()
        {
            PathMap.Clear();
            foreach (var s in SpriteCache.Values)
            {
                if (s != null) UnityEngine.Object.Destroy(s);
            }
            SpriteCache.Clear();
            Plugin.Log.LogInfo("[SpriteManager] Sprite 缓存与映射表已清空");
        }
    }