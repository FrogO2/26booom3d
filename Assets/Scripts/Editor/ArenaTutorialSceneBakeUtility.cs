#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class ArenaTutorialSceneBakeUtility
{
	private const string ArenaTutorialScenePath = "Assets/Scenes/ArenaTutorialScene.unity";
	private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
	private const string RuntimeRootObjectName = "Arena Tutorial Runtime";
	private const string CourseFloorObjectName = "Course Floor";
	private const string AuditOutputPath = "Temp/arena-audit-report.txt";

	[MenuItem("Tools/Arena Tutorial/Bake All Controllers Into Scene")]
	private static void BakeAllControllersIntoScene()
	{
		ArenaTutorialSceneController[] controllers = Object.FindObjectsByType<ArenaTutorialSceneController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
		if (controllers.Length == 0)
		{
			Debug.LogWarning($"{nameof(ArenaTutorialSceneBakeUtility)} could not find any {nameof(ArenaTutorialSceneController)} instances in the open scenes.");
			return;
		}

		for (int i = 0; i < controllers.Length; i++)
		{
			BakeController(controllers[i]);
		}

		Debug.Log($"Baked arena tutorial content into {controllers.Length} controller(s) across the open scenes.");
	}

	[MenuItem("Tools/Arena Tutorial/Clear Baked Content From Scene")]
	private static void ClearAllBakedContentFromScene()
	{
		ArenaTutorialSceneController[] controllers = Object.FindObjectsByType<ArenaTutorialSceneController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
		if (controllers.Length == 0)
		{
			Debug.LogWarning($"{nameof(ArenaTutorialSceneBakeUtility)} could not find any {nameof(ArenaTutorialSceneController)} instances in the open scenes.");
			return;
		}

		for (int i = 0; i < controllers.Length; i++)
		{
			ClearController(controllers[i]);
		}

		Debug.Log($"Cleared baked arena tutorial content from {controllers.Length} controller(s) across the open scenes.");
	}

	[MenuItem("Tools/Arena Tutorial/Validate Open Arena Setup")]
	private static void ValidateOpenArenaSetup()
	{
		Debug.Log(BuildSceneAuditReport(SceneManager.GetActiveScene()));
	}

	[MenuItem("Tools/Arena Tutorial/Normalize Open Arena Hierarchy")]
	private static void NormalizeOpenArenaHierarchy()
	{
		ArenaTutorialSceneController[] controllers = Object.FindObjectsByType<ArenaTutorialSceneController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
		if (controllers.Length == 0)
		{
			Debug.LogWarning($"{nameof(ArenaTutorialSceneBakeUtility)} could not find any {nameof(ArenaTutorialSceneController)} instances in the open scenes.");
			return;
		}

		int normalizedCount = 0;
		for (int i = 0; i < controllers.Length; i++)
		{
			if (NormalizeControllerHierarchy(controllers[i]))
			{
				normalizedCount++;
				EditorUtility.SetDirty(controllers[i]);
				EditorSceneManager.MarkSceneDirty(controllers[i].gameObject.scene);
			}
		}

		Debug.Log($"Normalized arena hierarchy for {normalizedCount} controller(s).");
	}

	public static void AuditReferenceScenes()
	{
		try
		{
			StringBuilder report = new StringBuilder();
			report.AppendLine(BuildSceneAuditReport(OpenSceneForAudit(ArenaTutorialScenePath)));
			report.AppendLine(BuildSceneAuditReport(OpenSceneForAudit(SampleScenePath)));
			File.WriteAllText(AuditOutputPath, report.ToString());
			Debug.Log(report.ToString());
		}
		catch (Exception exception)
		{
			File.WriteAllText(AuditOutputPath, exception.ToString());
			throw;
		}
	}

	[MenuItem("CONTEXT/ArenaTutorialSceneController/Bake Generated Content Into Scene")]
	private static void BakeFromContext(MenuCommand command)
	{
		BakeController(command.context as ArenaTutorialSceneController);
	}

	[MenuItem("CONTEXT/ArenaTutorialSceneController/Clear Baked Generated Content")]
	private static void ClearFromContext(MenuCommand command)
	{
		ClearController(command.context as ArenaTutorialSceneController);
	}

	private static void BakeController(ArenaTutorialSceneController controller)
	{
		if (controller == null)
		{
			return;
		}

		controller.BakeGeneratedContentIntoScene();

		Transform introObject = controller.transform.Find("Scene Intro Overlay");
		if (introObject != null)
		{
			SceneIntroOverlay introComponent = introObject.GetComponent<SceneIntroOverlay>();
			if (introComponent != null)
			{
				introComponent.ConfigureArenaDefaults();
			}
		}

		EditorUtility.SetDirty(controller);
		EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
	}

	private static void ClearController(ArenaTutorialSceneController controller)
	{
		if (controller == null)
		{
			return;
		}

		controller.ClearGeneratedContentFromScene();
		EditorUtility.SetDirty(controller);
		EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
	}

	private static Scene OpenSceneForAudit(string scenePath)
	{
		return EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
	}

	private static bool NormalizeControllerHierarchy(ArenaTutorialSceneController controller)
	{
		if (controller == null)
		{
			return false;
		}

		bool changed = false;
		changed |= ReparentNamedSceneObject(controller, RuntimeRootObjectName);
		changed |= ReparentNamedSceneObject(controller, "Arena Tutorial UI");
		changed |= ReparentNamedSceneObject(controller, "Run Timer System");
		changed |= ReparentNamedSceneObject(controller, "Scene Intro Overlay");

		Transform runtimeRoot = controller.transform.Find(RuntimeRootObjectName);
		if (runtimeRoot != null)
		{
			Transform floor = runtimeRoot.Find(CourseFloorObjectName);
			if (floor != null)
			{
				SerializedObject serializedController = new SerializedObject(controller);
				SerializedProperty courseOrigin = serializedController.FindProperty("courseOrigin");
				Vector3 expectedCourseOrigin = floor.position - new Vector3(0f, -0.5f, 42f);
				if (courseOrigin != null && courseOrigin.vector3Value != expectedCourseOrigin)
				{
					courseOrigin.vector3Value = expectedCourseOrigin;
					serializedController.ApplyModifiedProperties();
					changed = true;
				}
			}
		}

		return changed;
	}

	private static bool ReparentNamedSceneObject(ArenaTutorialSceneController controller, string objectName)
	{
		Transform directChild = controller.transform.Find(objectName);
		if (directChild != null)
		{
			return false;
		}

		Transform sceneObject = FindSceneObjectByName(controller.gameObject.scene, objectName);
		if (sceneObject == null || sceneObject == controller.transform)
		{
			return false;
		}

		sceneObject.SetParent(controller.transform, true);
		return true;
	}

	private static string BuildSceneAuditReport(Scene scene)
	{
		StringBuilder report = new StringBuilder();
		report.AppendLine($"=== Arena Tutorial Scene Audit: {scene.path} ===");

		ArenaTutorialSceneController[] controllers = Object.FindObjectsByType<ArenaTutorialSceneController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
		report.AppendLine($"Controllers: {controllers.Length}");

		CharacterController[] characterControllers = Object.FindObjectsByType<CharacterController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
		report.AppendLine($"CharacterControllers: {characterControllers.Length}");

		Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
		report.AppendLine($"Cameras: {cameras.Length}");

		List<Transform> runtimeRoots = FindSceneObjectsByName(RuntimeRootObjectName);
		report.AppendLine($"Runtime roots named '{RuntimeRootObjectName}': {runtimeRoots.Count}");

		List<Transform> floors = FindSceneObjectsByName(CourseFloorObjectName);
		report.AppendLine($"Course floors named '{CourseFloorObjectName}': {floors.Count}");

		for (int i = 0; i < controllers.Length; i++)
		{
			AppendControllerAudit(report, controllers[i]);
		}

		for (int i = 0; i < characterControllers.Length; i++)
		{
			Transform playerTransform = characterControllers[i].transform;
			report.AppendLine($"Player[{i}] path={GetHierarchyPath(playerTransform)} pos={playerTransform.position} enabled={characterControllers[i].enabled}");
		}

		for (int i = 0; i < floors.Count; i++)
		{
			Transform floorTransform = floors[i];
			BoxCollider floorCollider = floorTransform.GetComponent<BoxCollider>();
			report.AppendLine($"Floor[{i}] path={GetHierarchyPath(floorTransform)} pos={floorTransform.position} scale={floorTransform.localScale} colliderEnabled={(floorCollider != null && floorCollider.enabled)}");
		}

		return report.ToString();
	}

	private static void AppendControllerAudit(StringBuilder report, ArenaTutorialSceneController controller)
	{
		SerializedObject serializedController = new SerializedObject(controller);
		Vector3 courseOrigin = serializedController.FindProperty("courseOrigin")?.vector3Value ?? Vector3.zero;
		Transform controllerTransform = controller.transform;
		Transform runtimeRoot = controllerTransform.Find(RuntimeRootObjectName);
		ArenaRunTimerDisplay runTimerDisplay = controllerTransform.Find("Run Timer System")?.GetComponent<ArenaRunTimerDisplay>();
		Transform uiRoot = controllerTransform.Find("Arena Tutorial UI");

		report.AppendLine($"Controller path={GetHierarchyPath(controllerTransform)} pos={controllerTransform.position} courseOrigin={courseOrigin}");
		report.AppendLine($"  child runtime root: {(runtimeRoot != null ? GetHierarchyPath(runtimeRoot) : "missing")}");
		report.AppendLine($"  child UI root: {(uiRoot != null ? GetHierarchyPath(uiRoot) : "missing")}");
		report.AppendLine($"  run timer system: {(runTimerDisplay != null ? "present" : "missing")}");

		if (runtimeRoot != null)
		{
			int enemyCount = CountArenaEnemies(runtimeRoot);
			NavMeshSurface navMeshSurface = runtimeRoot.GetComponent<NavMeshSurface>();
			Transform floor = runtimeRoot.Find(CourseFloorObjectName);
			report.AppendLine($"  runtime root localPos={runtimeRoot.localPosition} worldPos={runtimeRoot.position} childCount={runtimeRoot.childCount}");
			report.AppendLine($"  navMeshSurface: {(navMeshSurface != null ? "present" : "missing")}, enemies={enemyCount}");
			report.AppendLine($"  direct floor child: {(floor != null ? floor.position.ToString() : "missing")}");
		}
	}

	private static int CountArenaEnemies(Transform runtimeRoot)
	{
		HashSet<GameObject> enemyRoots = new HashSet<GameObject>();

		KnifePawnController[] knifeEnemies = runtimeRoot.GetComponentsInChildren<KnifePawnController>(true);
		for (int i = 0; i < knifeEnemies.Length; i++)
		{
			if (knifeEnemies[i] != null)
			{
				enemyRoots.Add(knifeEnemies[i].gameObject);
			}
		}

		GunPawnController[] gunEnemies = runtimeRoot.GetComponentsInChildren<GunPawnController>(true);
		for (int i = 0; i < gunEnemies.Length; i++)
		{
			if (gunEnemies[i] != null)
			{
				enemyRoots.Add(gunEnemies[i].gameObject);
			}
		}

		return enemyRoots.Count;
	}

	private static List<Transform> FindSceneObjectsByName(string objectName)
	{
		Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
		List<Transform> results = new List<Transform>();
		for (int i = 0; i < transforms.Length; i++)
		{
			if (transforms[i].name == objectName)
			{
				results.Add(transforms[i]);
			}
		}

		return results;
	}

	private static Transform FindSceneObjectByName(Scene scene, string objectName)
	{
		Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
		Transform firstMatch = null;
		for (int i = 0; i < transforms.Length; i++)
		{
			Transform candidate = transforms[i];
			if (candidate == null || candidate.gameObject.scene != scene || candidate.name != objectName)
			{
				continue;
			}

			if (firstMatch != null)
			{
				return firstMatch;
			}

			firstMatch = candidate;
		}

		return firstMatch;
	}

	private static string GetHierarchyPath(Transform transform)
	{
		if (transform == null)
		{
			return "<null>";
		}

		StringBuilder path = new StringBuilder(transform.name);
		Transform current = transform.parent;
		while (current != null)
		{
			path.Insert(0, current.name + "/");
			current = current.parent;
		}

		return path.ToString();
	}
}
#endif
