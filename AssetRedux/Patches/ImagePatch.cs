using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace AssetRedux.Patches;

[HarmonyPatch(typeof(Image))]
public static class ImagePatch
{
    // 拦截 Sprite Setter
    [HarmonyPatch(nameof(Image.sprite), MethodType.Setter)]
    [HarmonyPrefix]
    public static void SetSpritePrefix(ref Sprite value)
    {
        if (value == null) return;

        // 尝试从 SpriteManager 获取自定义资源
        if (Tools.SpriteManager.TryGetSprite(value.name, out Sprite? newSprite))
        {
            if (newSprite != null && value.GetInstanceID() != newSprite.GetInstanceID())
            {
                value = newSprite;
            }
        }
    }

    // 拦截 OnEnable 确保初始化时的图片也被替换
    [HarmonyPatch(nameof(Image.OnEnable))]
    [HarmonyPostfix]
    public static void OnEnablePostfix(Image __instance)
    {
        if (__instance.sprite == null) return;

        if (Tools.SpriteManager.TryGetSprite(__instance.sprite.name, out Sprite? newSprite))
        {
            if (newSprite != null && __instance.sprite.GetInstanceID() != newSprite.GetInstanceID())
            {
                __instance.sprite = newSprite;
            }
        }
    }
}