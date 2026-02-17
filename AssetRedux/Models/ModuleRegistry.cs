using System.Reflection;
using System.Text;
using AssetRedux.Tools;

namespace AssetRedux.Models;

public static class ModuleRegistry
{
    private static readonly List<BaseResourceModule> Modules = new();
    private static readonly HashSet<string> ProcessedAssemblies = new();
    
    // 预加载 JSON 片段缓存，避免运行时 IO
    private static readonly Dictionary<string, List<string>> JsonFragmentCache = new();

    public static IReadOnlyList<BaseResourceModule> ActiveModules => Modules;

    public static void RegisterModule(BaseResourceModule? module, Assembly sourceAssembly)
    {
        if (module == null) return;

        string? asmName = sourceAssembly.FullName;
        if (asmName != null && !ProcessedAssemblies.Add(asmName))
        {
            Plugin.Log.LogWarning($"[Registry] 跳过重复注册: {sourceAssembly.GetName().Name}");
            return;
        }

        // 预处理：物理路径补全、文件预读入内存
        PreprocessModule(module, sourceAssembly);

        // 排序：基于 Priority 实现覆盖逻辑
        Modules.Add(module);
        Modules.Sort((a, b) => b.Priority.CompareTo(a.Priority));

        // 分发至各个管理器
        SyncToManagers(module);
        
        Plugin.Log.LogInfo($"[Registry] 模块 '{module.ModuleName}' 注册成功.");
    }

    private static void PreprocessModule(BaseResourceModule module, Assembly sourceAssembly)
    {
        // --- 资源路径补全 ---
        foreach (var key in module.Sprites.Keys.ToList())
            module.Sprites[key] = CommonTool.GetAbsolutePath(module.Sprites[key], sourceAssembly);

        foreach (var key in module.Textures.Keys.ToList())
            module.Textures[key] = CommonTool.GetAbsolutePath(module.Textures[key], sourceAssembly);

        // --- 蓝图扫描优化  ---
        const string bpConfigName = "build_blueprint_bin.sav";
        const string bpImageName = "build_blueprint_png.sav";

        foreach (var folder in module.BlueprintFolders)
        {
            string absPath = CommonTool.GetAbsolutePath(folder, sourceAssembly);
            if (!Directory.Exists(absPath))
            {
                Plugin.Log.LogWarning($"[Registry] 蓝图目录不存在: {absPath}");
                continue;
            }

            // 生成全局唯一 GUID: [Mod名称]_[文件夹名]
            // 这样即使两个 Mod 都有 "House" 文件夹，也会区分成 "ModA_House" 和 "ModB_House"
            string folderName = Path.GetFileName(absPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string uniqueGuid = $"{module.ModuleName}_{folderName}";
    
            // 加载核心配置
            string configPath = Path.Combine(absPath, bpConfigName);
            if (File.Exists(configPath))
            {
                try
                {
                    // 存入 LoadedBlueprints 缓存
                    module.LoadedBlueprints[uniqueGuid] = File.ReadAllText(configPath);
            
                    // 加载预览图
                    string imagePath = Path.Combine(absPath, bpImageName);
                    if (File.Exists(imagePath))
                    {
                        // 注册到 SpriteManager，Key 同样使用 uniqueGuid 保证匹配
                        Tools.SpriteManager.RegisterSprite($"Blueprint_{uniqueGuid}", imagePath);
                    }
            
                    Plugin.Log.LogDebug($"[Registry] 成功加载蓝图: {uniqueGuid}");
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"[Registry] 蓝图读取失败 {uniqueGuid}: {e.Message}");
                }
            }
            else
            {
                Plugin.Log.LogWarning($"[Registry] 文件夹 {folderName} 缺少核心文件 {bpConfigName}");
            }
        }

        // --- JSON 文件夹预加载与处理器注册 ---
        foreach (var kvp in module.JsonFolderProcessors)
        {
            string resName = kvp.Key;
            string absPath = CommonTool.GetAbsolutePath(kvp.Value, sourceAssembly);

            if (Directory.Exists(absPath))
            {
                var files = Directory.GetFiles(absPath, "*.json");
                Array.Sort(files);

                if (!JsonFragmentCache.TryGetValue(resName, out var fragments))
                {
                    fragments = new List<string>();
                    JsonFragmentCache[resName] = fragments;
                }

                foreach (var file in files)
                {
                    try
                    {
                        string content = File.ReadAllText(file).Trim();
                        // 预剥离 [] 减少运行时计算
                        if (content.StartsWith("[") && content.EndsWith("]"))
                            content = content.Substring(1, content.Length - 2).Trim();
                        
                        if (!string.IsNullOrEmpty(content)) fragments.Add(content);
                    }
                    catch (Exception e) { Plugin.Log.LogError($"Preload JSON Error: {file} - {e.Message}"); }
                }

                // 注入 FastMerge 处理器
                module.TextAssetProcessors[resName] = (original) => FastJsonMerge(original, resName);
            }
        }
    }

    /// <summary>
    /// 高性能 JSON 合并：基于预分配 StringBuilder 与内存指针探测
    /// </summary>
    private static string FastJsonMerge(string originalJson, string resourceName)
    {
        if (string.IsNullOrWhiteSpace(originalJson) || !JsonFragmentCache.TryGetValue(resourceName, out var fragments))
            return originalJson;

        int lastBracket = originalJson.LastIndexOf(']');
        if (lastBracket == -1) return originalJson;

        // 计算所需总容量，避免 StringBuilder 扩容带来的内存拷贝
        int extraLen = fragments.Sum(f => f.Length + 1) + 2; 
        var sb = new StringBuilder(originalJson.Length + extraLen);

        // 写入原 JSON 前半部分 (直接内存拷贝)
        sb.Append(originalJson, 0, lastBracket);

        // 智能探测是否为空数组
        bool isOriginalEmpty = true;
        for (int i = lastBracket - 1; i >= 0; i--)
        {
            char c = originalJson[i];
            if (char.IsWhiteSpace(c)) continue;
            if (c != '[') isOriginalEmpty = false;
            break;
        }

        if (!isOriginalEmpty) sb.Append(',');

        // 快速拼接预存片段
        for (int i = 0; i < fragments.Count; i++)
        {
            sb.Append(fragments[i]);
            if (i < fragments.Count - 1) sb.Append(',');
        }

        sb.Append(']');
        return sb.ToString();
    }

    private static void SyncToManagers(BaseResourceModule module)
    {
        foreach (var kvp in module.Sprites) Tools.SpriteManager.RegisterSprite(kvp.Key, kvp.Value);
        foreach (var kvp in module.Textures) TextureManager.RegisterTexture(kvp.Key, kvp.Value);
        foreach (var kvp in module.TextAssetProcessors) TextAssetManager.RegisterProcessor(kvp.Key, kvp.Value);
    }

    public static void Clear()
    {
        Modules.Clear();
        ProcessedAssemblies.Clear();
        JsonFragmentCache.Clear();
        Tools.SpriteManager.Clear();
        TextureManager.Clear();
        TextAssetManager.Clear();
    }
}