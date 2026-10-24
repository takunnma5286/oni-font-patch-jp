using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

namespace enslo.OniFontPatchJp
{
    // The bundled font assets are serialized in the legacy TMP format (m_fontInfo, m_glyphInfoList).
    // Newer TMP shipped with the game no longer upgrades them at runtime, so do it ourselves.
    public static class LegacyFontAssetUpgrader
    {
        public static void Upgrade(TMP_FontAsset font, IEnumerable<Material> bundledMaterials, Shader shader)
        {
            var asset = Traverse.Create(font);
            var fontInfo = asset.Field("m_fontInfo").GetValue<FaceInfo_Legacy>();
            var glyphInfoList = asset.Field("m_glyphInfoList").GetValue<List<TMP_Glyph>>();

            if (!string.IsNullOrEmpty(font.version) || fontInfo is null || glyphInfoList is null || glyphInfoList.Count == 0)
            {
                return; // Already in the new format.
            }

            if (font.material == null)
            {
                font.material = bundledMaterials.FirstOrDefault(material => material.name == $"{font.name} Material");
            }

            // Shaders in an old bundle may not work with the current Unity, so borrow the one the game uses.
            if (font.material != null && shader != null)
            {
                font.material.shader = shader;
            }

            font.faceInfo = new FaceInfo
            {
                familyName = fontInfo.Name,
                styleName = string.Empty,
                pointSize = (int)fontInfo.PointSize,
                scale = fontInfo.Scale,
                lineHeight = fontInfo.LineHeight,
                ascentLine = fontInfo.Ascender,
                capLine = fontInfo.CapHeight,
                meanLine = fontInfo.CenterLine,
                baseline = fontInfo.Baseline,
                descentLine = fontInfo.Descender,
                superscriptOffset = fontInfo.SuperscriptOffset,
                superscriptSize = fontInfo.SubSize,
                subscriptOffset = fontInfo.SubscriptOffset,
                subscriptSize = fontInfo.SubSize,
                underlineOffset = fontInfo.Underline,
                underlineThickness = fontInfo.UnderlineThickness,
                strikethroughOffset = fontInfo.strikethrough,
                strikethroughThickness = fontInfo.strikethroughThickness,
                tabWidth = fontInfo.TabWidth,
            };

            var atlasHeight = (int)fontInfo.AtlasHeight;
            asset.Field("m_AtlasWidth").SetValue((int)fontInfo.AtlasWidth);
            asset.Field("m_AtlasHeight").SetValue(atlasHeight);
            asset.Field("m_AtlasPadding").SetValue((int)fontInfo.Padding);
            asset.Field("m_AtlasRenderMode").SetValue(
                font.material != null && font.material.HasProperty(ShaderUtilities.ID_GradientScale)
                    ? GlyphRenderMode.SDFAA
                    : GlyphRenderMode.SMOOTH);

            var atlas = font.atlas != null || font.material == null
                ? font.atlas
                : font.material.GetTexture(ShaderUtilities.ID_MainTex) as Texture2D;
            asset.Field("m_AtlasTextures").SetValue(new[] { atlas });

            // Legacy glyph rects are top-left origin, while the new ones are bottom-left origin.
            var glyphTable = new List<Glyph>(glyphInfoList.Count);
            var characterTable = new List<TMP_Character>(glyphInfoList.Count);
            for (var i = 0; i < glyphInfoList.Count; i++)
            {
                var oldGlyph = glyphInfoList[i];
                var glyph = new Glyph(
                    (uint)i + 1,
                    new GlyphMetrics(oldGlyph.width, oldGlyph.height, oldGlyph.xOffset, oldGlyph.yOffset, oldGlyph.xAdvance),
                    new GlyphRect(
                        (int)oldGlyph.x,
                        atlasHeight - (int)(oldGlyph.y + oldGlyph.height + 0.5f),
                        (int)(oldGlyph.width + 0.5f),
                        (int)(oldGlyph.height + 0.5f)),
                    oldGlyph.scale,
                    0);
                glyphTable.Add(glyph);
                characterTable.Add(new TMP_Character((uint)oldGlyph.id, font, glyph));
            }
            asset.Field("m_GlyphTable").SetValue(glyphTable);
            asset.Field("m_CharacterTable").SetValue(characterTable);

            if (asset.Field("m_FontFeatureTable").GetValue() is null)
            {
                asset.Field("m_FontFeatureTable").SetValue(new TMP_FontFeatureTable());
            }

            var fallbacks = asset.Field("fallbackFontAssets").GetValue<List<TMP_FontAsset>>();
            font.fallbackFontAssetTable = fallbacks?.Where(fallback => fallback != null).ToList() ?? new List<TMP_FontAsset>();

            asset.Field("m_Version").SetValue("1.1.0");
            font.ReadFontAssetDefinition();
        }
    }
}
