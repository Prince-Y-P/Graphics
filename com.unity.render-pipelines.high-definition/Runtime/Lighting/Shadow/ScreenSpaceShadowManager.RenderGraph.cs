using System;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;

namespace UnityEngine.Rendering.HighDefinition
{
    public partial class HDRenderPipeline
    {
        public TextureHandle CreateScreenSpaceShadowTextureArray(RenderGraph renderGraph)
        {
            int numShadowTextures = Math.Max((int)Math.Ceiling(m_Asset.currentPlatformRenderPipelineSettings.hdShadowInitParams.maxScreenSpaceShadowSlots / 4.0f), 1);
            GraphicsFormat graphicsFormat = (GraphicsFormat)m_Asset.currentPlatformRenderPipelineSettings.hdShadowInitParams.screenSpaceShadowBufferFormat;
            return renderGraph.CreateTexture(new TextureDesc(Vector2.one, true, true)
            {
                colorFormat = graphicsFormat,
                slices = numShadowTextures * TextureXR.slices,
                dimension = TextureDimension.Tex2DArray,
                filterMode = FilterMode.Point,
                enableRandomWrite = true,
                useDynamicScale = true,
                useMipMap = false,
                name = "AreaShadowArrayBuffer"
            });
        }

        TextureHandle RenderScreenSpaceShadows(RenderGraph renderGraph, HDCamera hdCamera, TextureHandle depthBuffer, TextureHandle normalBuffer, TextureHandle motionVectorsBuffer, TextureHandle rayCountTexture)
        {
            // If screen space shadows are not supported for this camera, we are done
            if (!hdCamera.frameSettings.IsEnabled(FrameSettingsField.ScreenSpaceShadows))
                return m_RenderGraph.defaultResources.blackTextureArrayXR;

            // Request the output texture
            TextureHandle screenSpaceShadowTexture = CreateScreenSpaceShadowTextureArray(renderGraph);

            // First of all we handle the directional light
            screenSpaceShadowTexture = RenderDirectionalLightScreenSpaceShadow(renderGraph, hdCamera, depthBuffer, normalBuffer, motionVectorsBuffer, rayCountTexture, screenSpaceShadowTexture);

            // We render the debug view
            // TODO: The texture is currently unused, make usage of it
            EvaluateShadowDebugView(renderGraph, hdCamera, screenSpaceShadowTexture);

            return screenSpaceShadowTexture;
        }

        class ScreenSpaceShadowDebugPassData
        {
            public SSShadowDebugParameters parameters;
            public TextureHandle screenSpaceShadowArray;
            public TextureHandle outputBuffer;
        }

        TextureHandle EvaluateShadowDebugView(RenderGraph renderGraph, HDCamera hdCamera, TextureHandle screenSpaceShadowArray)
        {
            // If this is the right debug mode and the index we are asking for is in the range
            if (!rayTracingSupported || (m_ScreenSpaceShadowChannelSlot <= m_CurrentDebugDisplaySettings.data.screenSpaceShadowIndex))
                return m_RenderGraph.defaultResources.blackTextureXR;

            using (var builder = renderGraph.AddRenderPass<ScreenSpaceShadowDebugPassData>("Screen Space Shadows Debug", out var passData, ProfilingSampler.Get(HDProfileId.ScreenSpaceShadowsDebug)))
            {
                passData.parameters = PrepareSSShadowDebugParameters(hdCamera, (int)m_CurrentDebugDisplaySettings.data.screenSpaceShadowIndex);
                passData.screenSpaceShadowArray = builder.ReadTexture(screenSpaceShadowArray);
                passData.outputBuffer = builder.WriteTexture(renderGraph.CreateTexture(new TextureDesc(Vector2.one, true, true)
                                            { colorFormat = GraphicsFormat.R16G16B16A16_SFloat, enableRandomWrite = true, name = "EvaluateShadowDebug" }));

                builder.SetRenderFunc(
                (ScreenSpaceShadowDebugPassData data, RenderGraphContext context) =>
                {
                    SSShadowDebugResources resources = new SSShadowDebugResources();
                    resources.screenSpaceShadowArray = data.screenSpaceShadowArray;
                    resources.outputBuffer = data.outputBuffer;
                    ExecuteShadowDebugView(context.cmd, data.parameters, resources);
                });
                return passData.outputBuffer;
            }
        }

        class WriteScreenSpaceShadowPassData
        {
            public WriteScreenSpaceShadowParameters parameters;
            public TextureHandle inputShadowBuffer;
            public TextureHandle outputShadowArrayBuffer;
        }

        void WriteScreenSpaceShadow(RenderGraph renderGraph, HDCamera hdCamera, TextureHandle shadowTexture, TextureHandle screenSpaceShadowArray)
        {
            // Write the result texture to the screen space shadow buffer
            int dirShadowIndex = m_CurrentSunLightDirectionalLightData.screenSpaceShadowIndex & (int)LightDefinitions.s_ScreenSpaceShadowIndexMask;
            using (var builder = renderGraph.AddRenderPass<WriteScreenSpaceShadowPassData>("Screen Space Shadows Debug", out var passData, ProfilingSampler.Get(HDProfileId.RaytracingWriteShadow)))
            {
                passData.parameters = PrepareWriteScreenSpaceShadowParameters(hdCamera, dirShadowIndex, m_CurrentSunLightAdditionalLightData.colorShadow ? ScreenSpaceShadowType.Color : ScreenSpaceShadowType.GrayScale);
                passData.inputShadowBuffer = builder.ReadTexture(shadowTexture);
                passData.outputShadowArrayBuffer = builder.WriteTexture(builder.ReadTexture(screenSpaceShadowArray));

                builder.SetRenderFunc(
                (WriteScreenSpaceShadowPassData data, RenderGraphContext context) =>
                {
                    WriteScreenSpaceShadowResources resources = new WriteScreenSpaceShadowResources();
                    resources.inputShadowBuffer = data.inputShadowBuffer;
                    resources.outputShadowArrayBuffer = data.outputShadowArrayBuffer;
                    ExecuteWriteScreenSpaceShadow(context.cmd, data.parameters, resources);
                });
            }
        }
    }
}
