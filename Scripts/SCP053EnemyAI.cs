using System.Collections.Generic;
using UnityEngine;

namespace SCP053.Scripts;

public class SCP053EnemyAI : EnemyAI
{
    private static readonly int Scared = Animator.StringToHash("scared");
    public List<AudioClip> walkSounds;
    private readonly float runSpeed = 5f;
    private readonly float walkSpeed = 3.5f;

    private float aiInterval = 0.2f;
    private int lastBehaviorState;
    private readonly float walkSoundDelayRun = 0.5f;
    private readonly float walkSoundDelayWalk = 0.9f;

    private float walkSoundTimer;

    public override void Start()
    {
        base.Start();

        agent.speed = walkSpeed;
        agent.acceleration = 255f;
        agent.angularSpeed = 900f;
    }

    public override void Update()
    {
        base.Update();

        if (lastBehaviorState != currentBehaviourStateIndex)
        {
            Debug.Log($"New behavior state : {currentBehaviourStateIndex} last : {lastBehaviorState}");
            lastBehaviorState = currentBehaviourStateIndex;
            AllClientOnSwitchBehaviorState();
        }
        
        if (currentBehaviourStateIndex == 1 && GameNetworkManager.Instance.localPlayerController.HasLineOfSightToPosition(transform.position + Vector3.up * 0.25f, 100f, 60))
        {
            GameNetworkManager.Instance.localPlayerController.JumpToFearLevel(0.8f);
        }

        walkSoundTimer -= Time.deltaTime;

        //WALKSOUNDS
        if (walkSoundTimer <= 0f)
        {
            var randomSound = walkSounds[Random.Range(0, walkSounds.Count)];
            creatureSFX.PlayOneShot(randomSound);
            walkSoundTimer = currentBehaviourStateIndex == 1 ? walkSoundDelayRun : walkSoundDelayWalk;
        }

        if (!IsServer) return;

        if (aiInterval <= 0)
        {
            aiInterval = AIIntervalTime;
            DoAIInterval();
        }
    }

    public override void DoAIInterval()
    {
        base.DoAIInterval();

        switch (currentBehaviourStateIndex)
        {
            case 0:
            {
                TargetClosestPlayer(requireLineOfSight: true, viewWidth: 80f);
                if (targetPlayer == null)
                {
                    if (currentSearch.inProgress) break;
                    var aiSearchRoutine = new AISearchRoutine();
                    aiSearchRoutine.searchWidth = 50f;
                    aiSearchRoutine.searchPrecision = 8f;
                    StartSearch(ChooseFarthestNodeFromPosition(transform.position, true).position, aiSearchRoutine);
                }
                else if(PlayerIsTargetable(targetPlayer))
                {
                    SwitchToBehaviourState(1);
                }

                break;
            }
            case 1:
            {
                if (!targetPlayer || !CheckLineOfSightForPosition(targetPlayer.gameplayCamera.transform.position, width: 80f))
                {
                    SwitchToBehaviourState(0);
                }

                break;
            }
        }
    }

    private void AllClientOnSwitchBehaviorState()
    {
        switch (currentBehaviourStateIndex)
        {
            case 0:
            {
                agent.speed = walkSpeed;
                creatureAnimator.SetBool(Scared, false);
                break;
            }
            case 1:
            {
                agent.speed = 0;
                creatureAnimator.SetBool(Scared, true);
                break;
            }
        }
    }
}