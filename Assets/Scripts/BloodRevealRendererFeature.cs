using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class BloodRevealRendererFeature : ScriptableRendererFeature
{
	private const string DefaultShaderName = "Hidden/MyProject/BloodRevealMask";

	[SerializeField] private RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
	[SerializeField] private Shader revealShader;

	private Material revealMaterial;
	private BloodRevealPass revealPass;

	public override void Create()
	{
		if (revealShader == null)
		{
			revealShader = Shader.Find(DefaultShaderName);
		}

		if (revealShader == null)
		{
			Debug.LogWarning($"{nameof(BloodRevealRendererFeature)} could not find shader '{DefaultShaderName}'.");
			return;
		}

		if (revealMaterial == null || revealMaterial.shader != revealShader)
		{
			CoreUtils.Destroy(revealMaterial);
			revealMaterial = CoreUtils.CreateEngineMaterial(revealShader);
		}

		if (revealPass == null)
		{
			revealPass = new BloodRevealPass(revealMaterial, renderPassEvent);
		}

		revealPass.renderPassEvent = renderPassEvent;
	}

	public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
	{
		if (revealMaterial == null || revealPass == null)
		{
			return;
		}

		if (renderingData.cameraData.renderType == CameraRenderType.Overlay)
		{
			return;
		}

		revealPass.Setup(revealMaterial);
		renderer.EnqueuePass(revealPass);
	}

	protected override void Dispose(bool disposing)
	{
		CoreUtils.Destroy(revealMaterial);
		revealMaterial = null;
		revealPass = null;
	}

	public void SetRevealShader(Shader shader)
	{
		revealShader = shader;
	}
}