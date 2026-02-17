using AssetRedux.Tools;
using HarmonyLib;
using UnityEngine;

namespace AssetRedux.Patches;

[HarmonyPatch(typeof(Material), nameof(Material.mainTexture), MethodType.Setter)]
public static class MaterialPatch
{
    [HarmonyPrefix]
    public static void Prefix(ref Texture value)
    {
        if (value == null) return;

        // 尝试从 TextureManager 获取自定义纹理
        if (TextureManager.TryGetTexture(value.name, out Texture2D? customTex))
        {
            if (customTex != null && value.GetInstanceID() != customTex.GetInstanceID())
            {
                value = customTex;
            }
        }
    }
}