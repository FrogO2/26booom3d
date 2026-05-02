using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public class BloodRevealPass : ScriptableRenderPass
{
	private const string PassName = "Blood Reveal Pass";

	private Material revealMaterial;

	public BloodRevealPass(Material material, RenderPassEvent passEvent)
	{
		revealMaterial = material;
		renderPassEvent = passEvent;
	}

	public void Setup(Material material)
	{
		revealMaterial = material;
		ConfigureInput(ScriptableRenderPassInput.Depth);
		requiresIntermediateTexture = true;
	}

	public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
	{
		if (revealMaterial == null)
		{
			return;
		}

		UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
		UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

		if (cameraData.camera.cameraType != CameraType.Game || resourceData.isActiveTargetBackBuffer)
		{
			return;
		}

		BloodRevealManager.ApplyToMaterial(revealMaterial);

		TextureHandle source = resourceData.activeColorTexture;
		if (!source.IsValid())
		{
			return;
		}

		TextureDesc destinationDesc = renderGraph.GetTextureDesc(source);
		destinationDesc.name = "CameraColor-BloodReveal";
		destinationDesc.clearBuffer = false;
		TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

		RenderGraphUtils.BlitMaterialParameters blitParameters = new RenderGraphUtils.BlitMaterialParameters(source, destination, revealMaterial, 0);
		renderGraph.AddBlitPass(blitParameters, PassName);

		resourceData.cameraColor = destination;
	}
}