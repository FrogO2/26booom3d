using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public class BloodFxPass : ScriptableRenderPass
{
	private const string PassName = "Blood FX Pass";

	private Material bloodFxMaterial;

	public BloodFxPass(Material material, RenderPassEvent passEvent)
	{
		bloodFxMaterial = material;
		renderPassEvent = passEvent;
	}

	public void Setup(Material material)
	{
		bloodFxMaterial = material;
		ConfigureInput(ScriptableRenderPassInput.Depth);
		requiresIntermediateTexture = true;
	}

	public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
	{
		if (bloodFxMaterial == null || !BloodFxManager.HasActiveEffects)
		{
			return;
		}

		UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
		UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

		if (cameraData.camera.cameraType != CameraType.Game || resourceData.isActiveTargetBackBuffer)
		{
			return;
		}

		BloodFxManager.ApplyToMaterial(bloodFxMaterial);

		TextureHandle source = resourceData.activeColorTexture;
		if (!source.IsValid())
		{
			return;
		}

		TextureDesc destinationDesc = renderGraph.GetTextureDesc(source);
		destinationDesc.name = "CameraColor-BloodFx";
		destinationDesc.clearBuffer = false;
		TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

		RenderGraphUtils.BlitMaterialParameters blitParameters = new RenderGraphUtils.BlitMaterialParameters(source, destination, bloodFxMaterial, 0);
		renderGraph.AddBlitPass(blitParameters, PassName);

		resourceData.cameraColor = destination;
	}
}