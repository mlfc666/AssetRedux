namespace AssetRedux.Tools;

public static class TextAssetManager
{
    // 处理器字典
    private static readonly Dictionary<string, List<Func<string, string>>> ProcessorMap = new();
    
    // Key: TextAsset 的 InstanceID (int), Value: 修改后的字符串结果
    // 使用 InstanceID 比使用对象引用更安全，能避免 IL2CPP 下的引用存留问题
    private static readonly Dictionary<int, string> ResultCache = new();

    public static void RegisterProcessor(string assetName, Func<string, string>? processor)
    {
        if (string.IsNullOrEmpty(assetName) || processor == null) return;
        if (!ProcessorMap.ContainsKey(assetName)) ProcessorMap[assetName] = new();
        ProcessorMap[assetName].Add(processor);
        
        // 如果有新的处理器注册，必须清空缓存，确保下次读取时应用新逻辑
        ResultCache.Clear();
    }

    /// <summary>
    /// 带缓存检查的修改获取方法
    /// </summary>
    public static bool TryGetCachedContent(int instanceId, string assetName, string originalContent, out string modifiedContent)
    {
        // 优先从缓存获取
        if (ResultCache.TryGetValue(instanceId, out modifiedContent!))
        {
            return true;
        }

        // 缓存未命中，执行流水线处理
        if (TryGetModifiedContent(assetName, originalContent, out modifiedContent))
        {
            // 存入缓存
            ResultCache[instanceId] = modifiedContent;
            return true;
        }

        return false;
    }

    // 原始逻辑保持不变，作为内部处理流程
    private static bool TryGetModifiedContent(string assetName, string originalContent, out string modifiedContent)
    {
        modifiedContent = originalContent;
        if (!ProcessorMap.TryGetValue(assetName, out var processors) || processors.Count == 0) return false;

        string currentText = originalContent;
        foreach (var process in processors)
        {
            try { currentText = process(currentText); }
            catch (Exception e) { Plugin.Log.LogError($"[TextAssetManager] 处理器异常 [{assetName}]: {e.Message}"); }
        }

        modifiedContent = currentText;
        return true;
    }

    public static void Clear()
    {
        ProcessorMap.Clear();
        ResultCache.Clear(); //必须清理缓存
        Plugin.Log.LogInfo("[TextAssetManager] 文本处理器与缓存已重置");
    }
}