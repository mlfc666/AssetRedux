using System.Reflection;
using AssetRedux.Tools;

namespace AssetRedux.Models;

public static class ModuleRegistry
{
    private static readonly List<BaseResourceModule> Modules = new();
    
    // 补全用途：用于 RegisterModule 入口处的重复检查
    private static readonly HashSet<string> ProcessedAssemblies = new();

    public static IReadOnlyList<BaseResourceModule> ActiveModules => Modules;

    /// <summary>
    /// 核心方法：将模块加入账本并分发资源路径
    /// </summary>
    public static void RegisterModule(BaseResourceModule? module, Assembly sourceAssembly)
    {
        if (module == null) return;

        // 【补全逻辑】检查程序集是否已处理，防止重复注册导致的优先级混乱或内存浪费
        string? asmName = sourceAssembly.FullName;
        if (asmName != null)
        {
            if (!ProcessedAssemblies.Add(asmName))
            {
                Plugin.Log.LogWarning($"[Registry] 跳过重复注册的程序集: {sourceAssembly.GetName().Name}");
                return;
            }
        }

        // 1. 预处理：路径转绝对 & 蓝图 JSON 读入内存
        PreprocessModule(module, sourceAssembly);

        Modules.Add(module);

        // 2. 优先级排序
        Modules.Sort((a, b) => b.Priority.CompareTo(a.Priority));

        // 3. 将资源分发给对应的资源管理器
        SyncToManagers(module);
        
        Plugin.Log.LogInfo($"[Registry] 模块 '{module.ModuleName}' 注册成功 (优先级: {module.Priority})");
    }

    private static void PreprocessModule(BaseResourceModule module, Assembly sourceAssembly)
    {
        foreach (var key in new List<string>(module.Sprites.Keys))
            module.Sprites[key] = CommonTool.GetAbsolutePath(module.Sprites[key], sourceAssembly);

        foreach (var key in new List<string>(module.Textures.Keys))
            module.Textures[key] = CommonTool.GetAbsolutePath(module.Textures[key], sourceAssembly);

        foreach (var key in new List<string>(module.Blueprints.Keys))
        {
            string absPath = CommonTool.GetAbsolutePath(module.Blueprints[key], sourceAssembly);
            if (File.Exists(absPath)) module.Blueprints[key] = File.ReadAllText(absPath);
        }
        
        foreach (var kvp in module.BlueprintSnapshots)
        {
            string spriteKey = kvp.Key.StartsWith("Blueprint_") ? kvp.Key : $"Blueprint_{kvp.Key}";
            string absPath = CommonTool.GetAbsolutePath(kvp.Value, sourceAssembly);
            Tools.SpriteManager.RegisterSprite(spriteKey, absPath);
        }
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
        Tools.SpriteManager.Clear();
        TextureManager.Clear();
        TextAssetManager.Clear();
    }
}