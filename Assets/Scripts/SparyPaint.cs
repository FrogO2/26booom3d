using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

public class SprayPaint : MonoBehaviour
{
	[Header("References")]
	[SerializeField] private Camera targetCamera;

	[Header("Reveal")]
	[SerializeField, Min(0.5f)] private float projectionDistance = 30f;
	[SerializeField, Range(0.01f, 0.5f)] private float edgeFeather = 0.12f;
	[SerializeField] private Color hiddenColor = Color.black;
	[SerializeField, Min(16)] private int captureResolution = 64;
	[SerializeField, Min(0.001f)] private float visibleDepthBias = 0.05f;
	[SerializeField] private LayerMask projectionMask = ~0;
	[SerializeField, Min(1)] private int maxRetainedSprays = 16;
	[SerializeField] private bool refreshLatestProjectorEveryFrame;

	[Header("Blood FX")]
	[SerializeField] private Color bloodColor = new Color(1f, 0f, 0f, 0.8f);
	[SerializeField] private Material bloodMaterial;
	[SerializeField] private Texture bloodTexture;
	[SerializeField] private Vector2 bloodTextureScale = Vector2.one;
	[SerializeField] private Vector2 bloodTextureOffset = Vector2.zero;

	[Header("HD Depth")]
	[SerializeField, FormerlySerializedAs("asyncHighResResolution"), Min(16)] private int highResCaptureResolution = 256;
	[SerializeField] private Shader depthCaptureShader;

	private int latestProjectorId = -1;

	private void Awake()
	{
		ExcludeProtectedLayers();

		if (targetCamera == null)
		{
			targetCamera = Camera.main;
		}

		FrozenProjectorManager.SetMaxRetainedProjectors(maxRetainedSprays);
		BloodRevealManager.SetHiddenColor(hiddenColor);
	}

	private void Update()
	{
		Camera projectionCamera = GetProjectionCamera();

		if (projectionCamera == null)
		{
			return;
		}

		if (refreshLatestProjectorEveryFrame && latestProjectorId >= 0)
		{
			if (!FrozenProjectorManager.RefreshProjector(latestProjectorId, projectionCamera, projectionDistance, edgeFeather, visibleDepthBias, projectionMask, captureResolution))
			{
				latestProjectorId = -1;
			}
		}
	}

	public int TriggerSprayFromCurrentCamera()
	{
		return TriggerSpray(GetProjectionCamera());
	}

	private int TriggerSpray(Camera projectionCamera)
	{
		if (projectionCamera == null)
		{
			return -1;
		}

		FrozenProjectorManager.SetMaxRetainedProjectors(maxRetainedSprays);
		BloodRevealManager.SetHiddenColor(hiddenColor);

		int projectorId = FrozenProjectorManager.AddProjector(projectionCamera, projectionDistance, edgeFeather, visibleDepthBias, projectionMask, captureResolution);
		if (projectorId < 0)
		{
			return -1;
		}

		latestProjectorId = projectorId;
		BloodRevealManager.AddReveal(projectorId);
		BloodFxManager.AddBloodFx(projectorId, ResolveProjectionTexture(), ResolveProjectionColor(), bloodTextureScale, bloodTextureOffset);

		return projectorId;
	}

	public void CaptureHighResForProjector(int projectorId)
	{
		if (projectorId < 0 || depthCaptureShader == null)
		{
			return;
		}

		FrozenProjectorManager.CaptureHighResDepth(projectorId, highResCaptureResolution, projectionMask, depthCaptureShader);
	}

	private Camera GetProjectionCamera()
	{
		Camera candidate = targetCamera != null ? targetCamera : Camera.main;
		if (candidate == null)
		{
			return null;
		}

		UniversalAdditionalCameraData additionalCameraData = candidate.GetUniversalAdditionalCameraData();
		if (additionalCameraData == null || additionalCameraData.renderType != CameraRenderType.Overlay)
		{
			return candidate;
		}

		Camera baseCamera = FindBaseCameraForOverlay(candidate);
		return baseCamera != null ? baseCamera : candidate;
	}

	private static Camera FindBaseCameraForOverlay(Camera overlayCamera)
	{
		Camera[] cameras = Camera.allCameras;

		for (int i = 0; i < cameras.Length; i++)
		{
			Camera candidate = cameras[i];
			if (candidate == null || candidate == overlayCamera)
			{
				continue;
			}

			UniversalAdditionalCameraData additionalCameraData = candidate.GetUniversalAdditionalCameraData();
			if (additionalCameraData == null || additionalCameraData.renderType != CameraRenderType.Base)
			{
				continue;
			}

			if (additionalCameraData.cameraStack != null && additionalCameraData.cameraStack.Contains(overlayCamera))
			{
				return candidate;
			}
		}

		return Camera.main != overlayCamera ? Camera.main : null;
	}

	public void ClearAllSpray()
	{
		BloodFxManager.ClearAll();
		BloodRevealManager.ClearAll();
		FrozenProjectorManager.ClearAll();
		latestProjectorId = -1;
	}

	private void OnValidate()
	{
		projectionDistance = Mathf.Max(0.5f, projectionDistance);
		edgeFeather = Mathf.Clamp(edgeFeather, 0.01f, 0.5f);
		captureResolution = Mathf.Max(16, captureResolution);
		visibleDepthBias = Mathf.Max(0.001f, visibleDepthBias);
		maxRetainedSprays = Mathf.Max(1, maxRetainedSprays);
		highResCaptureResolution = Mathf.Max(16, highResCaptureResolution);
		ExcludeProtectedLayers();
	}

	private void ExcludeProtectedLayers()
	{
		projectionMask = RemoveLayerFromMask(projectionMask, "Unpaintable");
		projectionMask = RemoveLayerFromMask(projectionMask, "Unpaintable&Unmaskable");
	}

	private static LayerMask RemoveLayerFromMask(LayerMask mask, string layerName)
	{
		int layer = LayerMask.NameToLayer(layerName);
		if (layer < 0)
		{
			return mask;
		}

		return mask & ~(1 << layer);
	}

	private Texture ResolveProjectionTexture()
	{
		if (bloodTexture != null)
		{
			return bloodTexture;
		}

		if (bloodMaterial == null)
		{
			return null;
		}

		if (bloodMaterial.HasProperty("_BaseMap"))
		{
			Texture baseMap = bloodMaterial.GetTexture("_BaseMap");
			if (baseMap != null)
			{
				return baseMap;
			}
		}

		if (bloodMaterial.HasProperty("_MainTex"))
		{
			return bloodMaterial.GetTexture("_MainTex");
		}

		return null;
	}

	private Color ResolveProjectionColor()
	{
		Color resolvedColor = bloodColor;

		if (bloodMaterial == null)
		{
			return resolvedColor;
		}

		if (bloodMaterial.HasProperty("_BaseColor"))
		{
			resolvedColor *= bloodMaterial.GetColor("_BaseColor");
		}
		else if (bloodMaterial.HasProperty("_Color"))
		{
			resolvedColor *= bloodMaterial.GetColor("_Color");
		}

		return resolvedColor;
	}
}
