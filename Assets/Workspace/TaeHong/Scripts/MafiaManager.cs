using Photon.Pun;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Tae;
using TMPro;
using Mafia;

/// <summary>
/// programmer : Yerin, TaeHong
/// 
/// Manager for Mafia game mode.
/// </summary>

public class MafiaManager : Singleton<MafiaManager>, IPunObservable
{
    [Header("Components")]
    public SharedData sharedData;
    public AnimationFactory animFactory;
    private MafiaGameFlow gameFlow;
    public PhotonView photonView => GetComponent<PhotonView>();

    private int playerCount;
    public int PlayerCount => playerCount;

    private bool isDay;
    public bool IsDay { get; set; }
    [SerializeField] private int displayRoleTime;
    [SerializeField] private int roleUseTime;
    [SerializeField] private int voteTime;
    [SerializeField] private float skillTime;

    [SerializeField] List<House> houses;
    public List<House> Houses { get { return houses; } set { houses = value; } }
    public float SkillTime => skillTime;

    private MafiaPlayer player;
    public MafiaPlayer Player { get { return player; } set { player = value; } }

    private House house;
    public House House { get; set; }

    [Header("Game Logic")]
    public MafiaGame Game = new MafiaGame();
    private MafiaResult gameResult = MafiaResult.None;
    public MafiaResult GameResult => gameResult;
    public event Action VoteCountChanged;
    public event Action SkipVoteCountChanged;
    private int[] votes;
    public int[] Votes => votes;
    private int skipVotes;
    public int SkipVotes => skipVotes;

    private MafiaAction? playerAction;
    public MafiaAction? PlayerAction { get { return playerAction; } set { playerAction = value; } }

    public MafiaActionPQ MafiaActionPQ = new MafiaActionPQ();

    // Game Loop Flags
    public bool displayRoleFinished;
    public bool nightPhaseFinished;
    public bool nightEventsFinished;
    public bool nightResultsFinished;
    public bool dayPhaseFinished;
    public bool voteResultsFinished;

    private void Start()
    {
        gameFlow = GetComponent<MafiaGameFlow>();
        isDay = true;
        playerCount = PhotonNetwork.CurrentRoom.PlayerCount;
        votes = new int[playerCount];
    }

    public void ResetFlags()
    {
        displayRoleFinished = false;
        nightPhaseFinished = false;
        nightEventsFinished = false;
        nightResultsFinished = false;
        dayPhaseFinished = false;
        voteResultsFinished = false;
    }

    public int ActivePlayerCount()
    {
        return sharedData.ActivePlayerCount();
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(isDay);
        }
        else
        {
            isDay = (bool) stream.ReceiveNext();
        }
    }

    /******************************************************
    *                    Morning
    ******************************************************/
    #region Last Night Results
    public IEnumerator ShowKilledPlayers(List<int> killed)
    {
        Debug.Log($"{killed.Count} killed last night");
        foreach (int playerID in killed)
        {
            Debug.Log($"Player{playerID} got killed");
            // Show dying animation
            yield return animFactory.SpawnPlayerDie(Houses[playerID - 1]);

            yield return new WaitForSeconds(1);

            // Show everyone dead player's role
            yield return gameFlow.RemovedPlayerRoleRoutine(playerID);

            // Set player state as dead
            sharedData.SetDead(playerID - 1);
            if (PhotonNetwork.IsMasterClient)
            {
                PlayerDied(playerID);
            }

            yield return new WaitForSeconds(1);
        }
    }
    #endregion

    #region Voting
    [PunRPC]
    public void VoteForPlayer(int playerID)
    {
        if(playerID == -1)
        {
            skipVotes++;
            SkipVoteCountChanged?.Invoke();
            return;
        }
        votes[playerID - 1]++;
        VoteCountChanged?.Invoke();
    }

    [PunRPC] // Called on players who finished voting
    public void BlockVotes()
    {
        foreach(House house in houses)
        {
            house.ActivateOutline(false);
        }
        gameFlow.DisableSkipButton();
    }

    [PunRPC] // Called only on MasterClient
    public void CountVotes() // Return playerID or -1 if none
    {
        // Look for candidate with highest votes
        int highest = votes[0];
        int count = 1;
        int voted = 0;
        for(int i = 1; i < votes.Length; i++)
        {
            if (votes[i] > votes[highest])
            {
                highest = i;
                count = 1;
            }
            if (votes[i] == votes[highest])
            {
                count++;
            }
            voted += votes[i];
        }
        // Return result
        // No one gets kicked if:
        //      - There is a tie for highest votes
        //      - Skipped votes > highest vote
        int result;
        int skipped = Manager.Mafia.ActivePlayerCount() - voted;
        if (count > 1 || skipped > votes[highest])
        {
            result = -1;
        }
        else
        {
            result = highest + 1;
        }

        photonView.RPC("ResetVotes", RpcTarget.All);
        sharedData.photonView.RPC("SetPlayerToKick", RpcTarget.All, result);
    }

    [PunRPC]
    public void ResetVotes()
    {
        // Reset values before returning result
        for (int i = 0; i < votes.Length; i++)
        {
            votes[i] = 0;
        }
        skipVotes = 0;
    }
    #endregion

    #region Vote Result
    public void ApplyVoteResult(int voteResult)
    {
        if (PhotonNetwork.IsMasterClient)
        {
            PlayerDied(voteResult);
            sharedData.playerToKick = -1; // Reset value
        }
    }
    #endregion

    /******************************************************
    *                    Night
    ******************************************************/
    #region Player Actions
    public void NotifyAction()
    {
        if (PlayerAction == null)
        {
            return;
        }

        MafiaAction action = (MafiaAction) PlayerAction;
        photonView.RPC("EnqueueAction", RpcTarget.MasterClient, action.Serialize());
        PlayerAction = null;
    }

    [PunRPC] // Called only on MasterClient
    public void EnqueueAction(int[] serialized)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        MafiaAction action = new MafiaAction(serialized);
        MafiaActionPQ.Enqueue(action);
    }

    [PunRPC] // Called only on MasterClient
    public void ParseActionsAndAssign()
    {
        sharedData.photonView.RPC("ResetPlayerStates", RpcTarget.All);

        MafiaAction action;
        
        Debug.Log($"Begin ActionPQ Debug with Count: {MafiaActionPQ.Count}");
        while (MafiaActionPQ.Count > 0)
        {
            action = MafiaActionPQ.Dequeue();
            Debug.Log($"{action.sender} ==> {action.receiver} with {action.actionType}");
            int senderIdx = action.sender - 1;
            int receiverIdx = action.receiver - 1;

            // If blocked, don't add action
            if (sharedData.blockedPlayers[senderIdx])
            {
                continue;
            }

            // Send action info to players (if not insane)
            if (PhotonNetwork.CurrentRoom.Players[action.sender].GetPlayerRole() != MafiaRole.Insane)
            {
                switch (action.actionType)
                {
                    case MafiaActionType.Block:
                        sharedData.photonView.RPC("SetBlocked", RpcTarget.All, receiverIdx, true);
                        break;
                    case MafiaActionType.Kill:
                        sharedData.photonView.RPC("SetKilled", RpcTarget.All, receiverIdx, true);
                        break;
                    case MafiaActionType.Heal:
                        if (sharedData.killedPlayers[receiverIdx] == true)
                        {
                            sharedData.photonView.RPC("SetKilled", RpcTarget.All, receiverIdx, false);
                            sharedData.photonView.RPC("SetHealed", RpcTarget.All, receiverIdx, true);
                        }
                        break;
                }
            }

            // Add action to shared data
            sharedData.photonView.RPC("AddAction", RpcTarget.All, action.Serialize());
        }
        Debug.Log($"End ActionPQ Debug");
    }

    [PunRPC]
    public void ShowActions()
    {
        StartCoroutine(Player.ShowActionsRoutine());
    }
    #endregion

    /******************************************************
    *                    Game Over
    ******************************************************/
    #region Game Over
    // Called only on MasterClient
    public void PlayerDied(int id)
    {
        PhotonNetwork.PlayerList[id].SetDead(true);
        gameResult = Game.RemovePlayer(PhotonNetwork.CurrentRoom.Players[id].GetPlayerRole());
    }
    #endregion

    /******************************************************
    *                    Utils
    ******************************************************/
    #region Utils
    public void ActivateHouseOutlines()
    {
        for (int i = 0; i < PlayerCount; i++)
        {
            if (i == (PhotonNetwork.LocalPlayer.ActorNumber - 1))
                continue;

            Manager.Mafia.Houses[i].ActivateOutline(true);
        }
    }

    public void DeactivateHouseOutlines()
    {
        foreach (var house in Manager.Mafia.Houses)
        {
            house.ActivateOutline(false);
        }
    }
    #endregion
}
