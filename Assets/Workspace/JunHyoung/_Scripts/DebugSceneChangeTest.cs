using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;

public class DebugSceneChangeTest : MonoBehaviour
{
    [SerializeField] string sceneName;
    [SerializeField] Button button;
    [SerializeField] Button button2;

    private void Awake()
    {
        if ( button != null )
            button.onClick.AddListener(ChangeScene);

        if ( button2 != null )
            button2.onClick.AddListener(ChangeSceneLeaveRoom);
    }

    private void ChangeScene()
    {
        Room curRoom = PhotonNetwork.CurrentRoom;
        Debug.Log(curRoom.Name);
        PhotonNetwork.LoadLevel(sceneName);
    }

    private void ChangeSceneLeaveRoom()
    {
        Singleton<MafiaManager>.ReleaseInstance();
        PhotonNetwork.LeaveRoom();
        PhotonNetwork.LoadLevel(sceneName);
    }
}
