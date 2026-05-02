using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class BloodFxRendererFeature : ScriptableRendererFeature
{
	private const string DefaultShaderName = "Hidden/MyProject/BloodProjectorFx";

	[SerializeField] private RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
	[SerializeField] private Shader bloodFxShader;

	private Material bloodFxMaterial;
	private BloodFxPass bloodFxPass;

	public override void Create()
	{
		if (bloodFxShader == null)
		{
			bloodFxShader = Shader.Find(DefaultShaderName);
		}

		if (bloodFxShader == null)
		{
			Debug.LogWarning($"{nameof(BloodFxRendererFeature)} could not find shader '{DefaultShaderName}'.");
			return;
		}

		if (bloodFxMaterial == null || bloodFxMaterial.shader != bloodFxShader)
		{
			CoreUtils.Destroy(bloodFxMaterial);
			bloodFxMaterial = CoreUtils.CreateEngineMaterial(bloodFxShader);
		}

		if (bloodFxPass == null)
		{
			bloodFxPass = new BloodFxPass(bloodFxMaterial, renderPassEvent);
		}

		bloodFxPass.renderPassEvent = renderPassEvent;
	}

	public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
	{
		if (bloodFxMaterial == null || bloodFxPass == null || !BloodFxManager.HasActiveEffects)
		{
			return;
		}

		bloodFxPass.Setup(bloodFxMaterial);
		renderer.EnqueuePass(bloodFxPass);
	}

	protected override void Dispose(bool disposing)
	{
		CoreUtils.Destroy(bloodFxMaterial);
		bloodFxMaterial = null;
		bloodFxPass = null;
	}

	public void SetBloodFxShader(Shader shader)
	{
		bloodFxShader = shader;
	}
}