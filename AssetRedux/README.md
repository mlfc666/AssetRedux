# AssetRedux Framework 技术文档

AssetRedux 是一个专为 Unity IL2CPP 环境设计的资源重定向框架。该框架旨在为 《领地：种田与征战 (Territory: Farming and Warfare)》 提供一套高性能、异步且内存安全的方案，用于在运行时动态替换游戏内的纹理 (Texture2D)、精灵 (Sprite) 以及文本资源 (TextAsset)。

---

## 1. 框架核心组件与接口说明

### 1.1 AssetReduxController (核心控制器)
作为框架的驱动中心，负责场景监听、模块生命周期管理及全域资源刷新调度。

* **RequestRefresh()**
    * 作用：发起全域资源刷新请求。
    * 机制：内置防抖逻辑（Debounce），在场景加载或多个 Mod 同时请求时，确保短时间内仅执行一次扫描任务。
* **RefreshModules()**
    * 作用：扫描当前 AppDomain 中的程序集，自动发现并注册所有继承自 BaseResourceModule 的功能模块。
* **ExecuteRefreshInternal()**
    * 作用：刷新逻辑的入口包装，负责启动异步协程。
* **RefreshActiveObjectsCoroutine()**
    * 作用：通过协程分帧扫描场景中所有的 Image、SpriteRenderer 和 Renderer 组件。
    * 性能：每扫描固定数量的对象后执行 yield return null，防止主线程卡顿。

### 1.2 SpriteManager (精灵管理)
负责 2D 资源的映射、同步拦截及异步加载。

* **RegisterSprite(string name, string path)**
    * 作用：注册原始资源名称与本地文件路径的映射关系。
* **ApplySprite(string? originalName, Action<Sprite> applyAction)**
    * 作用：同步应用接口。主要用于 Harmony Patch 拦截点，在游戏原始逻辑执行时立即返回重定向后的资源。
* **GetSpriteAsync(MonoBehaviour runner, string name, Action<Sprite?> callback)**
    * 作用：异步加载接口。利用 UnityWebRequestTexture 在后台加载图片，避免大文件读取造成的 IO 阻塞，完成后通过回调返回结果。
* **Clear()**
    * 作用：清理资源。显式销毁已缓存的 Sprite 及其底层关联的 Texture2D，释放 Native 内存。

### 1.3 TextureManager (纹理管理)
专门负责 3D 材质贴图的替换逻辑。

* **RegisterTexture(string originalName, string absolutePath)**
    * 作用：建立 3D 贴图资源的路径映射。
* **TryGetTexture(string originalName, out Texture2D? texture)**
    * 作用：同步尝试获取纹理。优先检索缓存，若无缓存则执行磁盘同步加载。
* **GetTextureAsync(MonoBehaviour runner, string originalName, Action<Texture2D?>? callback)**
    * 作用：异步获取纹理接口。通过驱动器启动协程加载贴图，主要用于控制器进行全域批量刷新。
* **Clear()**
    * 作用：彻底释放 InstanceCache 中的所有 Texture2D 原生对象。

### 1.4 TextAssetManager (文本管理)
针对 JSON、XML、CSV 等文本数据提供基于流水线的修改逻辑。

* **RegisterProcessor(string assetName, Func<string, string> processor)**
    * 作用：注册文本处理器。支持针对同一文件进行链式修改（Pipeline）。
* **TryGetCachedContent(int instanceId, string name, string original, out string modified)**
    * 作用：带缓存的重定向接口。根据实例 ID 缓存处理后的结果，避免高频率调用下的字符串操作开销。
* **Clear()**
    * 作用：清理处理器链映射与处理后的文本缓存。

### 1.5 TextureService (基础加载服务)
封装底层的 Unity 原生 IO 调用。

* **LoadTexture(string fullPath)**
    * 作用：同步读取磁盘字节并执行 Texture2D.LoadImage。
* **LoadTextureAsync(string fullPath, Action<Texture2D?> callback)**
    * 作用：协程加载服务。利用 UnityWebRequest 获取纹理，确保在 IL2CPP 底层进行高效解码。

### 1.6 VersionValidator (版本校验)
* **CheckGameVersion()**
    * 作用：校验当前运行的游戏版本是否在框架支持范围内。
* **ValidateModule(Assembly assembly)**
    * 作用：针对各个子模块进行独立版本验证。

---

## 2. 补丁机制 (Patches)

框架通过 Harmony 注入以下关键点实现静默替换：

* **ImagePatch / SpriteRendererPatch**：拦截 Sprite 设置属性，实现 2D 资源的同步重定向。
* **MaterialPatch**：在材质实例化或属性更新时，介入 MainTexture 的分配。
* **TextAssetPatch**：拦截 TextAsset.text 的访问器，返回经由 TextAssetManager 处理后的文本。
* **BuildBlueprintPatch**：针对游戏特有的蓝图或配置文件加载流程进行深度挂载。

---

## 3. 开发集成示例

开发者通过继承 BaseResourceModule 来定义自己的资源包。

```csharp
public class SampleResourceModule : BaseResourceModule
{
public override string ModuleName => "SampleMod";
public override string TargetGameVersion => "1.0.0";

    public override void Preprocess()
    {
        // 注册 2D 资源
        SpriteManager.RegisterSprite("UI_Icon_Home", GetPath("home_custom.png"));

        // 注册 3D 贴图
        TextureManager.RegisterTexture("Character_Diffuse", GetPath("skin_v2.png"));

        // 修改游戏配置
        TextAssetManager.RegisterProcessor("GameSettings", (content) => {
            return content.Replace("EnableBlood: true", "EnableBlood: false");
        });
    }
}
```

---

## 4. 性能与注意事项

1.  **分帧逻辑**：全域刷新采用协程驱动，每扫描 500 个对象挂起一帧，确保在资源密集的关卡切换时不影响玩家体验。
2.  **IL2CPP 兼容性**：在调用 StartCoroutine 时，务必引用 BepInEx.Unity.IL2CPP.Utils 命名空间，以利用其提供的 IEnumerator 扩展包装器。
3.  **引用释放**：所有 Manager 的 Clear 方法应在插件卸载 (OnDestroy) 时被显式调用，否则会导致 Native 资源残留在显存中。
4.  **防抖频率**：RequestRefresh 的默认延迟为 0.5 秒，这能有效缓解某些 Mod 在初始化期间频繁触发刷新导致的性能抖动。
