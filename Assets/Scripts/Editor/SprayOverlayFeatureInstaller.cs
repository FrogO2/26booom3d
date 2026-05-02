#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
public static class BloodProjectorFeatureInstaller
{
	private const string RendererAssetPath = "Assets/Settings/PC_Renderer.asset";
	private const string RevealShaderPath = "Assets/Arts/Shaders/BloodRevealMask.shader";
	private const string BloodFxShaderPath = "Assets/Arts/Shaders/BloodProjectorFx.shader";

	static BloodProjectorFeatureInstaller()
	{
		EditorApplication.delayCall += EnsureInstalled;
	}

	[MenuItem("Tools/Blood Projector/Install Renderer Features")]
	public static void EnsureInstalled()
	{
		if (EditorApplication.isCompiling || EditorApplication.isUpdating)
		{
			return;
		}

		UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererAssetPath);
		if (rendererData == null)
		{
			Debug.LogWarning($"{nameof(BloodProjectorFeatureInstaller)} could not find renderer asset at {RendererAssetPath}.");
			return;
		}

		Shader revealShader = AssetDatabase.LoadAssetAtPath<Shader>(RevealShaderPath);
		Shader bloodFxShader = AssetDatabase.LoadAssetAtPath<Shader>(BloodFxShaderPath);

		SerializedObject serializedRenderer = new SerializedObject(rendererData);
		SerializedProperty featuresProperty = serializedRenderer.FindProperty("m_RendererFeatures");
		SerializedProperty featureMapProperty = serializedRenderer.FindProperty("m_RendererFeatureMap");
		RemoveLegacyAndMissingFeatures(featuresProperty, featureMapProperty);

		BloodRevealRendererFeature revealFeature = GetOrCreateFeature<BloodRevealRendererFeature>(rendererData, featuresProperty, featureMapProperty);
		BloodFxRendererFeature bloodFxFeature = GetOrCreateFeature<BloodFxRendererFeature>(rendererData, featuresProperty, featureMapProperty);

		if (revealShader != null)
		{
			revealFeature.SetRevealShader(revealShader);
			EditorUtility.SetDirty(revealFeature);
		}

		if (bloodFxShader != null)
		{
			bloodFxFeature.SetBloodFxShader(bloodFxShader);
			EditorUtility.SetDirty(bloodFxFeature);
		}

		serializedRenderer.ApplyModifiedProperties();
		rendererData.SetDirty();
		EditorUtility.SetDirty(rendererData);
		AssetDatabase.SaveAssets();
		AssetDatabase.ImportAsset(RendererAssetPath);
	}

	private static void RemoveLegacyAndMissingFeatures(SerializedProperty featuresProperty, SerializedProperty featureMapProperty)
	{
		for (int i = featuresProperty.arraySize - 1; i >= 0; i--)
		{
			Object featureObject = featuresProperty.GetArrayElementAtIndex(i).objectReferenceValue;
			if (featureObject == null || featureObject.GetType().Name == "SprayOverlayRendererFeature")
			{
				featuresProperty.DeleteArrayElementAtIndex(i);
				featureMapProperty.DeleteArrayElementAtIndex(i);
			}
		}
	}

	private static T GetOrCreateFeature<T>(UniversalRendererData rendererData, SerializedProperty featuresProperty, SerializedProperty featureMapProperty)
		where T : ScriptableRendererFeature
	{
		if (rendererData.TryGetRendererFeature(out T existingFeature))
		{
			return existingFeature;
		}

		T feature = ScriptableObject.CreateInstance<T>();
		feature.name = typeof(T).Name;
		AssetDatabase.AddObjectToAsset(feature, rendererData);
		AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

		featuresProperty.arraySize++;
		featuresProperty.GetArrayElementAtIndex(featuresProperty.arraySize - 1).objectReferenceValue = feature;

		featureMapProperty.arraySize++;
		featureMapProperty.GetArrayElementAtIndex(featureMapProperty.arraySize - 1).longValue = localId;

		EditorUtility.SetDirty(feature);
		return feature;
	}
}
#endif