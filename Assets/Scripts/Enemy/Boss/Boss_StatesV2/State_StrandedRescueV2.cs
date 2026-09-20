using UnityEngine;

public class State_StrandedRescueV2 : BossStateV2
{
    private Vector3 jumpStartPos;
    private Vector3 targetPos;
    private float jumpTimer = 0f;
    private float jumpDuration = 1.0f;
    private float jumpPeakHeight = 6.0f;

    public State_StrandedRescueV2(BossBrainV2 brain) : base(brain) { }

    public override void Enter()
    {
        UnityEngine.AI.NavMeshAgent agent = brain.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = false;

        jumpStartPos = brain.transform.position;
        jumpTimer = 0f;
        targetPos = brain.originalPosition;

        if (brain.arenaWaypoints != null && brain.arenaWaypoints.waypoints != null && brain.arenaWaypoints.waypoints.Length > 0 && brain.currentTarget != null)
        {
            float closestDist = float.MaxValue;
            Vector3 playerPos = brain.currentTarget.GetPosition();
            foreach (var wp in brain.arenaWaypoints.waypoints)
            {
                float d = Vector3.Distance(wp.position, playerPos);
                if (d < closestDist)
                {
                    closestDist = d;
                    targetPos = wp.position;
                }
            }
        }
    }

    public override void Tick()
    {
        jumpTimer += Time.deltaTime;
        float t = Mathf.Clamp01(jumpTimer / jumpDuration);

        Vector3 currentBasePos = Vector3.Lerp(jumpStartPos, targetPos, t);
        float currentHeight = 4f * jumpPeakHeight * t * (1f - t);
        brain.transform.position = new Vector3(currentBasePos.x, currentBasePos.y + currentHeight, currentBasePos.z);

        Vector3 lookDir = targetPos - brain.transform.position;
        lookDir.y = 0;
        if (lookDir.sqrMagnitude > 0.001f)
            brain.transform.rotation = Quaternion.Slerp(brain.transform.rotation, Quaternion.LookRotation(lookDir.normalized), Time.deltaTime * 15f);

        if (t >= 1.0f)
            brain.ChangeState(new State_IdleV2(brain, 1.0f));
    }

    public override void Exit()
    {
        brain.desiredMovementDirection = Vector3.zero;
        UnityEngine.AI.NavMeshAgent agent = brain.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = true;
        brain.SetCooldown(this.GetType(), 10.0f);
    }

    public override Color GetStateColor() { return Color.cyan; }
    public override string GetStateName() { return "STRANDED RESCUE LEAP"; }
}
