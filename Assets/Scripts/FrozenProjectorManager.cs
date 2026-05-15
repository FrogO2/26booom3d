using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static class FrozenProjectorManager
{
	public const int ShaderMaxProjectors = 16;

	private static readonly List<FrozenProjector> Projectors = new List<FrozenProjector>(ShaderMaxProjectors);
	private static readonly int ProjectorVisibilityAtlasId = Shader.PropertyToID("_ProjectorVisibilityAtlas");
	private static int maxRetainedProjectors = ShaderMaxProjectors;
	private static int nextProjectorId = 1;
	private static int nextDepthSliceIndex;
	private static Texture2DArray projectorVisibilityAtlas;
	private static int visibilityAtlasResolution;

	// --- HD Atlas ---
	private static readonly int ProjectorHDAtlasId = Shader.PropertyToID("_ProjectorHDAtlas");
	private static Texture2DArray projectorHDAtlas;
	private static Texture2DArray hdAtlasStub;
	private static int hdAtlasResolution;
	private static Camera captureCamera;
	private static Material captureDepthMaterial;

	public static void SetMaxRetainedProjectors(int maxProjectors)
	{
		maxRetainedProjectors = Mathf.Clamp(maxProjectors, 1, ShaderMaxProjectors);
		TrimToLimit();
	}

	public static int AddProjector(Camera sourceCamera, float projectionDistance, float edgeFeather, float visibleDepthBias, LayerMask projectionMask, int captureResolution)
	{
		if (sourceCamera == null)
		{
			return -1;
		}

		int depthSliceIndex = AcquireDepthSliceIndex();
		FrozenProjector projector = CreateProjectorFromCamera(sourceCamera, projectionDistance, edgeFeather, visibleDepthBias, depthSliceIndex, nextProjectorId++);
		CaptureProjectorVisibility(sourceCamera, projectionMask, captureResolution, projector);
		ClearHDAtlasSlice(depthSliceIndex);

		Projectors.Add(projector);
		TrimToLimit();
		return projector.id;
	}

	public static bool RefreshProjector(int projectorId, Camera sourceCamera, float projectionDistance, float edgeFeather, float visibleDepthBias, LayerMask projectionMask, int captureResolution)
	{
		if (sourceCamera == null || !TryGetProjectorIndex(projectorId, out int projectorIndex))
		{
			return false;
		}

		FrozenProjector currentProjector = Projectors[projectorIndex];
		FrozenProjector refreshedProjector = CreateProjectorFromCamera(
			sourceCamera,
			projectionDistance,
			edgeFeather,
			visibleDepthBias,
			currentProjector.depthSliceIndex,
			currentProjector.id);

		CaptureProjectorVisibility(sourceCamera, projectionMask, captureResolution, refreshedProjector);
		ClearHDAtlasSlice(refreshedProjector.depthSliceIndex);
		Projectors[projectorIndex] = refreshedProjector;
		return true;
	}

	public static void CaptureHighResDepth(int projectorId, int hdResolution, LayerMask captureMask, Shader captureShader)
	{
		if (captureShader == null || !TryGetProjector(projectorId, out FrozenProjector projector))
		{
			return;
		}

		EnsureHDAtlas(hdResolution);

		Camera cam = GetOrCreateCaptureCamera();
		Transform camTransform = cam.transform;
		camTransform.position = projector.position;
		camTransform.rotation = Quaternion.LookRotation(projector.forward, projector.up);
		cam.fieldOfView = Mathf.Atan(projector.tanHalfFov) * 2f * Mathf.Rad2Deg;
		cam.aspect = projector.aspect;
		cam.nearClipPlane = projector.nearDistance;
		cam.farClipPlane = projector.farDistance;
		cam.cullingMask = captureMask;

		Renderer[] visibleRenderers = CollectVisibleRenderers(cam, captureMask);
		if (visibleRenderers.Length == 0)
		{
			ClearHDAtlasSlice(projector.depthSliceIndex, hdResolution);
			return;
		}

		RenderTexture renderTexture = new RenderTexture(hdResolution, hdResolution, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear)
		{
			filterMode = FilterMode.Bilinear,
			wrapMode = TextureWrapMode.Clamp,
		};
		renderTexture.Create();

		Material depthMaterial = GetOrCreateCaptureDepthMaterial(captureShader);
		CommandBuffer cmd = new CommandBuffer { name = "HDDepthCapture_Synchronous" };

		cmd.SetRenderTarget(renderTexture);
		cmd.ClearRenderTarget(true, true, Color.clear);
		cmd.SetGlobalVector("_CaptureNearFar", new Vector4(projector.nearDistance, projector.farDistance, 0f, 0f));
		cmd.SetViewProjectionMatrices(cam.worldToCameraMatrix, cam.projectionMatrix);

		foreach (Renderer visibleRenderer in visibleRenderers)
		{
			if (visibleRenderer == null)
			{
				continue;
			}

			cmd.DrawRenderer(visibleRenderer, depthMaterial, 0, 0);
		}

		Graphics.ExecuteCommandBuffer(cmd);
		cmd.Release();

		UploadRenderTextureToHDAtlas(renderTexture, projector.depthSliceIndex, hdResolution);

		renderTexture.Release();
		Object.Destroy(renderTexture);
	}

	public static void ClearAll()
	{
		Projectors.Clear();
	}

	public static void ApplySharedVisibilityData(Material material)
	{
		if (material == null || projectorVisibilityAtlas == null)
		{
			return;
		}

		material.SetTexture(ProjectorVisibilityAtlasId, projectorVisibilityAtlas);
		material.SetTexture(ProjectorHDAtlasId, GetHDAtlasForBinding());
	}

	public static bool TryGetProjector(int projectorId, out FrozenProjector projector)
	{
		if (TryGetProjectorIndex(projectorId, out int projectorIndex))
		{
			projector = Projectors[projectorIndex];
			return true;
		}

		projector = default;
		return false;
	}

	private static Renderer[] CollectVisibleRenderers(Camera captureSource, LayerMask captureMask)
	{
		Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(captureSource);
		Renderer[] allRenderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
		var visibleRenderers = new List<Renderer>(allRenderers.Length);

		foreach (Renderer renderer in allRenderers)
		{
			if ((captureMask.value & (1 << renderer.gameObject.layer)) == 0)
			{
				continue;
			}

			if (!GeometryUtility.TestPlanesAABB(frustumPlanes, renderer.bounds))
			{
				continue;
			}

			visibleRenderers.Add(renderer);
		}

		return visibleRenderers.ToArray();
	}

	private static void UploadRenderTextureToHDAtlas(RenderTexture renderTexture, int sliceIndex, int hdResolution)
	{
		if (projectorHDAtlas == null || projectorHDAtlas.width != hdResolution || projectorHDAtlas.height != hdResolution)
		{
			return;
		}

		RenderTexture previousActive = RenderTexture.active;
		Texture2D readbackTexture = new Texture2D(hdResolution, hdResolution, TextureFormat.RGBAFloat, false, true);

		RenderTexture.active = renderTexture;
		readbackTexture.ReadPixels(new Rect(0f, 0f, hdResolution, hdResolution), 0, 0, false);
		readbackTexture.Apply(false, false);
		RenderTexture.active = previousActive;

		projectorHDAtlas.SetPixels(readbackTexture.GetPixels(), sliceIndex, 0);
		projectorHDAtlas.Apply(false, false);

		Object.Destroy(readbackTexture);
	}

	private static void ClearHDAtlasSlice(int sliceIndex, int hdResolution)
	{
		if (projectorHDAtlas == null || projectorHDAtlas.width != hdResolution || projectorHDAtlas.height != hdResolution)
		{
			return;
		}

		Color[] clearPixels = new Color[hdResolution * hdResolution];
		projectorHDAtlas.SetPixels(clearPixels, sliceIndex, 0);
		projectorHDAtlas.Apply(false, false);
	}

	private static void ClearHDAtlasSlice(int sliceIndex)
	{
		if (projectorHDAtlas == null || hdAtlasResolution <= 0)
		{
			return;
		}

		ClearHDAtlasSlice(sliceIndex, hdAtlasResolution);
	}

	public static int PopulateProjectorData(
		IReadOnlyList<int> projectorIds,
		Vector4[] positions,
		Vector4[] rights,
		Vector4[] ups,
		Vector4[] forwards,
		Vector4[] params0,
		Vector4[] params1)
	{
		if (projectorIds == null)
		{
			return 0;
		}

		int count = 0;
		int startIndex = Mathf.Max(0, projectorIds.Count - ShaderMaxProjectors);

		for (int i = startIndex; i < projectorIds.Count && count < ShaderMaxProjectors; i++)
		{
			if (!TryGetProjector(projectorIds[i], out FrozenProjector projector))
			{
				continue;
			}

			positions[count] = new Vector4(projector.position.x, projector.position.y, projector.position.z, 1f);
			rights[count] = new Vector4(projector.right.x, projector.right.y, projector.right.z, 0f);
			ups[count] = new Vector4(projector.up.x, projector.up.y, projector.up.z, 0f);
			forwards[count] = new Vector4(projector.forward.x, projector.forward.y, projector.forward.z, 0f);
			params0[count] = new Vector4(projector.tanHalfFov, projector.aspect, projector.nearDistance, projector.farDistance);
			params1[count] = new Vector4(projector.edgeFeather, projector.depthSliceIndex, projector.visibleDepthBias, 0f);
			count++;
		}

		return count;
	}

	private static int AcquireDepthSliceIndex()
	{
		int sliceIndex = nextDepthSliceIndex;
		nextDepthSliceIndex = (nextDepthSliceIndex + 1) % ShaderMaxProjectors;
		return sliceIndex;
	}

	private static FrozenProjector CreateProjectorFromCamera(Camera sourceCamera, float projectionDistance, float edgeFeather, float visibleDepthBias, int depthSliceIndex, int projectorId)
	{
		Transform cameraTransform = sourceCamera.transform;
		float nearDistance = sourceCamera.nearClipPlane;
		float farDistance = Mathf.Clamp(projectionDistance, nearDistance + 0.01f, sourceCamera.farClipPlane);

		return new FrozenProjector
		{
			id = projectorId,
			depthSliceIndex = depthSliceIndex,
			position = cameraTransform.position,
			right = cameraTransform.right.normalized,
			up = cameraTransform.up.normalized,
			forward = cameraTransform.forward.normalized,
			tanHalfFov = Mathf.Tan(sourceCamera.fieldOfView * 0.5f * Mathf.Deg2Rad),
			aspect = sourceCamera.aspect,
			nearDistance = nearDistance,
			farDistance = farDistance,
			edgeFeather = Mathf.Clamp(edgeFeather, 0.01f, 0.5f),
			visibleDepthBias = Mathf.Max(0.001f, visibleDepthBias),
		};
	}

	private static void CaptureProjectorVisibility(Camera sourceCamera, LayerMask projectionMask, int captureResolution, FrozenProjector projector)
	{
		CaptureVisibleDepth(sourceCamera, projectionMask, captureResolution, projector.nearDistance, projector.farDistance, projector.depthSliceIndex);
	}

	private static bool TryGetProjectorIndex(int projectorId, out int projectorIndex)
	{
		for (int i = 0; i < Projectors.Count; i++)
		{
			if (Projectors[i].id == projectorId)
			{
				projectorIndex = i;
				return true;
			}
		}

		projectorIndex = -1;
		return false;
	}

	private static void CaptureVisibleDepth(Camera sourceCamera, LayerMask projectionMask, int captureResolution, float nearDistance, float farDistance, int depthSliceIndex)
	{
		EnsureDepthAtlas(captureResolution);

		Color[] depthPixels = new Color[captureResolution * captureResolution];
		Transform cameraTransform = sourceCamera.transform;
		Vector3 cameraPosition = cameraTransform.position;
		Vector3 cameraForward = cameraTransform.forward;

		for (int y = 0; y < captureResolution; y++)
		{
			float viewportY = (y + 0.5f) / captureResolution;

			for (int x = 0; x < captureResolution; x++)
			{
				float viewportX = (x + 0.5f) / captureResolution;
				Ray ray = sourceCamera.ViewportPointToRay(new Vector3(viewportX, viewportY, 0f));
				int pixelIndex = y * captureResolution + x;

				if (Physics.Raycast(ray, out RaycastHit hit, farDistance, projectionMask, QueryTriggerInteraction.Ignore))
				{
					float visibleProjectedDepth = Vector3.Dot(hit.point - cameraPosition, cameraForward);
					depthPixels[pixelIndex] = new Color(visibleProjectedDepth, 0f, 0f, 1f);
				}
				else
				{
					depthPixels[pixelIndex] = Color.clear;
				}
			}
		}

		projectorVisibilityAtlas.SetPixels(depthPixels, depthSliceIndex, 0);
		projectorVisibilityAtlas.Apply(false, false);
	}

	private static void EnsureHDAtlas(int resolution)
	{
		if (projectorHDAtlas != null && hdAtlasResolution == resolution)
		{
			return;
		}

		if (projectorHDAtlas != null)
		{
			Object.Destroy(projectorHDAtlas);
		}

		hdAtlasResolution = resolution;
		projectorHDAtlas = new Texture2DArray(resolution, resolution, ShaderMaxProjectors, TextureFormat.RGBAFloat, false, true)
		{
			wrapMode = TextureWrapMode.Clamp,
			filterMode = FilterMode.Bilinear,
			anisoLevel = 0,
		};

		// Explicitly clear every slice to zero so the shader's alpha > 0.5 guard never
		// fires on uninitialised GPU memory before the first real capture arrives.
		Color[] clearPixels = new Color[resolution * resolution];
		for (int s = 0; s < ShaderMaxProjectors; s++)
		{
			projectorHDAtlas.SetPixels(clearPixels, s, 0);
		}
		projectorHDAtlas.Apply(false, false);
	}

	// Returns the HD atlas if one has been created, otherwise a 1×1 stub.
	// A stub ensures the shader always receives a valid texture binding.
	private static Texture2DArray GetHDAtlasForBinding()
	{
		if (projectorHDAtlas != null)
		{
			return projectorHDAtlas;
		}

		if (hdAtlasStub == null)
		{
			hdAtlasStub = new Texture2DArray(1, 1, ShaderMaxProjectors, TextureFormat.RGBAFloat, false, true)
			{
				wrapMode = TextureWrapMode.Clamp,
				filterMode = FilterMode.Bilinear,
			};
			hdAtlasStub.Apply(false, false);
		}

		return hdAtlasStub;
	}

	private static Camera GetOrCreateCaptureCamera()
	{
		if (captureCamera != null)
		{
			return captureCamera;
		}

		GameObject go = new GameObject("__HDDepthCapture__")
		{
			hideFlags = HideFlags.HideAndDontSave,
		};
		captureCamera = go.AddComponent<Camera>();
		captureCamera.enabled = false;

		Application.quitting += () =>
		{
			if (captureCamera != null)
			{
				Object.Destroy(captureCamera.gameObject);
				captureCamera = null;
			}
			if (captureDepthMaterial != null)
			{
				Object.Destroy(captureDepthMaterial);
				captureDepthMaterial = null;
			}
		};

		return captureCamera;
	}

	// Cache the Material built from captureShader to avoid per-spray allocation.
	private static Material GetOrCreateCaptureDepthMaterial(Shader captureShader)
	{
		if (captureDepthMaterial != null && captureDepthMaterial.shader == captureShader)
		{
			return captureDepthMaterial;
		}

		if (captureDepthMaterial != null)
		{
			Object.Destroy(captureDepthMaterial);
		}

		captureDepthMaterial = new Material(captureShader) { hideFlags = HideFlags.HideAndDontSave };
		return captureDepthMaterial;
	}

	private static void EnsureDepthAtlas(int captureResolution)
	{
		if (projectorVisibilityAtlas != null && visibilityAtlasResolution == captureResolution)
		{
			return;
		}

		if (projectorVisibilityAtlas != null)
		{
			Object.Destroy(projectorVisibilityAtlas);
		}

		visibilityAtlasResolution = captureResolution;
		projectorVisibilityAtlas = new Texture2DArray(captureResolution, captureResolution, ShaderMaxProjectors, TextureFormat.RGBAHalf, false, true)
		{
			wrapMode = TextureWrapMode.Clamp,
			filterMode = FilterMode.Bilinear,
			anisoLevel = 0,
		};
	}

	private static void TrimToLimit()
	{
		while (Projectors.Count > maxRetainedProjectors)
		{
			Projectors.RemoveAt(0);
		}
	}

	public struct FrozenProjector
	{
		public int id;
		public int depthSliceIndex;
		public Vector3 position;
		public Vector3 right;
		public Vector3 up;
		public Vector3 forward;
		public float tanHalfFov;
		public float aspect;
		public float nearDistance;
		public float farDistance;
		public float edgeFeather;
		public float visibleDepthBias;
	}
}