namespace AssetRedux.Models;

/// <summary>
/// 所有资源模块的基类。子插件通过继承此类并重写属性来注册资源。
/// </summary>
public abstract class BaseResourceModule
{
    public virtual string ModuleName => "UnknownModule";
    public virtual int Priority => 0;

    /// <summary>
    /// 基础资源映射
    /// </summary>
    public virtual Dictionary<string, string> Sprites => new();

    public virtual Dictionary<string, string> Textures => new();
    public virtual Dictionary<string, Func<string, string>> TextAssetProcessors => new();

    /// <summary>
    /// 蓝图文件夹列表。
    /// 每一项应该是相对于 Mod 根目录的路径，例如："Blueprints/Room"
    /// 框架会自动寻找该目录下的 .sav/.json 文件以及预览图
    /// </summary>
    public virtual List<string> BlueprintFolders => new();

    // 由框架扫描后填充，供 Patch 使用
    // Key: GUID, Value: JSON 内容
    internal Dictionary<string, string> LoadedBlueprints { get; } = new();
    
    /// <summary>
    /// JSON 文件夹合并映射表。
    /// Key: 游戏资源名 (如 "build")
    /// Value: 存放多个扩展 JSON 的文件夹相对路径 (如 "Mod_Data/build")
    /// </summary>
    public virtual Dictionary<string, string> JsonFolderProcessors => new();

    public virtual string Description => string.Empty;
    public virtual bool IsLocked => false;
}