using System.Reflection;
using AssetRedux.Tools;

namespace AssetRedux.Models;

/// <summary>
/// 模块注册中心：负责管理所有已加载的模块元数据及冲突逻辑
/// </summary>
public static class ModuleRegistry
{
    // 存储所有已实例化的模块
    private static readonly List<BaseResourceModule> Modules = new();

    // 追踪已处理的程序集，防止重复注册
    private static readonly HashSet<string> ProcessedAssemblies = new();

    /// <summary>
    /// 获取当前所有已注册的模块（按优先级排序）
    /// </summary>
    public static IReadOnlyList<BaseResourceModule> ActiveModules => Modules;

    /// <summary>
    /// 检查程序集是否已被记录
    /// </summary>
    public static bool IsAssemblyProcessed(Assembly assembly) =>
        assembly.FullName != null && ProcessedAssemblies.Contains(assembly.FullName);

    /// <summary>
    /// 核心方法：将模块加入账本并分发资源路径
    /// </summary>
    public static void RegisterModule(BaseResourceModule? module, Assembly sourceAssembly)
    {
        if (module == null) return;

        // 1. 添加到内部清单
        Modules.Add(module);
        if (sourceAssembly.FullName != null) ProcessedAssemblies.Add(sourceAssembly.FullName);

        // 2. 重新排序：确保高优先级模块在列表前方
        Modules.Sort((a, b) => b.Priority.CompareTo(a.Priority));

        // 3. 将资源分发给对应的 Manager
        // 注意：由于 Manager 内部通常是 Dictionary 覆盖模式，
        // 这种“后注册优先”或“按顺序重新注册”保证了优先级的有效性。
        SyncModuleToManagers(module);

        // 1. 处理蓝图数据 (JSON)
        // 我们把原本模块里的相对路径，转换成绝对路径再存回去，方便 Patch 直接读取
        var blueprintKeys = new List<string>(module.Blueprints.Keys);
        foreach (var guid in blueprintKeys)
        {
            string relativePath = module.Blueprints[guid];
            module.Blueprints[guid] = CommonTool.GetAbsolutePath(relativePath, sourceAssembly);
        }

        // 2. 处理蓝图预览图 (Snapshot) -> 自动注册到 SpriteManager
        foreach (var kvp in module.BlueprintSnapshots)
        {
            string guid = kvp.Key;
            string spriteKey = guid.StartsWith("Blueprint_") ? guid : $"Blueprint_{guid}";
            string absPath = CommonTool.GetAbsolutePath(kvp.Value, sourceAssembly);

            // 直接塞进 SpriteManager，这样 RedirectSnapshotPatch 就能搜到了
            Tools.SpriteManager.RegisterSprite(spriteKey, absPath);
        }

        Plugin.Log.LogInfo(
            $"[Registry] 模块 '{module.ModuleName}' 注册成功 (来自: {sourceAssembly.GetName().Name}, 优先级: {module.Priority})");
    }

    /// <summary>
    /// 将单个模块的所有资源同步到全局管理器
    /// </summary>
    private static void SyncModuleToManagers(BaseResourceModule module)
    {
        // 同步 Sprite 路径
        foreach (var kvp in module.Sprites)
        {
            Tools.SpriteManager.RegisterSprite(kvp.Key, kvp.Value);
        }

        // 同步 Texture 路径
        foreach (var kvp in module.Textures)
        {
            TextureManager.RegisterTexture(kvp.Key, kvp.Value);
        }

        // 同步文本处理器
        foreach (var kvp in module.TextAssetProcessors)
        {
            TextAssetManager.RegisterProcessor(kvp.Key, kvp.Value);
        }
    }

    /// <summary>
    /// 清空所有注册信息（通常用于热重载或重启）
    /// </summary>
    public static void Clear()
    {
        Modules.Clear();
        ProcessedAssemblies.Clear();
        // 注意：这里可能还需要调用各个 Manager 的 Clear 方法来清空路径表
    }
}