using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class BloodFxPass : ScriptableRenderPass
{
	private const string PassName = "Blood FX Pass";

	private Material bloodFxMaterial;

	private class PassData
	{
		internal Material material;
		internal TextureHandle source;
	}

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

		using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>(PassName, out PassData passData))
		{
			passData.material = bloodFxMaterial;
			passData.source = source;

			builder.UseTexture(passData.source, AccessFlags.Read);
			builder.UseAllGlobalTextures(true);
			builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
			builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
			{
				Blitter.BlitTexture(context.cmd, data.source, Vector2.one, data.material, 0);
			});
		}

		resourceData.cameraColor = destination;
	}
}