using UnityEngine;

public static class ArenaTextStyleUtility
{
	public const string SequentialOverlayGroupKey = "SequentialOverlayMessage";
	public const float DefaultContrastBias = 0.05f;
	public const float DefaultContrastBlendWidth = 0.22f;

	public static readonly Color DefaultForegroundColor = new Color(1f, 1f, 1f, 0.98f);
	public static readonly Color AlertForegroundColor = new Color(0.92f, 0.17f, 0.15f, 0.98f);
	public static readonly Color DefaultOutlineColor = new Color(0f, 0f, 0f, 0.72f);

	public static Color ResolveForeground(Camera camera)
	{
		return ResolveForeground(camera, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
	}

	public static Color ResolveForeground(Camera camera, Vector2 screenPoint)
	{
		return ResolveForeground(camera, screenPoint, DefaultForegroundColor, AlertForegroundColor, DefaultContrastBias);
	}

	public static Color ResolveForeground(Camera camera, Vector2 screenPoint, Color primaryColor, Color secondaryColor, float secondaryBias)
	{
		return ResolveForeground(camera, screenPoint, primaryColor, secondaryColor, secondaryBias, 0f);
	}

	public static Color ResolveForeground(Camera camera, Vector2 screenPoint, Color primaryColor, Color secondaryColor, float secondaryBias, float blendWidth)
	{
		Color backgroundColor = SampleBackgroundColor(camera, screenPoint);
		float primaryContrastRatio = ComputeContrastRatio(backgroundColor, primaryColor);
		float secondaryContrastRatio = ComputeContrastRatio(backgroundColor, secondaryColor);
		if (IsNearWhite(backgroundColor))
		{
			return secondaryColor;
		}

		float contrastDelta = secondaryContrastRatio - (primaryContrastRatio + secondaryBias);
		if (blendWidth <= 0.0001f)
		{
			return contrastDelta > 0f ? secondaryColor : primaryColor;
		}

		float blendFactor = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-blendWidth, blendWidth, contrastDelta));
		return Color.Lerp(primaryColor, secondaryColor, blendFactor);
	}

	public static Color ResolvePromptColor(Camera camera, ArenaPromptColorMode colorMode, Color solidColor)
	{
		if (colorMode == ArenaPromptColorMode.Solid)
		{
			return solidColor;
		}

		if (colorMode == ArenaPromptColorMode.AdaptiveContrast)
		{
			return DefaultForegroundColor;
		}

		Color backgroundColor = SampleSceneColor(camera);
		Color.RGBToHSV(backgroundColor, out float hue, out float saturation, out float value);

		float baseHue = Mathf.Repeat(hue + 0.5f, 1f);
		if (colorMode == ArenaPromptColorMode.AdaptiveHueShift)
		{
			baseHue = Mathf.Repeat(baseHue + Time.unscaledTime * 0.18f, 1f);
		}

		float targetSaturation = Mathf.Clamp01(0.65f + (1f - saturation) * 0.35f);
		float targetValue = value > 0.55f ? 0.12f : 1f;
		return Color.HSVToRGB(baseHue, targetSaturation, targetValue);
	}

	public static Color SampleSceneColor(Camera camera)
	{
		return SampleBackgroundColor(camera, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
	}

	private static bool IsNearWhite(Color color)
	{
		return Mathf.Abs(color.r - 1f) + Mathf.Abs(color.g - 1f) + Mathf.Abs(color.b - 1f) < 0.72f;
	}

	private static float ComputeContrastRatio(Color backgroundColor, Color foregroundColor)
	{
		float backgroundLuminance = ComputeRelativeLuminance(backgroundColor);
		float foregroundLuminance = ComputeRelativeLuminance(foregroundColor);
		float brighter = Mathf.Max(backgroundLuminance, foregroundLuminance);
		float darker = Mathf.Min(backgroundLuminance, foregroundLuminance);
		return (brighter + 0.05f) / (darker + 0.05f);
	}

	private static float ComputeRelativeLuminance(Color color)
	{
		return 0.2126f * LinearizeChannel(color.r) +
			0.7152f * LinearizeChannel(color.g) +
			0.0722f * LinearizeChannel(color.b);
	}

	private static float LinearizeChannel(float value)
	{
		return value <= 0.03928f ? value / 12.92f : Mathf.Pow((value + 0.055f) / 1.055f, 2.4f);
	}

	private static Color SampleBackgroundColor(Camera camera, Vector2 screenPoint)
	{
		Camera sampledCamera = camera != null ? camera : Camera.main;
		if (sampledCamera == null)
		{
			return Color.black;
		}

		Ray ray = sampledCamera.ScreenPointToRay(screenPoint);
		if (Physics.Raycast(ray, out RaycastHit hit, 250f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
		{
			Renderer renderer = hit.collider.GetComponentInParent<Renderer>();
			if (renderer != null)
			{
				Material sharedMaterial = renderer.sharedMaterial;
				if (sharedMaterial != null)
				{
					if (sharedMaterial.HasProperty("_BaseColor"))
					{
						return sharedMaterial.GetColor("_BaseColor");
					}

					if (sharedMaterial.HasProperty("_Color"))
					{
						return sharedMaterial.GetColor("_Color");
					}
				}
			}
		}

		return RenderSettings.ambientSkyColor.maxColorComponent > 0f ? RenderSettings.ambientSkyColor : sampledCamera.backgroundColor;
	}
}