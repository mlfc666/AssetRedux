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
        if (__instance == null || string.IsNullOrEmpty(__result)) return;

        // 调用 TextAssetManager 的流水线处理器
        if (TextAssetManager.TryGetModifiedContent(__instance.name, __result, out string modified))
        {
            __result = modified;
        }
    }
}