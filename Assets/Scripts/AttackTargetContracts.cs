public interface IAttackTargetGate
{
	bool CanTarget(ArenaBakedEnemyTarget target);
}

public interface IAttackTargetDeathListener
{
	void OnTargetKilled(ArenaBakedEnemyTarget target);
}