using System.Diagnostics.CodeAnalysis;
using AssetRedux.Tools;
using HarmonyLib;
using UnityEngine;

namespace AssetRedux.Patches;

[HarmonyPatch(typeof(TextAsset), nameof(TextAsset.text), MethodType.Getter)]
public static class TextAssetPatch
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public static void Postfix(TextAsset __instance, ref string __result)
    {
        // 如果原始结果为空，或者实例已失效，直接跳过
        if (__instance == null || string.IsNullOrEmpty(__result)) return;

        // 使用 InstanceID 配合缓存逻辑
        // 这样即使每帧访问 text 属性，也只会执行一次流水线运算
        if (TextAssetManager.TryGetCachedContent(__instance.GetInstanceID(), __instance.name, __result, out string modified))
        {
            __result = modified;
        }
    }
}