using System.Reflection;
using AssetRedux.Models;
using AssetRedux.Services;
using AssetRedux.Tools;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
    /// 内部执行包装：处理原子锁
    /// </summary>
    [HideFromIl2Cpp]
    private void ExecuteRefreshInternal()
    {
        if (_isRefreshing) return;

        try
        {
            _isRefreshing = true;
            RefreshActiveObjects();
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"[AssetRedux] 全域资源刷新期间发生异常: {e.Message}");
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    /// <summary>
    /// 核心逻辑：单次全域扫描，对所有活动对象进行资源重定向
    /// </summary>
    [HideFromIl2Cpp]
    private void RefreshActiveObjects()
    {
        Plugin.Log.LogInfo("[AssetRedux] 正在执行全域资源热刷新...");

        // 获取内存中所有 Component
        var allComponents = Resources.FindObjectsOfTypeAll<Component>();

        foreach (var comp in allComponents)
        {
            if (comp == null) continue;

            // 处理 UI Image
            if (comp.TryCast<Image>() is { } img && img.sprite != null)
            {
                Tools.SpriteManager.ApplySprite(img.sprite.name, (newSprite) =>
                {
                    if (img.sprite.GetInstanceID() != newSprite.GetInstanceID())
                        img.sprite = newSprite;
                });
                continue;
            }

            // 处理 2D SpriteRenderer
            if (comp.TryCast<SpriteRenderer>() is { } sr && sr.sprite != null)
            {
                Tools.SpriteManager.ApplySprite(sr.sprite.name, (newSprite) =>
                {
                    if (sr.sprite.GetInstanceID() != newSprite.GetInstanceID())
                        sr.sprite = newSprite;
                });
                continue;
            }

            // 处理 3D Renderer 材质贴图
            if (comp.TryCast<Renderer>() is { } ren && ren.sharedMaterial != null)
            {
                Material mat = ren.sharedMaterial;
                if (mat.HasProperty(MainTexId) && mat.mainTexture != null)
                {
                    var oldTex = mat.mainTexture;
                    if (TextureManager.TryGetTexture(oldTex.name, out var customTex) && customTex != null)
                    {
                        if (oldTex.GetInstanceID() != customTex.GetInstanceID())
                            mat.mainTexture = customTex;
                    }
                }
            }
        }

        Plugin.Log.LogInfo("[AssetRedux] 全域资源刷新完成。");
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