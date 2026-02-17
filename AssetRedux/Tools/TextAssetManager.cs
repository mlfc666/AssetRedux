namespace AssetRedux.Tools;

/// <summary>
    /// 文本资源管理器：负责维护文本处理流水线
    /// </summary>
    public static class TextAssetManager
    {
        // 处理器字典：Key = 资源名称, Value = 处理器函数列表
        private static readonly Dictionary<string, List<Func<string, string>>> ProcessorMap = new();

        /// <summary>
        /// 注册文本处理器（由 ModuleRegistry 调用）
        /// </summary>
        /// <param name="assetName">要拦截的 TextAsset 名称</param>
        /// <param name="processor">处理逻辑委托</param>
        public static void RegisterProcessor(string assetName, Func<string, string>? processor)
        {
            if (string.IsNullOrEmpty(assetName) || processor == null) return;

            if (!ProcessorMap.ContainsKey(assetName))
            {
                ProcessorMap[assetName] = new List<Func<string, string>>();
            }

            // 将处理器加入队列
            ProcessorMap[assetName].Add(processor);
        }

        /// <summary>
        /// 尝试执行流水线修改（由 TextAssetPatch 调用）
        /// </summary>
        /// <param name="assetName">资源名称</param>
        /// <param name="originalContent">原始文本内容</param>
        /// <param name="modifiedContent">输出修改后的内容</param>
        /// <returns>是否有处理器进行了处理</returns>
        public static bool TryGetModifiedContent(string assetName, string originalContent, out string modifiedContent)
        {
            modifiedContent = originalContent;

            if (!ProcessorMap.TryGetValue(assetName, out var processors) || processors.Count == 0)
            {
                return false;
            }

            // 核心逻辑：流水线处理
            // 原始字符串依次经过每一个处理器的加工
            string currentText = originalContent;
            foreach (var process in processors)
            {
                try
                {
                    currentText = process(currentText);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"[TextAssetManager] 处理器执行异常 [{assetName}]: {e.Message}");
                }
            }

            modifiedContent = currentText;
            return true;
        }

        /// <summary>
        /// 清空所有文本处理器
        /// </summary>
        public static void Clear()
        {
            ProcessorMap.Clear();
            Plugin.Log.LogInfo("[TextAssetManager] 文本处理器流水线已重置");
        }
    }