using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SCP053.Scripts;

public class SCP053EnemyAI : EnemyAI
{
    private static readonly int Scared = Animator.StringToHash("scared");
    private static readonly int Idle = Animator.StringToHash("idle");
    public List<AudioClip> walkSounds;
    
    public AudioClip scaredSoundBuildUp;
    public AudioClip killPlayerSound;

    public Light killPlayerLight;
    
    public GameObject meshObject;
    
    private readonly float walkSpeed = 3.5f;

    private float aiInterval = 0.2f;
    private int lastBehaviorState;
    private readonly float walkSoundDelayRun = 0.5f;
    private readonly float walkSoundDelayWalk = 0.9f;

    private float walkSoundTimer;
    
    private bool isLocalPlayerTargeted;
    private PlayerControllerB player;

    private float timeInFear = 0f;
    private float maxTimeInFear = 10f;
    private float fearPower = 0f;

    private List<Light> ligthsClose = new List<Light>();

    private Coroutine lightCoroutine;

    private ulong? currentTargetPlayerId;
    

    public override void Start()
    {
        Scp053Plugin.instance.SpawnActionsObject();
        
        base.Start();

        agent.speed = walkSpeed;
        agent.acceleration = 255f;
        agent.angularSpeed = 900f;
        
        killPlayerLight.enabled = false;
        
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
            if(currentBehaviourStateIndex != 0) return;
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

    private void CancelPlayerEffect()
    {
        if(!player) return;
        player.disableLookInput = false;
        player.disableMoveInput = false;
        player.gameplayCamera.transform.localRotation = Quaternion.Euler(new Vector3(player.gameplayCamera.transform.localRotation.x,0,0));
    }

    private void LateUpdate()
    {
        if (currentBehaviourStateIndex == 1)
        {
            timeInFear += Time.deltaTime;
        }
        
        fearPower = Mathf.Clamp(timeInFear / maxTimeInFear, 0f, 1f);
        
        if (isLocalPlayerTargeted && currentBehaviourStateIndex == 1)
        {
            if(fearPower > 0.8f)
            {
                HUDManager.Instance.ShakeCamera(ScreenShakeType.VeryStrong);
            }
            else if(fearPower > 0.6f)
            {
                HUDManager.Instance.ShakeCamera(ScreenShakeType.Big);
            }
            else if(fearPower > 0.4f)
            {
                HUDManager.Instance.ShakeCamera(ScreenShakeType.Small);
            }
            
            Scp053Plugin.instance.currentSCP053Actions.Enable(true);
            Scp053Plugin.instance.currentSCP053Actions.SetVolumeWeight(fearPower);
            
            player = GameNetworkManager.Instance.localPlayerController;

            player.transform.LookAt(eye);
            player.transform.localRotation = Quaternion.Euler(new Vector3(0,player.transform.localRotation.eulerAngles.y,0));
            
            player.gameplayCamera.transform.LookAt(eye);
            
            player.disableLookInput = true;

            if(fearPower < 0.5)
            {
                player.disableMoveInput = true;
                
                //player.thisController.Move(transform.position * (Time.deltaTime * 0.1f)) ;
                
            }
            else
            {
                player.disableMoveInput = false;
            }
            
 

            if (fearPower >= 1f)
            {
                SwitchToBehaviourServerRpc(2);
            }
        }
        else if (currentBehaviourStateIndex == 2 )
        {

            if (isLocalPlayerTargeted)
            {
                player.disableMoveInput = true;
                player.disableLookInput = true;
            }
            
            var currentPlayertarget = StartOfRound.Instance.allPlayerScripts.ToList().Find(p => p.playerClientId == currentTargetPlayerId);
            
            var positionJumpScare = currentPlayertarget.gameplayCamera.transform.position + currentPlayertarget.gameplayCamera.transform.forward * 1.7f;
            transform.position = positionJumpScare - Vector3.up * 1f;
            transform.LookAt(currentPlayertarget.gameplayCamera.transform);
            transform.eulerAngles = new Vector3(0, transform.eulerAngles.y, 0);
        }
        else
        {
            Scp053Plugin.instance.currentSCP053Actions.Enable(false);
        }
    }

    public override void DoAIInterval()
    {
        base.DoAIInterval();

        switch (currentBehaviourStateIndex)
        {
            case 0: //walk
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
                else if(PlayerIsTargetable(targetPlayer) && Vector3.Distance(transform.position, targetPlayer.transform.position) < 12f)
                {
                    ChangeTargetPlayerIdClientRpc(targetPlayer.playerClientId);
                    SwitchToBehaviourState(1);
                }

                break;
            }
            case 1: //scared
            {
                
                if (!targetPlayer || !CheckLineOfSightForPosition(targetPlayer.gameplayCamera.transform.position, width: 80f))
                {
                    ChangeTargetPlayerIdClientRpc(555);
                    SwitchToBehaviourState(0);
                }

                break;
            }
            case 2: //kill
            {
                

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
                timeInFear = 0f;
                fearPower = 0f;
                agent.speed = walkSpeed;
                creatureAnimator.SetBool(Scared, false);
                creatureAnimator.SetBool(Idle, false);

                creatureVoice.Stop();
                CancelPlayerEffect();
                //lights
                StopCoroutine(lightCoroutine);
                SwitchLightsActive(true);
                ligthsClose.Clear();
                
                break;
            }
            case 1:
            {
                agent.speed = 0;
                creatureAnimator.SetBool(Scared, true);
                creatureAnimator.SetBool(Idle, false);

                creatureVoice.PlayOneShot(scaredSoundBuildUp);
                lightCoroutine = StartCoroutine(PlayWithLights());
                break;
            }
            case 2:
            {
                agent.speed = 0;
                creatureAnimator.SetBool(Scared, true);
                creatureAnimator.SetBool(Idle, true);
                if (IsServer) KillPlayerClientRpc();
                //lights
                StopCoroutine(lightCoroutine);
                break;
            }
        }
    }

    private void SwitchLightsActive(bool active)
    {
        ligthsClose.ForEach(l => l.enabled = active);
    }

    private void RandomEnableLights()
    {
        ligthsClose.ForEach(l => l.enabled = Random.Range(0f,1f) > 0.3f);
    }

    private IEnumerator PlayWithLights()
    {
        ligthsClose.Clear();
        var lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).ToList();
        
        lights.ForEach(l =>
        {
            if (l.enabled && Vector3.Distance(l.transform.position, transform.position) < 15f)
            {
                ligthsClose.Add(l);
            }
        });

        while (currentBehaviourStateIndex == 1)
        {
            RandomEnableLights();
            
            yield return new WaitForSeconds(Random.Range(0.2f, 1.5f));
            
        }
        
    }

    [ClientRpc]
    private void KillPlayerClientRpc()
    {
        StartCoroutine(KillPlayer());
    }

    [ClientRpc]
    private void ChangeTargetPlayerIdClientRpc(ulong id)
    {
        currentTargetPlayerId = id;
        isLocalPlayerTargeted = id == GameNetworkManager.Instance.localPlayerController.playerClientId;
        Debug.Log($"IS LOCAL PLAYER TARGET {isLocalPlayerTargeted}");
    }


    
    private IEnumerator KillPlayer()
    {
        SwitchLightsActive(false);
        meshObject.SetActive(false);
        yield return new WaitForSeconds(1f);
        meshObject.SetActive(true);
        
        killPlayerLight.enabled = true;
        creatureVoice.PlayOneShot(killPlayerSound);
        if (isLocalPlayerTargeted)
        {
            Scp053Plugin.instance.currentSCP053Actions.Enable(true);
            Scp053Plugin.instance.currentSCP053Actions.SetVolumeWeight(0.9f);
            HUDManager.Instance.ShakeCamera(ScreenShakeType.VeryStrong);
        }
        
        yield return new WaitForSeconds(2f);


        if (isLocalPlayerTargeted)
        {
            GameNetworkManager.Instance.localPlayerController.KillPlayer(Vector3.back);
            CancelPlayerEffect();
            
            player = null;
            Scp053Plugin.instance.currentSCP053Actions.Enable(false);
        }
        
        
        killPlayerLight.enabled = false;
        SwitchLightsActive(true);
        ligthsClose.Clear();

        if (IsServer)
        {
            ChangeTargetPlayerIdClientRpc(555);
            SwitchToBehaviourState(0);
        }


    }
}