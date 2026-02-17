using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace AssetRedux.Patches;

[HarmonyPatch(typeof(Image))]
public static class ImagePatch
{
    [HarmonyPatch(nameof(Image.sprite), MethodType.Setter)]
    [HarmonyPrefix]
    public static void SetSpritePrefix(ref Sprite? value)
    {
        if (value == null) return;

        // 核心修复：将 ref 里的值拷贝给一个局部变量
        var tempValue = value;
        Sprite? result = null;

        // 在 ApplySprite 中查找
        Tools.SpriteManager.ApplySprite(tempValue.name, (newSprite) =>
        {
            // 如果找到了，记录下来
            result = newSprite;
        });

        // Lambda 执行完后，再根据结果修改 ref value
        if (result != null && tempValue.GetInstanceID() != result.GetInstanceID())
        {
            value = result;
        }
    }

    [HarmonyPatch(nameof(Image.OnEnable))]
    [HarmonyPostfix]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public static void OnEnablePostfix(Image __instance)
    {
        if (__instance.sprite == null) return;

        Tools.SpriteManager.ApplySprite(__instance.sprite.name, (newSprite) =>
        {
            if (__instance.sprite.GetInstanceID() != newSprite.GetInstanceID())
            {
                __instance.sprite = newSprite;
            }
        });
    }
}