using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class BloodObjectMaskRendererFeature : ScriptableRendererFeature
{
	private const string DefaultShaderName = "Hidden/MyProject/BloodObjectMask";
	private static readonly int MaskColorId = Shader.PropertyToID("_MaskColor");

	[SerializeField] private RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
	[SerializeField] private Shader maskShader;
	[SerializeField] private LayerMask alwaysVisibleLayerMask;
	[SerializeField] private LayerMask noBloodLayerMask;
	[SerializeField] private LayerMask alwaysVisibleAndNoBloodLayerMask;

	private Material alwaysVisibleMaterial;
	private Material noBloodMaterial;
	private Material combinedMaterial;
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

		alwaysVisibleMaterial = CreateOrUpdateMaterial(alwaysVisibleMaterial, new Color(1f, 0f, 0f, 1f));
		noBloodMaterial = CreateOrUpdateMaterial(noBloodMaterial, new Color(0f, 1f, 0f, 1f));
		combinedMaterial = CreateOrUpdateMaterial(combinedMaterial, new Color(1f, 1f, 0f, 1f));

		if (maskPass == null)
		{
			maskPass = new BloodObjectMaskPass();
		}

		maskPass.renderPassEvent = renderPassEvent;
	}

	public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
	{
		if (maskPass == null || alwaysVisibleMaterial == null || noBloodMaterial == null || combinedMaterial == null)
		{
			return;
		}

		maskPass.Setup(
			alwaysVisibleMaterial,
			noBloodMaterial,
			combinedMaterial,
			alwaysVisibleLayerMask,
			noBloodLayerMask,
			alwaysVisibleAndNoBloodLayerMask);
		renderer.EnqueuePass(maskPass);
	}

	protected override void Dispose(bool disposing)
	{
		CoreUtils.Destroy(alwaysVisibleMaterial);
		CoreUtils.Destroy(noBloodMaterial);
		CoreUtils.Destroy(combinedMaterial);
		alwaysVisibleMaterial = null;
		noBloodMaterial = null;
		combinedMaterial = null;
		maskPass = null;
	}

	public void SetMaskShader(Shader shader)
	{
		maskShader = shader;
	}

	private Material CreateOrUpdateMaterial(Material material, Color maskColor)
	{
		if (material == null || material.shader != maskShader)
		{
			CoreUtils.Destroy(material);
			material = CoreUtils.CreateEngineMaterial(maskShader);
		}

		material.SetColor(MaskColorId, maskColor);
		return material;
	}

	private class BloodObjectMaskPass : ScriptableRenderPass
	{
		private const string PassName = "Blood Object Mask Pass";
		private static readonly int BloodObjectMaskTexId = Shader.PropertyToID("_BloodObjectMaskTex");

		private readonly List<ShaderTagId> shaderTagIds = new List<ShaderTagId>
		{
			new ShaderTagId("UniversalForward"),
			new ShaderTagId("UniversalForwardOnly"),
			new ShaderTagId("UniversalGBuffer"),
			new ShaderTagId("SRPDefaultUnlit"),
			new ShaderTagId("LightweightForward")
		};

		private Material alwaysVisibleMaterial;
		private Material noBloodMaterial;
		private Material combinedMaterial;
		private LayerMask alwaysVisibleLayerMask;
		private LayerMask noBloodLayerMask;
		private LayerMask combinedLayerMask;

		private class PassData
		{
			internal RendererListHandle alwaysVisibleRendererList;
			internal RendererListHandle noBloodRendererList;
			internal RendererListHandle combinedRendererList;
		}

		public void Setup(
			Material alwaysVisible,
			Material noBlood,
			Material combined,
			LayerMask alwaysVisibleMask,
			LayerMask noBloodMask,
			LayerMask combinedMask)
		{
			alwaysVisibleMaterial = alwaysVisible;
			noBloodMaterial = noBlood;
			combinedMaterial = combined;
			alwaysVisibleLayerMask = alwaysVisibleMask;
			noBloodLayerMask = noBloodMask;
			combinedLayerMask = combinedMask;
			ConfigureInput(ScriptableRenderPassInput.Depth);
		}

		public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
		{
			UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
			UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

			if (cameraData.camera.cameraType != CameraType.Game || resourceData.isActiveTargetBackBuffer)
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
				passData.alwaysVisibleRendererList = CreateRendererList(renderGraph, frameData, alwaysVisibleLayerMask, alwaysVisibleMaterial);
				passData.noBloodRendererList = CreateRendererList(renderGraph, frameData, noBloodLayerMask, noBloodMaterial);
				passData.combinedRendererList = CreateRendererList(renderGraph, frameData, combinedLayerMask, combinedMaterial);

				if (passData.alwaysVisibleRendererList.IsValid())
				{
					builder.UseRendererList(passData.alwaysVisibleRendererList);
				}

				if (passData.noBloodRendererList.IsValid())
				{
					builder.UseRendererList(passData.noBloodRendererList);
				}

				if (passData.combinedRendererList.IsValid())
				{
					builder.UseRendererList(passData.combinedRendererList);
				}

				builder.AllowPassCulling(false);
				builder.SetRenderAttachment(maskTexture, 0, AccessFlags.Write);
				builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);
				builder.SetGlobalTextureAfterPass(maskTexture, BloodObjectMaskTexId);
				builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
				{
					if (data.alwaysVisibleRendererList.IsValid())
					{
						context.cmd.DrawRendererList(data.alwaysVisibleRendererList);
					}

					if (data.noBloodRendererList.IsValid())
					{
						context.cmd.DrawRendererList(data.noBloodRendererList);
					}

					if (data.combinedRendererList.IsValid())
					{
						context.cmd.DrawRendererList(data.combinedRendererList);
					}
				});
			}
		}

		private RendererListHandle CreateRendererList(RenderGraph renderGraph, ContextContainer frameData, LayerMask layerMask, Material material)
		{
			if (layerMask.value == 0 || material == null)
			{
				return default;
			}

			UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
			UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
			UniversalLightData lightData = frameData.Get<UniversalLightData>();

			FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.all, layerMask);
			DrawingSettings drawingSettings = RenderingUtils.CreateDrawingSettings(
				shaderTagIds,
				renderingData,
				cameraData,
				lightData,
				cameraData.defaultOpaqueSortFlags);

			drawingSettings.overrideMaterial = material;
			drawingSettings.overrideMaterialPassIndex = 0;
			drawingSettings.perObjectData = PerObjectData.None;

			RendererListParams rendererListParams = new RendererListParams(renderingData.cullResults, drawingSettings, filteringSettings);
			return renderGraph.CreateRendererList(rendererListParams);
		}
	}
}