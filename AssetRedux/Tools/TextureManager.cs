using AssetRedux.Services;
using UnityEngine;

namespace AssetRedux.Tools;

/// <summary>
    /// 纹理管理中心：负责 3D 贴图与材质纹理的映射与加载
    /// </summary>
    public static class TextureManager
    {
        // 映射表：Key = 原始贴图名称, Value = 自定义图片绝对路径
        private static readonly Dictionary<string, string> PathMap = new(StringComparer.OrdinalIgnoreCase);

        // 运行时实例缓存：Key = 物理文件路径, Value = 已加载的 Texture2D 对象
        private static readonly Dictionary<string, Texture2D> InstanceCache = new();

        /// <summary>
        /// 注册纹理路径（由 ModuleRegistry 调用）
        /// </summary>
        /// <param name="originalName">游戏原始纹理名</param>
        /// <param name="absolutePath">自定义文件路径</param>
        public static void RegisterTexture(string originalName, string absolutePath)
        {
            if (string.IsNullOrEmpty(originalName) || string.IsNullOrEmpty(absolutePath)) return;

            // 高优先级模块会通过覆盖此 Dictionary 来胜出
            PathMap[originalName] = absolutePath;
        }

        /// <summary>
        /// 尝试获取自定义纹理（由 MaterialPatch 调用）
        /// </summary>
        /// <param name="originalName">原始纹理名</param>
        /// <param name="texture">输出的纹理实例</param>
        /// <returns>是否成功获取</returns>
        public static bool TryGetTexture(string originalName, out Texture2D? texture)
        {
            texture = null;

            // 1. 查找是否有路径映射
            if (!PathMap.TryGetValue(originalName, out string? fullPath))
            {
                return false;
            }

            // 2. 检查实例缓存，避免重复创建纹理对象
            if (InstanceCache.TryGetValue(fullPath, out var cachedTex) && cachedTex != null)
            {
                texture = cachedTex;
                return true;
            }

            // 3. 缓存未命中，调用底层服务从磁盘加载
            // TextureService 内部也带有一层文件级的缓存
            texture = TextureService.LoadTexture(fullPath);

            if (texture != null)
            {
                texture.name = originalName; // 保持名称与原始资源一致，方便某些依赖名称的逻辑
                InstanceCache[fullPath] = texture;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 清空所有映射和纹理实例（释放内存）
        /// </summary>
        public static void Clear()
        {
            PathMap.Clear();
            foreach (var tex in InstanceCache.Values)
            {
                if (tex != null) UnityEngine.Object.Destroy(tex);
            }
            InstanceCache.Clear();
            Plugin.Log.LogInfo("[TextureManager] 纹理管理中心已重置");
        }
    }