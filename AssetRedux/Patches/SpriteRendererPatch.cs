using HarmonyLib;
using UnityEngine;

namespace AssetRedux.Patches;

[HarmonyPatch(typeof(SpriteRenderer), nameof(SpriteRenderer.sprite), MethodType.Setter)]
public static class SpriteRendererPatch
{
    [HarmonyPrefix]
    public static void Prefix(ref Sprite value)
    {
        if (value == null) return;

        if (Tools.SpriteManager.TryGetSprite(value.name, out Sprite? newSprite))
        {
            if (newSprite != null && value.GetInstanceID() != newSprite.GetInstanceID())
            {
                value = newSprite;
            }
        }
    }
}