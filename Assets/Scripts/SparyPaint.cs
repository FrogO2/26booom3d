using UnityEngine;
using UnityEngine.InputSystem;

public class SprayPaint : MonoBehaviour
{
	[Header("References")]
	[SerializeField] private Camera targetCamera;
	[SerializeField] private InputActionAsset inputActions;
	[SerializeField] private string actionMapName = "Player";
	[SerializeField] private string attackActionName = "Attack";

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

	[Header("Async HD Depth")]
	[SerializeField] private bool enableAsyncHighRes;
	[SerializeField, Min(16)] private int asyncHighResResolution = 256;
	[SerializeField] private Shader depthCaptureShader;
	[SerializeField, Min(1), Tooltip("每帧渲染的扫描行数。越小则每帧 GPU 开销越低，但 HD 数据就绪所需帧数越多。")]
	private int hdCaptureRowsPerFrame = 64;

	[Header("Render Time Assist")]
	[SerializeField] private bool slowTimeWhileProjectorRendering = true;
	[SerializeField, Range(0f, 1f)] private float renderPendingTimeScale = 0.08f;
	[SerializeField, Min(0f)] private float initialHitStopDuration = 0.04f;
	[SerializeField, Min(0.1f)] private float timeScaleRestoreSpeed = 6f;

	private InputAction attackAction;
	private int latestProjectorId = -1;
	private float originalTimeScale = 1f;
	private float originalFixedDeltaTime;
	private float hitStopTimer;
	private bool waitingForRenderCompletion;
	private bool isManagingTimeScale;

	private void Awake()
	{
		originalFixedDeltaTime = Time.fixedDeltaTime;

		if (targetCamera == null)
		{
			targetCamera = Camera.main;
		}

		BindInputAction();
		FrozenProjectorManager.SetMaxRetainedProjectors(maxRetainedSprays);
		BloodRevealManager.SetHiddenColor(hiddenColor);
	}

	private void OnEnable()
	{
		attackAction?.Enable();
	}

	private void OnDisable()
	{
		attackAction?.Disable();
		RestoreManagedTimeScaleImmediate();
	}

	private void Update()
	{
		FrozenProjectorManager.Tick(hdCaptureRowsPerFrame);
		UpdateRenderTimeAssist();

		if (targetCamera == null)
		{
			return;
		}

		if (WasAttackPressedThisFrame())
		{
			TriggerSpray();
			return;
		}

		if (refreshLatestProjectorEveryFrame && latestProjectorId >= 0)
		{
			if (!FrozenProjectorManager.RefreshProjector(latestProjectorId, targetCamera, projectionDistance, edgeFeather, visibleDepthBias, projectionMask, captureResolution))
			{
				latestProjectorId = -1;
			}
		}
	}

	private void BindInputAction()
	{
		if (inputActions == null)
		{
			Debug.LogWarning($"{nameof(SprayPaint)} on {name} has no InputActionAsset assigned.", this);
			return;
		}

		InputActionMap actionMap = inputActions.FindActionMap(actionMapName, true);
		attackAction = actionMap.FindAction(attackActionName, true);
	}

	private bool WasAttackPressedThisFrame()
	{
		if (attackAction != null && attackAction.WasPressedThisFrame())
		{
			return true;
		}

		if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
		{
			return true;
		}

		if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
		{
			return true;
		}

		return Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame;
	}

	private void TriggerSpray()
	{
		FrozenProjectorManager.SetMaxRetainedProjectors(maxRetainedSprays);
		BloodRevealManager.SetHiddenColor(hiddenColor);

		bool useFastInitialVisibility = enableAsyncHighRes && depthCaptureShader != null;
		int projectorId = FrozenProjectorManager.AddProjector(targetCamera, projectionDistance, edgeFeather, visibleDepthBias, projectionMask, captureResolution, useFastInitialVisibility);
		if (projectorId < 0)
		{
			return;
		}

		latestProjectorId = projectorId;
		BloodRevealManager.AddReveal(projectorId);
		BloodFxManager.AddBloodFx(projectorId, ResolveProjectionTexture(), ResolveProjectionColor(), bloodTextureScale, bloodTextureOffset);

		if (enableAsyncHighRes && depthCaptureShader != null)
		{
			FrozenProjectorManager.ScheduleAsyncHDCapture(projectorId, asyncHighResResolution, projectionMask, depthCaptureShader);

			if (slowTimeWhileProjectorRendering)
			{
				waitingForRenderCompletion = true;
				hitStopTimer = Mathf.Max(hitStopTimer, initialHitStopDuration);
				ApplyManagedTimeScale(initialHitStopDuration > 0f ? 0f : renderPendingTimeScale);
			}
		}
	}

	private void UpdateRenderTimeAssist()
	{
		if (!slowTimeWhileProjectorRendering)
		{
			waitingForRenderCompletion = false;
			hitStopTimer = 0f;
			RestoreManagedTimeScaleImmediate();
			return;
		}

		if (!waitingForRenderCompletion && !isManagingTimeScale)
		{
			return;
		}

		if (hitStopTimer > 0f)
		{
			hitStopTimer -= Time.unscaledDeltaTime;
			ApplyManagedTimeScale(0f);
			return;
		}

		if (waitingForRenderCompletion && FrozenProjectorManager.HasPendingAsyncWork)
		{
			ApplyManagedTimeScale(renderPendingTimeScale);
			return;
		}

		waitingForRenderCompletion = false;
		RestoreManagedTimeScaleStep();
	}

	private void ApplyManagedTimeScale(float targetTimeScale)
	{
		float clampedTimeScale = Mathf.Clamp01(targetTimeScale);
		if (!isManagingTimeScale)
		{
			originalTimeScale = Time.timeScale;
			originalFixedDeltaTime = Time.fixedDeltaTime;
			isManagingTimeScale = true;
		}

		Time.timeScale = clampedTimeScale;
		float scaleRatio = originalTimeScale > 0.0001f ? clampedTimeScale / originalTimeScale : clampedTimeScale;
		Time.fixedDeltaTime = clampedTimeScale > 0f
			? Mathf.Max(0.0001f, originalFixedDeltaTime * scaleRatio)
			: 0.0001f;
	}

	private void RestoreManagedTimeScaleStep()
	{
		if (!isManagingTimeScale)
		{
			return;
		}

		float restoredTimeScale = Mathf.MoveTowards(Time.timeScale, originalTimeScale, timeScaleRestoreSpeed * Time.unscaledDeltaTime);
		Time.timeScale = restoredTimeScale;

		float restoredFixedDeltaTime = Mathf.MoveTowards(Time.fixedDeltaTime, originalFixedDeltaTime, timeScaleRestoreSpeed * originalFixedDeltaTime * Time.unscaledDeltaTime);
		Time.fixedDeltaTime = restoredFixedDeltaTime;

		if (Mathf.Approximately(restoredTimeScale, originalTimeScale) && Mathf.Approximately(restoredFixedDeltaTime, originalFixedDeltaTime))
		{
			RestoreManagedTimeScaleImmediate();
		}
	}

	private void RestoreManagedTimeScaleImmediate()
	{
		if (!isManagingTimeScale)
		{
			return;
		}

		Time.timeScale = originalTimeScale;
		Time.fixedDeltaTime = originalFixedDeltaTime;
		isManagingTimeScale = false;
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
		asyncHighResResolution = Mathf.Max(16, asyncHighResResolution);
		hdCaptureRowsPerFrame = Mathf.Max(1, hdCaptureRowsPerFrame);
		renderPendingTimeScale = Mathf.Clamp01(renderPendingTimeScale);
		initialHitStopDuration = Mathf.Max(0f, initialHitStopDuration);
		timeScaleRestoreSpeed = Mathf.Max(0.1f, timeScaleRestoreSpeed);
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
