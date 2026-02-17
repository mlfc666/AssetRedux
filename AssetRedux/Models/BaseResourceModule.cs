namespace AssetRedux.Models;

/// <summary>
/// 所有资源模块的基类。子插件通过继承此类并重写属性来注册资源。
/// </summary>
public abstract class BaseResourceModule
{
    public virtual string ModuleName => "UnknownModule";
    public virtual int Priority => 0;

    /// <summary>
    /// 数据映射表。
    /// Key: 蓝图的 GUID (通常对应文件夹名)
    /// Value: 蓝图 JSON 文件的相对路径 (例如 "Blueprints/MyHouse.json")
    /// </summary>
    public virtual Dictionary<string, string> Sprites => new();

    public virtual Dictionary<string, string> Textures => new();
    public virtual Dictionary<string, Func<string, string>> TextAssetProcessors => new();
    public virtual Dictionary<string, string> Blueprints => new();

    /// <summary>
    /// 蓝图预览图映射表 (可选)。
    /// 这里的 Key 建议与 Blueprints 的 GUID 保持一致。
    /// 注意：如果此项留空，Patch 会尝试自动去 Sprites 字典中寻找 Key 为 "Blueprint_{GUID}" 的资源。
    /// </summary>
    public virtual Dictionary<string, string> BlueprintSnapshots => new();

    public virtual string Description => string.Empty;
    public virtual bool IsLocked => false;
}