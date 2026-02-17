using HarmonyLib;
using UnityEngine;

namespace AssetRedux.Patches;

[HarmonyPatch(typeof(SpriteRenderer), nameof(SpriteRenderer.sprite), MethodType.Setter)]
public static class SpriteRendererPatch
{
    [HarmonyPrefix]
    public static void Prefix(ref Sprite? value)
    {
        if (value == null) return;

        // 核心修复：同理，使用局部变量中转
        var tempValue = value;
        Sprite? result = null;

        Tools.SpriteManager.ApplySprite(tempValue.name, (newSprite) =>
        {
            result = newSprite;
        });

        if (result != null && tempValue.GetInstanceID() != result.GetInstanceID())
        {
            value = result;
        }
    }
}