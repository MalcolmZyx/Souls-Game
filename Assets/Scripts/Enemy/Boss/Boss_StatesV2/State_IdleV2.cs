using UnityEngine;

public class State_IdleV2 : BossStateV2
{
    private float idleTimer = 0f;
    private float idleDuration;
    private bool facePlayer;
    private bool isHardIdle;
    private bool isChainAdvanceIdle; // When true, calls ExecuteCurrentChainEntry on finish

    public State_IdleV2(BossBrainV2 brain, float idleDuration = 1.0f, bool facePlayer = true, bool isHardIdle = false, bool isChainAdvanceIdle = false) : base(brain)
    {
        this.idleDuration = idleDuration;
        this.facePlayer = facePlayer;
        this.isHardIdle = isHardIdle;
        this.isChainAdvanceIdle = isChainAdvanceIdle;
    }

    public override void Enter()
    {
        idleTimer = 0f;
        brain.PlayAnimation("boss_idle");
    }

    public override void Tick()
    {
        if (brain.currentTarget == null || !brain.currentTarget.IsAlive())
            return;

        // Track the player with rotation but do not move
        if (facePlayer && brain.currentTarget != null)
        {
            Vector3 lookDir = brain.currentTarget.GetPosition() - brain.transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.0001f)
            {
                brain.transform.rotation = Quaternion.Slerp(
                    brain.transform.rotation,
                    Quaternion.LookRotation(lookDir.normalized),
                    Time.deltaTime * 5f);
            }
        }

        idleTimer += Time.deltaTime;

        if (idleTimer >= idleDuration)
        {
            if (isChainAdvanceIdle)
                brain.ExecuteCurrentChainEntry();
            else
                brain.AdvanceChain();
        }
    }

    public override Color GetStateColor() { return Color.yellow; }
    public override string GetStateName() { return isHardIdle ? "HARD IDLE (RECOVERING)" : "Idling... plotting..."; }

    public override void Exit() { }
}
