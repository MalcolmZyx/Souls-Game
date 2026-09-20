using UnityEngine;
using System.Collections.Generic;

public class State_ArenaRepositionV2 : BossStateV2
{
    private enum Phase { JumpingUp, WaitingAtPeak, JumpingDown }
    private Phase currentPhase = Phase.JumpingUp;

    private float timer = 0f;
    private Vector3 jumpStartPos;
    private Vector3 jumpTargetPos;
    private float floatCurrentDuration;
    private float floatPeakHeight;

    private bool inAirLoopPlaying = false;

    public State_ArenaRepositionV2(BossBrainV2 brain) : base(brain) { }

    public override void Enter()
    {
        var cfg = brain.arenaRepositionConfig;
        brain.SetCooldown(this.GetType(), cfg.cooldownDuration);
        brain.desiredMovementDirection = Vector3.zero;

        UnityEngine.AI.NavMeshAgent agent = brain.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = false;

        brain.isInArenaSequence = true;

        if (brain.arenaWaypoints == null || brain.arenaWaypoints.waypoints == null || brain.arenaWaypoints.waypoints.Length == 0)
        {
            Debug.LogError("[ArenaRepositionV2] No waypoints baked!");
            brain.ChangeState(new State_IdleV2(brain));
            return;
        }

        List<BossArenaWaypoints.WaypointData> highPoints = brain.arenaWaypoints.GetPointsByType(BossArenaWaypoints.WaypointType.High);
        BossArenaWaypoints.WaypointData chosenPoint;
        if (highPoints.Count > 0)
        {
            chosenPoint = highPoints[Random.Range(0, highPoints.Count)];
            if (brain.currentTarget != null && Vector3.Distance(chosenPoint.position, brain.currentTarget.GetPosition()) < 5f)
                chosenPoint = highPoints[Random.Range(0, highPoints.Count)];
        }
        else
        {
            chosenPoint = brain.arenaWaypoints.waypoints[Random.Range(0, brain.arenaWaypoints.waypoints.Length)];
        }

        currentPhase = Phase.JumpingUp;
        jumpStartPos = brain.transform.position;
        jumpTargetPos = chosenPoint.position;
        timer = 0f;
        floatCurrentDuration = cfg.jumpUpDuration;
        floatPeakHeight = cfg.jumpUpPeakHeight;

        brain.PlaySound(BossSoundCue.LeapTakeoff);
        StartInAirLoop();
    }

    public override void Tick()
    {
        var cfg = brain.arenaRepositionConfig;
        brain.desiredMovementDirection = Vector3.zero;

        if (currentPhase == Phase.JumpingUp)
        {
            HandleJumpTick();
            if (timer >= floatCurrentDuration)
            {
                currentPhase = Phase.WaitingAtPeak;
                timer = 0f;
                StopInAirLoop();
                brain.PlaySound(BossSoundCue.LeapLand);
            }
        }
        else if (currentPhase == Phase.WaitingAtPeak)
        {
            timer += Time.deltaTime;
            
            // Face target while waiting
            if (brain.currentTarget != null)
            {
                Vector3 dir = brain.currentTarget.GetPosition() - brain.transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.001f)
                    brain.transform.rotation = Quaternion.Slerp(brain.transform.rotation, Quaternion.LookRotation(dir.normalized), Time.deltaTime * 5f);
            }

            if (timer >= cfg.waitAtPeak)
            {
                currentPhase = Phase.JumpingDown;
                timer = 0f;
                jumpStartPos = brain.transform.position;
                jumpTargetPos = GetCenterPoint();
                floatCurrentDuration = cfg.jumpDownDuration;
                floatPeakHeight = cfg.jumpDownPeakHeight;

                brain.PlaySound(BossSoundCue.LeapTakeoff);
                StartInAirLoop();
            }
        }
        else if (currentPhase == Phase.JumpingDown)
        {
            HandleJumpTick();
            if (timer >= floatCurrentDuration)
            {
                StopInAirLoop();
                brain.PlaySound(BossSoundCue.LeapLand);
                brain.AdvanceChain();
            }
        }
    }

    private void HandleJumpTick()
    {
        timer += Time.deltaTime;
        float t = Mathf.Clamp01(timer / floatCurrentDuration);
        Vector3 currentBasePos = Vector3.Lerp(jumpStartPos, jumpTargetPos, t);
        float jumpHeight = 4f * floatPeakHeight * t * (1f - t);
        brain.transform.position = new Vector3(currentBasePos.x, currentBasePos.y + jumpHeight, currentBasePos.z);

        Vector3 moveDir = jumpTargetPos - jumpStartPos;
        moveDir.y = 0;
        if (moveDir.sqrMagnitude > 0.001f)
            brain.transform.rotation = Quaternion.Slerp(brain.transform.rotation, Quaternion.LookRotation(moveDir.normalized), Time.deltaTime * 6f);
    }

    private Vector3 GetCenterPoint()
    {
        if (brain.arenaWaypoints == null) return Vector3.zero;
        foreach (var wp in brain.arenaWaypoints.waypoints)
            if (wp.type == BossArenaWaypoints.WaypointType.Center) return wp.position;
        return Vector3.zero;
    }

    public override void Exit()
    {
        brain.desiredMovementDirection = Vector3.zero;
        brain.isInArenaSequence = false;
        StopInAirLoop();

        UnityEngine.AI.NavMeshAgent agent = brain.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = true;
    }

    private void StartInAirLoop()
    {
        if (inAirLoopPlaying) return;
        brain.PlaySound(BossSoundCue.LeapAirLoopStart);
        inAirLoopPlaying = true;
    }

    private void StopInAirLoop()
    {
        if (!inAirLoopPlaying) return;
        brain.PlaySound(BossSoundCue.LeapAirLoopStop);
        inAirLoopPlaying = false;
    }

    public override Color GetStateColor() { return new Color(0f, 0.9f, 0.4f); }
    public override string GetStateName()
    {
        if (currentPhase == Phase.JumpingUp) return "LEAPING HIGH";
        if (currentPhase == Phase.WaitingAtPeak) return "REPOSITION WAIT";
        return "RETURNING TO CENTER";
    }
}