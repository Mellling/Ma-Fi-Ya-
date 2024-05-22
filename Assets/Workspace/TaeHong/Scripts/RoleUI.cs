using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using TMPro;
using UnityEngine.UI;
using System;

public class RoleUI : BaseUI
{
    [SerializeField] MafiaRoleDataSO dataSO;

    private void OnEnable()
    {
        MafiaRole playerRole = PhotonNetwork.LocalPlayer.GetPlayerRole();
        MafiaRoleData data = dataSO.GetData(playerRole);
        Debug.Log(playerRole);

        GetUI<Image>("RoleIcon").sprite = data.roleIcon;
        GetUI<TextMeshProUGUI>("RoleName").text = data.roleName;

        MafiaGameChatManager.Instance.isMafia = PhotonNetwork.LocalPlayer.GetPlayerRole() == MafiaRole.Mafia;
        Debug.Log($"RRRRRRRRRRRRR {MafiaGameChatManager.Instance.isMafia}");
    }
}
