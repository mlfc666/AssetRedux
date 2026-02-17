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
/// AssetRedux 核心控制器：负责模块扫描、生命周期管理与场景资源重刷
/// </summary>
public class AssetReduxController(IntPtr ptr) : MonoBehaviour(ptr)
{
    private static readonly int MainTex = Shader.PropertyToID("_MainTex");
    
    // 使用 Action 委托处理场景加载事件
    private Action<Scene, LoadSceneMode>? _sceneLoadedDelegate;

    [HideFromIl2Cpp]
    public void Awake()
    {
        DontDestroyOnLoad(gameObject);
        
        // 初始化委托
        _sceneLoadedDelegate = OnSceneLoaded;

        // 1. 扫描并加载所有子模块
        RefreshModules();
    }

    [HideFromIl2Cpp]
    public void Start()
    {
        Plugin.Log.LogInfo("AssetRedux 核心控制器启动成功！");
        
        if (_sceneLoadedDelegate != null)
        {
            SceneManager.sceneLoaded += _sceneLoadedDelegate;
        }
        
        // 首次启动刷新当前活跃对象
        RefreshActiveObjects();
    }

    [HideFromIl2Cpp]
    public void OnDestroy()
    {
        if (_sceneLoadedDelegate != null)
        {
            SceneManager.sceneLoaded -= _sceneLoadedDelegate;
        }
    }

    [HideFromIl2Cpp]
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Plugin.Log.LogInfo($"场景 [{scene.name}] 加载完成，执行资源重定向...");
        RefreshActiveObjects();
    }

    /// <summary>
    /// 全域扫描已加载的 DLL 并提取资源模块
    /// </summary>
    [HideFromIl2Cpp]
    public void RefreshModules()
    {
        Plugin.Log.LogInfo("开始扫描程序集资源模块...");
        
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (var assembly in assemblies)
        {
            // 1. 排除系统程序集与已处理的程序集
            if (IsSystemAssembly(assembly) || ModuleRegistry.IsAssemblyProcessed(assembly)) 
                continue;

            // 2. 版本兼容性检查 (调用专用的 VersionValidator)
            if (!VersionValidator.IsCompatible(assembly))
            {
                continue;
            }

            // 3. 提取并注册模块
            RegisterModulesFromAssembly(assembly);
        }
    }

    [HideFromIl2Cpp]
    private void RegisterModulesFromAssembly(Assembly assembly)
    {
        try
        {
            // 查找所有继承自 BaseResourceModule 的非抽象类
            var types = assembly.GetTypes().Where(t => 
                typeof(BaseResourceModule).IsAssignableFrom(t) && t is { IsInterface: false, IsAbstract: false });

            foreach (var type in types)
            {
                if (Activator.CreateInstance(type) is BaseResourceModule module)
                {
                    // 使用统一的 Registry 进行注册和资源分发
                    ModuleRegistry.RegisterModule(module, assembly);
                }
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogDebug($"跳过程序集 {assembly.GetName().Name} 的模块解析: {e.Message}");
        }
    }

    /// <summary>
    /// 强制刷新当前场景中所有支持重定向的组件
    /// </summary>
    [HideFromIl2Cpp]
    public void RefreshActiveObjects()
    {
        // 1. 刷新 UI Image
        foreach (var img in Resources.FindObjectsOfTypeAll<Image>())
        {
            if (img == null || img.sprite == null) continue;
            if (Tools.SpriteManager.TryGetSprite(img.sprite.name, out var customSprite) && customSprite != null)
            {
                if (img.sprite.GetInstanceID() != customSprite.GetInstanceID())
                    img.sprite = customSprite;
            }
        }

        // 2. 刷新 SpriteRenderer (2D 世界物体)
        foreach (var sr in Resources.FindObjectsOfTypeAll<SpriteRenderer>())
        {
            if (sr == null || sr.sprite == null) continue;
            if (Tools.SpriteManager.TryGetSprite(sr.sprite.name, out var customSprite) && customSprite != null)
            {
                if (sr.sprite.GetInstanceID() != customSprite.GetInstanceID())
                    sr.sprite = customSprite;
            }
        }

        // 3. 刷新材质贴图 (3D 模型)
        foreach (var renderer in Resources.FindObjectsOfTypeAll<Renderer>())
        {
            if (renderer == null || renderer.sharedMaterial == null) continue;
            
            Material mat = renderer.sharedMaterial;
            if (!mat.HasProperty(MainTex)) continue;

            var oldTex = mat.mainTexture;
            if (oldTex != null && TextureManager.TryGetTexture(oldTex.name, out var customTex) && customTex != null)
            {
                if (oldTex.GetInstanceID() != customTex.GetInstanceID())
                    mat.mainTexture = customTex;
            }
        }
    }

    [HideFromIl2Cpp]
    private bool IsSystemAssembly(Assembly assembly)
    {
        // 修改变量名避免隐藏 'name' 属性
        string? asmFullName = assembly.FullName?.ToLower();
        return asmFullName != null && (asmFullName.StartsWith("system") || 
                                       asmFullName.StartsWith("microsoft") || 
                                       asmFullName.StartsWith("unityengine") || 
                                       asmFullName.StartsWith("mscorlib") ||
                                       asmFullName.StartsWith("netstandard") ||
                                       asmFullName.StartsWith("interop") ||
                                       asmFullName.StartsWith("beminex")); 
    }
}