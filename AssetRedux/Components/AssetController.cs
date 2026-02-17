using System.Collections;
using System.Reflection;
using AssetRedux.Models;
using AssetRedux.Services;
using AssetRedux.Tools;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using BepInEx.Unity.IL2CPP.Utils;

namespace AssetRedux.Components;

/// <summary>
/// AssetRedux 核心控制器：负责模块扫描、生命周期管理与全域资源防抖重刷
/// </summary>
public class AssetReduxController(IntPtr ptr) : MonoBehaviour(ptr)
{
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

    // 委托引用，防止被 GC 或 IL2CPP 卸载
    private Action<Scene, LoadSceneMode>? _sceneLoadedDelegate;

    // 性能与安全锁
    private bool _isRefreshing;
    private const float RefreshDebounceTime = 0.5f; // 防抖延迟时间（秒）

    [HideFromIl2Cpp]
    public void Awake()
    {
        // 确保控制器跨场景不销毁
        DontDestroyOnLoad(gameObject);

        _sceneLoadedDelegate = OnSceneLoaded;

        // 在加载模块前先执行游戏版本校验
        VersionValidator.CheckGameVersion();

        // 初始化扫描并加载所有子模块
        RefreshModules();
    }

    [HideFromIl2Cpp]
    public void Start()
    {
        Plugin.Log.LogInfo("AssetRedux 核心控制器已启动并准备就绪。");

        if (_sceneLoadedDelegate != null)
        {
            SceneManager.sceneLoaded += _sceneLoadedDelegate;
        }

        // 启动时首次触发刷新
        RequestRefresh();
    }

    [HideFromIl2Cpp]
    public void OnDestroy()
    {
        ModuleRegistry.Clear();

        // 清除版本校验缓存
        VersionValidator.ClearCache();

        if (_sceneLoadedDelegate != null)
        {
            SceneManager.sceneLoaded -= _sceneLoadedDelegate;
        }
    }

    [HideFromIl2Cpp]
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Plugin.Log.LogInfo($"[AssetRedux] 检测到场景 [{scene.name}] 加载，准备重定向资源...");
        RequestRefresh();
    }

    /// <summary>
    /// 公开的刷新请求接口。
    /// 采用防抖机制：如果在短时间内连续收到多个请求，只会执行最后一次。
    /// </summary>
    [HideFromIl2Cpp]
    public void RequestRefresh()
    {
        // 取消之前还未执行的刷新任务
        CancelInvoke(nameof(ExecuteRefreshInternal));
        // 延迟执行，防止在场景加载瞬间或多个 Mod 同时加载时产生剧烈卡顿
        Invoke(nameof(ExecuteRefreshInternal), RefreshDebounceTime);
    }

    /// <summary>
    /// 启动异步刷新协程
    /// </summary>
    [HideFromIl2Cpp]
    private void ExecuteRefreshInternal()
    {
        if (_isRefreshing) return;
        this.StartCoroutine(RefreshActiveObjectsCoroutine());
    }

    /// <summary>
    /// 分帧全域扫描，支持异步资源加载，确保 FPS 稳定
    /// </summary>
    [HideFromIl2Cpp]
    private IEnumerator RefreshActiveObjectsCoroutine()
    {
        _isRefreshing = true;
        Plugin.Log.LogInfo("[AssetRedux] 正在执行全域资源异步热刷新...");

        // 获取内存中所有 Component
        var allComponents = Resources.FindObjectsOfTypeAll<Component>();
        int count = 0;

        foreach (var comp in allComponents)
        {
            if (comp == null) continue;

            // 每扫描 500 个组件让出主线程一帧，防止游戏卡死
            count++;
            if (count % 500 == 0) yield return null;

            // 处理 UI Image
            if (comp.TryCast<Image>() is { } img && img.sprite != null)
            {
                string spriteName = img.sprite.name;
                Tools.SpriteManager.GetSpriteAsync(this, spriteName, (newSprite) =>
                {
                    // 回调检查：确保组件在异步加载期间没被销毁
                    if (newSprite != null && img != null && img.sprite != null)
                    {
                        if (img.sprite.GetInstanceID() != newSprite.GetInstanceID())
                            img.sprite = newSprite;
                    }
                });
                continue;
            }

            // 处理 2D SpriteRenderer
            if (comp.TryCast<SpriteRenderer>() is { } sr && sr.sprite != null)
            {
                string spriteName = sr.sprite.name;
                Tools.SpriteManager.GetSpriteAsync(this, spriteName, (newSprite) =>
                {
                    if (newSprite != null && sr != null && sr.sprite != null)
                    {
                        if (sr.sprite.GetInstanceID() != newSprite.GetInstanceID())
                            sr.sprite = newSprite;
                    }
                });
                continue;
            }

            // 处理 3D Renderer 材质贴图
            if (comp.TryCast<Renderer>() is { } ren && ren.sharedMaterial != null)
            {
                Material mat = ren.sharedMaterial;
                if (mat.HasProperty(MainTexId) && mat.mainTexture != null)
                {
                    string texName = mat.mainTexture.name;
                    TextureManager.GetTextureAsync(this, texName, (customTex) =>
                    {
                        if (customTex != null && mat != null && mat.mainTexture != null)
                        {
                            if (mat.mainTexture.GetInstanceID() != customTex.GetInstanceID())
                                mat.mainTexture = customTex;
                        }
                    });
                }
            }
        }

        _isRefreshing = false;
        Plugin.Log.LogInfo("[AssetRedux] 全域异步刷新指令已分发完毕。");
    }

    /// <summary>
    /// 扫描所有程序集以注册资源模块
    /// </summary>
    [HideFromIl2Cpp]
    public void RefreshModules()
    {
        // 清理旧数据和校验缓存
        ModuleRegistry.Clear();
        VersionValidator.ClearCache();
        Plugin.Log.LogInfo("开始扫描程序集资源模块...");
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (var assembly in assemblies)
        {
            if (IsSystemAssembly(assembly)) continue;
            // 执行子模块版本校验
            VersionValidator.ValidateModule(assembly);
            try
            {
                var types = assembly.GetTypes().Where(t =>
                    typeof(BaseResourceModule).IsAssignableFrom(t) && t is { IsInterface: false, IsAbstract: false });

                foreach (var type in types)
                {
                    if (Activator.CreateInstance(type) is BaseResourceModule module)
                    {
                        // 注册模块逻辑内部已包含 CommonTool 的路径预检
                        ModuleRegistry.RegisterModule(module, assembly);
                    }
                }
            }
            catch (Exception e)
            {
                // 仅在 Debug 模式记录，因为很多动态程序集不支持读取 Types
                Plugin.Log.LogWarning($"跳过程序集 {assembly.GetName().Name}: {e.Message}");
            }
        }
    }

    [HideFromIl2Cpp]
    private bool IsSystemAssembly(Assembly assembly)
    {
        string? asmName = assembly.FullName?.ToLower();
        return asmName != null && (asmName.StartsWith("system") ||
                                   asmName.StartsWith("microsoft") ||
                                   asmName.StartsWith("unityengine") ||
                                   asmName.StartsWith("mscorlib") ||
                                   asmName.StartsWith("netstandard") ||
                                   asmName.StartsWith("interop") ||
                                   asmName.StartsWith("bepinex") ||
                                   asmName.StartsWith("unhollower") ||
                                   asmName.StartsWith("harmony"));
    }
}