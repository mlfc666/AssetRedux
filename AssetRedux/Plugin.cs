using System.Diagnostics.CodeAnalysis;
using AssetRedux.Components;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AssetRedux;

[BepInPlugin(AssetReduxInfo.PluginGuid, AssetReduxInfo.PluginName, AssetReduxInfo.PluginVersion)]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public class Plugin : BasePlugin
{
    public new static ManualLogSource Log = null!;

    public override void Load()
    {
        // 强制控制台 UTF-8 (解决中文乱码)
        try
        {
            // 尝试设置编码
            Console.OutputEncoding = System.Text.Encoding.UTF8;
        }
        catch
        {
            /* 忽略任何控制台初始化错误 */
        }

        Log = base.Log;

        // 应用 Harmony 补丁
        // PatchAll 比较耗时，放在最后，确保前面关键组件已就绪
        new Harmony(AssetReduxInfo.PluginGuid).PatchAll();

        // 注册 IL2CPP 类型 
        ClassInjector.RegisterTypeInIl2Cpp<AssetReduxController>();

        // 创建控制器
        var go = new GameObject(AssetReduxInfo.ControllerName);
        Object.DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave; // 防止被游戏内的清理脚本误删

        // 挂载脚本
        go.AddComponent<AssetReduxController>();
    }
}

public static class AssetReduxInfo
{
    public const string PluginGuid = "moe.mlfc.assetredux";
    public const string PluginName = "MlfcAssetRedux";
    public const string PluginVersion = "1.0.20260218.18";
    public const string ControllerName = PluginName + "Controller";
    public const string TargetVersion = PluginVersion;
}