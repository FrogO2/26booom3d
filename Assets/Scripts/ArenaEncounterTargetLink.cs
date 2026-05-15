using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Arena/Encounter Target Link")]
public class ArenaEncounterTargetLink : MonoBehaviour, IAttackTargetGate, IAttackTargetDeathListener
{
	[SerializeField] private ArenaEncounterFlow encounterFlow;

	public void Initialize(ArenaEncounterFlow flow)
	{
		encounterFlow = flow;
	}

	public bool CanTarget(ArenaBakedEnemyTarget target)
	{
		return encounterFlow == null || (encounterFlow.HasStarted && !encounterFlow.IsCleared);
	}

	public void OnTargetKilled(ArenaBakedEnemyTarget target)
	{
		encounterFlow?.NotifyEnemyKilled(target);
	}
}