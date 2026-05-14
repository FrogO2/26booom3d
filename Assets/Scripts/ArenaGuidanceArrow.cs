using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Arena/Guidance Arrow")]
public class ArenaGuidanceArrow : MonoBehaviour
{
	private readonly List<Renderer> arrowRenderers = new List<Renderer>();
	private Vector3 basePosition;
	private bool hasBasePosition;

	private void Awake()
	{
		EnsureSceneBuilt();
	}

	private void Update()
	{
		if (!gameObject.activeSelf)
		{
			return;
		}

		Animate();
	}

	public void EnsureSceneBuilt()
	{
		if (!hasBasePosition)
		{
			basePosition = transform.position;
			hasBasePosition = true;
		}

		NormalizeGeometry();
		CacheRenderers();
	}

	public void SetBasePosition(Vector3 worldPosition)
	{
		transform.position = worldPosition;
		basePosition = worldPosition;
		hasBasePosition = true;
	}

	public void Show()
	{
		EnsureSceneBuilt();
		gameObject.SetActive(true);
	}

	public void Hide()
	{
		if (!hasBasePosition)
		{
			basePosition = transform.position;
			hasBasePosition = true;
		}

		gameObject.SetActive(false);
		transform.position = basePosition;
	}

	public void ResetIndicator()
	{
		Hide();
	}

	private void CacheRenderers()
	{
		arrowRenderers.Clear();
		Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
		for (int i = 0; i < renderers.Length; i++)
		{
			if (renderers[i] != null)
			{
				arrowRenderers.Add(renderers[i]);
			}
		}
	}

	private void NormalizeGeometry()
	{
		Transform shaft = transform.Find("Arrow Shaft");
		if (shaft != null)
		{
			shaft.position = transform.position + new Vector3(0f, 0f, -0.12f);
			shaft.rotation = Quaternion.identity;
			shaft.localScale = new Vector3(0.9f, 0.12f, 3.3f);
			DisableCollider(shaft.gameObject);
		}

		Transform headLeft = transform.Find("Arrow Head Left");
		if (headLeft != null)
		{
			headLeft.position = transform.position + new Vector3(-0.52f, 0f, 1.34f);
			headLeft.rotation = Quaternion.Euler(0f, 38f, 0f);
			headLeft.localScale = new Vector3(0.64f, 0.12f, 1.72f);
			DisableCollider(headLeft.gameObject);
		}

		Transform headRight = transform.Find("Arrow Head Right");
		if (headRight != null)
		{
			headRight.position = transform.position + new Vector3(0.52f, 0f, 1.34f);
			headRight.rotation = Quaternion.Euler(0f, -38f, 0f);
			headRight.localScale = new Vector3(0.64f, 0.12f, 1.72f);
			DisableCollider(headRight.gameObject);
		}
	}

	private void Animate()
	{
		float hue = Mathf.Repeat(Time.unscaledTime * 0.22f, 1f);
		Color animatedColor = Color.HSVToRGB(hue, 0.85f, 1f);
		float bob = Mathf.Sin(Time.unscaledTime * 2.2f) * 0.18f;
		transform.position = basePosition + new Vector3(0f, 0.18f + bob, 0f);

		for (int i = 0; i < arrowRenderers.Count; i++)
		{
			Renderer renderer = arrowRenderers[i];
			if (renderer == null)
			{
				continue;
			}

			Material material = renderer.sharedMaterial;
			if (material == null)
			{
				continue;
			}

			if (material.HasProperty("_BaseColor"))
			{
				material.SetColor("_BaseColor", animatedColor);
			}
			else if (material.HasProperty("_Color"))
			{
				material.SetColor("_Color", animatedColor);
			}

			if (material.HasProperty("_EmissionColor"))
			{
				material.SetColor("_EmissionColor", animatedColor * 0.6f);
			}
		}
	}

	private static void DisableCollider(GameObject gameObject)
	{
		Collider collider = gameObject.GetComponent<Collider>();
		if (collider != null)
		{
			collider.enabled = false;
		}
	}
}
