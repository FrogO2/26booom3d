// ClickToMove.cs
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent (typeof (UnityEngine.AI.NavMeshAgent))]
public class ClickToMove : MonoBehaviour {
	RaycastHit hitInfo = new RaycastHit();
	UnityEngine.AI.NavMeshAgent agent;

	void Start () {
		agent = GetComponent<UnityEngine.AI.NavMeshAgent> ();
	}
	void Update () {
		var mouse = Mouse.current;
		if(mouse != null && mouse.leftButton.wasPressedThisFrame) {
			Ray ray = Camera.main.ScreenPointToRay(mouse.position.ReadValue());
			if (Physics.Raycast(ray.origin, ray.direction, out hitInfo))
				agent.destination = hitInfo.point;
		}
	}
}
