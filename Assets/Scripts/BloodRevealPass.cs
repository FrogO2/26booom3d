using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public class BloodRevealPass : ScriptableRenderPass
{
	private const string PassName = "Blood Reveal Pass";
	private const string StackCopyPassName = "Blood Reveal Stack Copy";

	private Material revealMaterial;

	private class PassData
	{
		internal Material material;
		internal TextureHandle source;
	}

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

		if (cameraData.cameraType != CameraType.Game || resourceData.isActiveTargetBackBuffer)
		{
			return;
		}

		BloodRevealManager.ApplyToMaterial(revealMaterial);

		TextureHandle source = resourceData.activeColorTexture;
		if (!source.IsValid())
		{
			return;
		}

		bool preserveStackColor = cameraData.renderType == CameraRenderType.Base &&
			!cameraData.resolveFinalTarget &&
			resourceData.cameraColor.IsValid();

		TextureDesc destinationDesc = renderGraph.GetTextureDesc(source);
		destinationDesc.name = "CameraColor-BloodReveal";
		destinationDesc.clearBuffer = false;
		TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

		using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>(PassName, out PassData passData))
		{
			passData.material = revealMaterial;
			passData.source = source;

			builder.UseTexture(passData.source, AccessFlags.Read);
			builder.UseAllGlobalTextures(true);
			builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
			builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
			{
				Blitter.BlitTexture(context.cmd, data.source, Vector2.one, data.material, 0);
			});
		}

		if (preserveStackColor)
		{
			renderGraph.AddBlitPass(destination, resourceData.cameraColor, Vector2.one, Vector2.zero, passName: StackCopyPassName);
			return;
		}

		resourceData.cameraColor = destination;
	}
}