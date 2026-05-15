using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class BloodObjectMaskRendererFeature : ScriptableRendererFeature
{
	private const string DefaultShaderName = "Hidden/MyProject/BloodObjectMask";
	private static readonly int MaskWriteColorId = Shader.PropertyToID("_BloodMaskWriteColor");

	[SerializeField] private RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
	[SerializeField] private Shader maskShader;
	[SerializeField] private LayerMask alwaysVisibleLayerMask;
	[SerializeField] private LayerMask noBloodLayerMask;
	[SerializeField] private LayerMask alwaysVisibleAndNoBloodLayerMask;

	private BloodObjectMaskPass maskPass;

	public override void Create()
	{
		if (maskShader == null)
		{
			maskShader = Shader.Find(DefaultShaderName);
		}

		if (maskShader == null)
		{
			Debug.LogWarning($"{nameof(BloodObjectMaskRendererFeature)} could not find shader '{DefaultShaderName}'.");
			return;
		}

		if (maskPass == null)
		{
			maskPass = new BloodObjectMaskPass();
		}

		maskPass.renderPassEvent = renderPassEvent;
	}

	public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
	{
		if (maskPass == null || maskShader == null)
		{
			return;
		}

		if (renderingData.cameraData.renderType == CameraRenderType.Overlay)
		{
			return;
		}

		maskPass.Setup(
			maskShader,
			alwaysVisibleLayerMask,
			noBloodLayerMask,
			alwaysVisibleAndNoBloodLayerMask);
		renderer.EnqueuePass(maskPass);
	}

	protected override void Dispose(bool disposing)
	{
		maskPass = null;
	}

	public void SetMaskShader(Shader shader)
	{
		maskShader = shader;
	}

	private class BloodObjectMaskPass : ScriptableRenderPass
	{
		private const string PassName = "Blood Object Mask Pass";
		private static readonly int BloodObjectMaskTexId = Shader.PropertyToID("_BloodObjectMaskTex");
		private static readonly Color AlwaysVisibleMaskColor = new Color(1f, 0f, 0f, 0f);
		private static readonly Color NoBloodMaskColor = new Color(0f, 1f, 0f, 0f);
		private static readonly Color CombinedMaskColor = new Color(1f, 1f, 0f, 0f);

		private readonly List<ShaderTagId> shaderTagIds = new List<ShaderTagId>
		{
			new ShaderTagId("UniversalForward"),
			new ShaderTagId("UniversalForwardOnly"),
			new ShaderTagId("UniversalGBuffer"),
			new ShaderTagId("SRPDefaultUnlit"),
			new ShaderTagId("LightweightForward")
		};

		private Shader maskShader;
		private LayerMask alwaysVisibleLayerMask;
		private LayerMask noBloodLayerMask;
		private LayerMask combinedLayerMask;

		private class PassData
		{
			internal RendererListHandle alwaysVisibleOpaqueRendererList;
			internal RendererListHandle alwaysVisibleTransparentRendererList;
			internal RendererListHandle noBloodOpaqueRendererList;
			internal RendererListHandle noBloodTransparentRendererList;
			internal RendererListHandle combinedOpaqueRendererList;
			internal RendererListHandle combinedTransparentRendererList;
		}

		public void Setup(
			Shader overrideShader,
			LayerMask alwaysVisibleMask,
			LayerMask noBloodMask,
			LayerMask combinedMask)
		{
			maskShader = overrideShader;
			alwaysVisibleLayerMask = alwaysVisibleMask;
			noBloodLayerMask = noBloodMask;
			combinedLayerMask = combinedMask;
			ConfigureInput(ScriptableRenderPassInput.Depth);
		}

		public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
		{
			UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
			UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

			if (cameraData.cameraType != CameraType.Game || resourceData.isActiveTargetBackBuffer)
			{
				return;
			}

			TextureDesc maskTextureDesc = renderGraph.GetTextureDesc(resourceData.activeColorTexture);
			maskTextureDesc.name = "BloodObjectMask";
			maskTextureDesc.clearBuffer = true;
			maskTextureDesc.clearColor = Color.clear;
			maskTextureDesc.msaaSamples = MSAASamples.None;
			maskTextureDesc.depthBufferBits = 0;
			TextureHandle maskTexture = renderGraph.CreateTexture(maskTextureDesc);

			using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>(PassName, out PassData passData))
			{
				passData.alwaysVisibleOpaqueRendererList = CreateRendererList(renderGraph, frameData, alwaysVisibleLayerMask, RenderQueueRange.opaque, true);
				passData.alwaysVisibleTransparentRendererList = CreateRendererList(renderGraph, frameData, alwaysVisibleLayerMask, RenderQueueRange.transparent, false);
				passData.noBloodOpaqueRendererList = CreateRendererList(renderGraph, frameData, noBloodLayerMask, RenderQueueRange.opaque, true);
				passData.noBloodTransparentRendererList = CreateRendererList(renderGraph, frameData, noBloodLayerMask, RenderQueueRange.transparent, false);
				passData.combinedOpaqueRendererList = CreateRendererList(renderGraph, frameData, combinedLayerMask, RenderQueueRange.opaque, true);
				passData.combinedTransparentRendererList = CreateRendererList(renderGraph, frameData, combinedLayerMask, RenderQueueRange.transparent, false);

				UseRendererList(builder, passData.alwaysVisibleOpaqueRendererList);
				UseRendererList(builder, passData.alwaysVisibleTransparentRendererList);
				UseRendererList(builder, passData.noBloodOpaqueRendererList);
				UseRendererList(builder, passData.noBloodTransparentRendererList);
				UseRendererList(builder, passData.combinedOpaqueRendererList);
				UseRendererList(builder, passData.combinedTransparentRendererList);

				builder.AllowPassCulling(false);
				builder.AllowGlobalStateModification(true);
				builder.SetRenderAttachment(maskTexture, 0, AccessFlags.Write);
				builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);
				builder.SetGlobalTextureAfterPass(maskTexture, BloodObjectMaskTexId);
				builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
				{
					DrawRendererList(context, data.alwaysVisibleOpaqueRendererList, AlwaysVisibleMaskColor);
					DrawRendererList(context, data.alwaysVisibleTransparentRendererList, AlwaysVisibleMaskColor);
					DrawRendererList(context, data.noBloodOpaqueRendererList, NoBloodMaskColor);
					DrawRendererList(context, data.noBloodTransparentRendererList, NoBloodMaskColor);
					DrawRendererList(context, data.combinedOpaqueRendererList, CombinedMaskColor);
					DrawRendererList(context, data.combinedTransparentRendererList, CombinedMaskColor);
				});
			}
		}

		private RendererListHandle CreateRendererList(RenderGraph renderGraph, ContextContainer frameData, LayerMask layerMask, RenderQueueRange renderQueueRange, bool opaque)
		{
			if (layerMask.value == 0 || maskShader == null)
			{
				return default;
			}

			UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
			UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
			UniversalLightData lightData = frameData.Get<UniversalLightData>();

			SortingCriteria sortingCriteria = opaque ? cameraData.defaultOpaqueSortFlags : SortingCriteria.CommonTransparent;
			FilteringSettings filteringSettings = new FilteringSettings(renderQueueRange, layerMask);
			DrawingSettings drawingSettings = RenderingUtils.CreateDrawingSettings(
				shaderTagIds,
				renderingData,
				cameraData,
				lightData,
				sortingCriteria);

			drawingSettings.overrideMaterial = null;
			drawingSettings.overrideMaterialPassIndex = 0;
			drawingSettings.overrideShader = maskShader;
			drawingSettings.overrideShaderPassIndex = 0;
			drawingSettings.perObjectData = PerObjectData.None;

			RendererListParams rendererListParams = new RendererListParams(renderingData.cullResults, drawingSettings, filteringSettings);
			return renderGraph.CreateRendererList(rendererListParams);
		}

		private static void UseRendererList(IRasterRenderGraphBuilder builder, RendererListHandle rendererList)
		{
			if (rendererList.IsValid())
			{
				builder.UseRendererList(rendererList);
			}
		}

		private static void DrawRendererList(RasterGraphContext context, RendererListHandle rendererList, Color maskColor)
		{
			if (!rendererList.IsValid())
			{
				return;
			}

			context.cmd.SetGlobalColor(MaskWriteColorId, maskColor);
			context.cmd.DrawRendererList(rendererList);
		}
	}
}