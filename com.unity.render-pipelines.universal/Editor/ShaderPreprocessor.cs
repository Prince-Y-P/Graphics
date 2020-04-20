using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;

namespace UnityEditor.Rendering.Universal
{
    [Flags]
    enum ShaderFeatures
    {
        MainLight = (1 << 0),
        MainLightShadows = (1 << 1),
        AdditionalLights = (1 << 2),
        AdditionalLightShadows = (1 << 3),
        VertexLighting = (1 << 4),
        SoftShadows = (1 << 5),
        MixedLighting = (1 << 6),
        TerrainHoles = (1 << 7),
        ShaderQualityLow = (1 << 8),
        ShaderQualityMedium = (1 << 9),
        ShaderQualityHigh = (1 << 10),
    }

    internal class ShaderPreprocessor : IPreprocessShaders
    {

        ShaderKeyword m_MainLightShadows = new ShaderKeyword(ShaderKeywordStrings.MainLightShadows);
        ShaderKeyword m_AdditionalLightsVertex = new ShaderKeyword(ShaderKeywordStrings.AdditionalLightsVertex);
        ShaderKeyword m_AdditionalLightsPixel = new ShaderKeyword(ShaderKeywordStrings.AdditionalLightsPixel);
        ShaderKeyword m_AdditionalLightShadows = new ShaderKeyword(ShaderKeywordStrings.AdditionalLightShadows);
        ShaderKeyword m_CascadeShadows = new ShaderKeyword(ShaderKeywordStrings.MainLightShadowCascades);
        ShaderKeyword m_SoftShadows = new ShaderKeyword(ShaderKeywordStrings.SoftShadows);
        ShaderKeyword m_MixedLightingSubtractive = new ShaderKeyword(ShaderKeywordStrings.MixedLightingSubtractive);
        ShaderKeyword m_Lightmap = new ShaderKeyword("LIGHTMAP_ON");
        ShaderKeyword m_DirectionalLightmap = new ShaderKeyword("DIRLIGHTMAP_COMBINED");
        ShaderKeyword m_AlphaTestOn = new ShaderKeyword("_ALPHATEST_ON");
        ShaderKeyword m_ShaderQualityLow = new ShaderKeyword(ShaderKeywordStrings.ShaderQualityLow);
        ShaderKeyword m_ShaderQualityMedium = new ShaderKeyword(ShaderKeywordStrings.ShaderQualityMedium);
        ShaderKeyword m_ShaderQualityHigh = new ShaderKeyword(ShaderKeywordStrings.ShaderQualityHigh);


        ShaderKeyword m_DeprecatedVertexLights = new ShaderKeyword("_VERTEX_LIGHTS");
        ShaderKeyword m_DeprecatedShadowsEnabled = new ShaderKeyword("_SHADOWS_ENABLED");
        ShaderKeyword m_DeprecatedShadowsCascade = new ShaderKeyword("_SHADOWS_CASCADE");
        ShaderKeyword m_DeprecatedLocalShadowsEnabled = new ShaderKeyword("_LOCAL_SHADOWS_ENABLED");

        int m_TotalVariantsInputCount;
        int m_TotalVariantsOutputCount;

        // Multiple callback may be implemented.
        // The first one executed is the one where callbackOrder is returning the smallest number.
        public int callbackOrder { get { return 0; } }

        bool StripUnusedShader(ShaderFeatures features, Shader shader, ShaderCompilerData compilerData)
        {
            if (!CoreUtils.HasFlag(features, ShaderFeatures.MainLightShadows) &&
                shader.name.Contains("ScreenSpaceShadows"))
                return true;

            return false;
        }

        bool StripUnusedPass(ShaderFeatures features, ShaderSnippetData snippetData)
        {
            if (snippetData.passType == PassType.Meta)
                return true;

            if (snippetData.passType == PassType.ShadowCaster)
                if (!CoreUtils.HasFlag(features, ShaderFeatures.MainLightShadows) && !CoreUtils.HasFlag(features, ShaderFeatures.AdditionalLightShadows))
                    return true;

            return false;
        }

        bool StripUnusedFeatures(ShaderFeatures features, Shader shader, ShaderCompilerData compilerData)
        {
            if (!CoreUtils.HasFlag(features, ShaderFeatures.ShaderQualityLow) &&
                compilerData.shaderKeywordSet.IsEnabled(m_ShaderQualityLow))
                return true;

            if (!CoreUtils.HasFlag(features, ShaderFeatures.ShaderQualityMedium) &&
                compilerData.shaderKeywordSet.IsEnabled(m_ShaderQualityMedium))
                return true;

            if (!CoreUtils.HasFlag(features, ShaderFeatures.ShaderQualityHigh) &&
                compilerData.shaderKeywordSet.IsEnabled(m_ShaderQualityHigh))
                return true;

            // strip main light shadows and cascade variants
            if (!CoreUtils.HasFlag(features, ShaderFeatures.MainLightShadows))
            {
                if (compilerData.shaderKeywordSet.IsEnabled(m_MainLightShadows))
                    return true;

                if (compilerData.shaderKeywordSet.IsEnabled(m_CascadeShadows))
                    return true;
            }

            bool isAdditionalLightPerVertex = compilerData.shaderKeywordSet.IsEnabled(m_AdditionalLightsVertex);
            bool isAdditionalLightPerPixel = compilerData.shaderKeywordSet.IsEnabled(m_AdditionalLightsPixel);
            bool isAdditionalLightShadow = compilerData.shaderKeywordSet.IsEnabled(m_AdditionalLightShadows);

            // Additional light are shaded per-vertex. Strip additional lights per-pixel and shadow variants
            if ((isAdditionalLightPerPixel || isAdditionalLightShadow) &&
                CoreUtils.HasFlag(features, ShaderFeatures.VertexLighting))
                return true;

            // No additional lights
            if ((isAdditionalLightPerPixel || isAdditionalLightPerVertex || isAdditionalLightShadow)&&
                !CoreUtils.HasFlag(features, ShaderFeatures.AdditionalLights))
                return true;

            // No additional light shadows
            if (isAdditionalLightShadow && !CoreUtils.HasFlag(features, ShaderFeatures.AdditionalLightShadows))
                return true;

            if (!CoreUtils.HasFlag(features, ShaderFeatures.SoftShadows) &&
                compilerData.shaderKeywordSet.IsEnabled(m_SoftShadows))
                return true;

            if (!CoreUtils.HasFlag(features, ShaderFeatures.MixedLighting) &&
                compilerData.shaderKeywordSet.IsEnabled(m_MixedLightingSubtractive))
                return true;

            if (!CoreUtils.HasFlag(features, ShaderFeatures.TerrainHoles) &&
                shader.name.Contains("Universal Render Pipeline/Terrain/Lit") &&
                compilerData.shaderKeywordSet.IsEnabled(m_AlphaTestOn))
                return true;

            return false;
        }

        bool StripUnsupportedVariants(ShaderCompilerData compilerData)
        {
            // Dynamic GI is not supported so we can strip variants that have directional lightmap
            // enabled but not baked lightmap.
            if (compilerData.shaderKeywordSet.IsEnabled(m_DirectionalLightmap) &&
                !compilerData.shaderKeywordSet.IsEnabled(m_Lightmap))
                return true;

            if (compilerData.shaderCompilerPlatform == ShaderCompilerPlatform.GLES20)
            {
                if (compilerData.shaderKeywordSet.IsEnabled(m_CascadeShadows))
                    return true;
            }

            return false;
        }

        bool StripInvalidVariants(ShaderCompilerData compilerData)
        {
            bool isMainShadow = compilerData.shaderKeywordSet.IsEnabled(m_MainLightShadows);
            bool isAdditionalShadow = compilerData.shaderKeywordSet.IsEnabled(m_AdditionalLightShadows);
            bool isShadowVariant = isMainShadow || isAdditionalShadow;

            if (!isMainShadow && compilerData.shaderKeywordSet.IsEnabled(m_CascadeShadows))
                return true;

            if (!isShadowVariant && compilerData.shaderKeywordSet.IsEnabled(m_SoftShadows))
                return true;

            if (isAdditionalShadow && !compilerData.shaderKeywordSet.IsEnabled(m_AdditionalLightsPixel))
                return true;

            return false;
        }

        bool StripDeprecated(ShaderCompilerData compilerData)
        {
            if (compilerData.shaderKeywordSet.IsEnabled(m_DeprecatedVertexLights))
                return true;

            if (compilerData.shaderKeywordSet.IsEnabled(m_DeprecatedShadowsCascade))
                return true;

            if (compilerData.shaderKeywordSet.IsEnabled(m_DeprecatedShadowsEnabled))
                return true;

            if (compilerData.shaderKeywordSet.IsEnabled(m_DeprecatedLocalShadowsEnabled))
                return true;

            return false;
        }

        bool StripUnused(ShaderFeatures features, Shader shader, ShaderSnippetData snippetData, ShaderCompilerData compilerData)
        {
            if (StripUnusedShader(features, shader, compilerData))
                return true;

            if (StripUnusedPass(features, snippetData))
                return true;

            if (StripUnusedFeatures(features, shader, compilerData))
                return true;

            if (StripUnsupportedVariants(compilerData))
                return true;

            if (StripInvalidVariants(compilerData))
                return true;

            if (StripDeprecated(compilerData))
                return true;

            return false;
        }

        void LogShaderVariants(Shader shader, ShaderSnippetData snippetData, ShaderVariantLogLevel logLevel, int prevVariantsCount, int currVariantsCount)
        {
            if (logLevel == ShaderVariantLogLevel.AllShaders || shader.name.Contains("Universal Render Pipeline"))
            {
                float percentageCurrent = (float)currVariantsCount / (float)prevVariantsCount * 100f;
                float percentageTotal = (float)m_TotalVariantsOutputCount / (float)m_TotalVariantsInputCount * 100f;

                string result = string.Format("STRIPPING: {0} ({1} pass) ({2}) -" +
                        " Remaining shader variants = {3}/{4} = {5}% - Total = {6}/{7} = {8}%",
                        shader.name, snippetData.passName, snippetData.shaderType.ToString(), currVariantsCount,
                        prevVariantsCount, percentageCurrent, m_TotalVariantsOutputCount, m_TotalVariantsInputCount,
                        percentageTotal);
                Debug.Log(result);
            }
        }

        public void OnProcessShader(Shader shader, ShaderSnippetData snippetData, IList<ShaderCompilerData> compilerDataList)
        {
            UniversalRenderPipelineAsset urpAsset = GraphicsSettings.renderPipelineAsset as UniversalRenderPipelineAsset;
            if (urpAsset == null || compilerDataList == null || compilerDataList.Count == 0)
                return;


            int prevVariantCount = compilerDataList.Count;
            Debug.Log(ShaderBuildPreprocessor.supportedFeatures);
            for (int i = 0; i < compilerDataList.Count; ++i)
            {
                if (StripUnused(ShaderBuildPreprocessor.supportedFeatures, shader, snippetData, compilerDataList[i]))
                {
                    compilerDataList.RemoveAt(i);
                    --i;
                }
            }

            if (urpAsset.shaderVariantLogLevel != ShaderVariantLogLevel.Disabled)
            {
                m_TotalVariantsInputCount += prevVariantCount;
                m_TotalVariantsOutputCount += compilerDataList.Count;
                LogShaderVariants(shader, snippetData, urpAsset.shaderVariantLogLevel, prevVariantCount, compilerDataList.Count);
            }
        }

        class ShaderBuildPreprocessor : IPreprocessBuildWithReport
        {

            public static ShaderFeatures supportedFeatures
            {
                get
                {
                    if (_supportedFeatures <= 0)
                    {
                        FetchAllSupportedFeatures();
                    }
                    return _supportedFeatures;
                }
            }

            private static ShaderFeatures _supportedFeatures = 0;
            public int callbackOrder { get { return 0; } }

            public void OnPreprocessBuild(BuildReport report)
            {
                FetchAllSupportedFeatures();
            }

            private static void FetchAllSupportedFeatures()
            {
                List<UniversalRenderPipelineAsset> urps = new List<UniversalRenderPipelineAsset>();
                urps.Add(GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset);
                for (int i = 0; i < QualitySettings.names.Length; i++)
                {
                    urps.Add(QualitySettings.GetRenderPipelineAssetAt(i) as UniversalRenderPipelineAsset);
                }
                foreach (UniversalRenderPipelineAsset urp in urps)
                {
                    _supportedFeatures |= GetSupportedShaderFeatures(urp);
                }
            }
        }

        private static ShaderFeatures GetSupportedShaderFeatures(UniversalRenderPipelineAsset pipelineAsset)
        {
            ShaderFeatures shaderFeatures;
            shaderFeatures = ShaderFeatures.MainLight;

            if (pipelineAsset.supportsMainLightShadows)
                shaderFeatures |= ShaderFeatures.MainLightShadows;

            if (pipelineAsset.additionalLightsRenderingMode == LightRenderingMode.PerVertex)
            {
                shaderFeatures |= ShaderFeatures.AdditionalLights;
                shaderFeatures |= ShaderFeatures.VertexLighting;
            }
            else if (pipelineAsset.additionalLightsRenderingMode == LightRenderingMode.PerPixel)
            {
                shaderFeatures |= ShaderFeatures.AdditionalLights;

                if (pipelineAsset.supportsAdditionalLightShadows)
                    shaderFeatures |= ShaderFeatures.AdditionalLightShadows;
            }

            bool anyShadows = pipelineAsset.supportsMainLightShadows ||
                              CoreUtils.HasFlag(shaderFeatures, ShaderFeatures.AdditionalLightShadows);
            if (pipelineAsset.supportsSoftShadows && anyShadows)
                shaderFeatures |= ShaderFeatures.SoftShadows;

            if (pipelineAsset.supportsMixedLighting)
                shaderFeatures |= ShaderFeatures.MixedLighting;

            if (pipelineAsset.supportsTerrainHoles)
                shaderFeatures |= ShaderFeatures.TerrainHoles;

            if (pipelineAsset.shaderQuality == UnityEngine.Rendering.Universal.ShaderQuality.Low)
                shaderFeatures |= ShaderFeatures.ShaderQualityLow;
            else if (pipelineAsset.shaderQuality == UnityEngine.Rendering.Universal.ShaderQuality.Medium)
                shaderFeatures |= ShaderFeatures.ShaderQualityMedium;
            else if (pipelineAsset.shaderQuality == UnityEngine.Rendering.Universal.ShaderQuality.High)
                shaderFeatures |= ShaderFeatures.ShaderQualityHigh;

            return shaderFeatures;
        }
    }
}
