using EPOOutline;
using Photon.Pun;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// programmer : TaeHong
/// 
/// The House class handles onClick events made by the player on the house.
/// Click events should only happen in specific phases.
/// </summary>
public class House : MonoBehaviourPun, IPointerClickHandler, IPointerExitHandler, IPunObservable
{
    [SerializeField] private GameObject useSkillUI;
    [SerializeField] private GameObject voteUI;
    [SerializeField] private Outlinable outline;

    [SerializeField] private MafiaPlayer houseOwner;
    public MafiaPlayer HouswOwner { get { return houseOwner; } set { houseOwner = value; } }

    public bool debugMode;

    private void Start()
    {
        if ( debugMode )
        {
            ActivateOutline(true);
        }

        if (PhotonNetwork.IsMasterClient)
        {
            photonView.RPC("AddList", RpcTarget.All);
        }
    }

    // What UI should be shown when a house is clicked
    public void OnPointerClick( PointerEventData eventData )
    {
        if ( !outline.enabled || outline.OutlineParameters.Color == Color.red )
            return;
        
        voteUI.gameObject.SetActive(Manager.Mafia.IsDay);      // Day == vote
        useSkillUI.gameObject.SetActive(!Manager.Mafia.IsDay); // Night == skill
    }

    // Hide UI if cursor exits house
    public void OnPointerExit( PointerEventData eventData )
    {
        HideUI();
    }
    
    public void HideUI()
    {
        useSkillUI.gameObject.SetActive(false);
        voteUI.gameObject.SetActive(false);
    }

    public void ActivateOutline( bool activate )
    {
        if ( outline.OutlineParameters.Color == Color.red )
        {
            outline.OutlineParameters.Color = Color.green;
        }

        outline.enabled = activate;
    }

    public void ClickedUseSkillUI()
    {
        foreach ( House house in Manager.Mafia.Houses )
        {
            if (house.HouswOwner == houseOwner)
            {
                useSkillUI.gameObject.SetActive(false);
                outline.OutlineParameters.Color = Color.red;
                continue;
            }

            house.ActivateOutline(false);
        }
    }

    [PunRPC]
    private void AddList()
    {
        Manager.Mafia.Houses.Add(this);
    }

    public void OnPhotonSerializeView( PhotonStream stream, PhotonMessageInfo info )
    {
        
    }
}
