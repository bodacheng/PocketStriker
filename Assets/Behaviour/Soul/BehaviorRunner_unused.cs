using UnityEngine;

namespace Soul
{
    public partial class BehaviorRunner : MonoBehaviour
    {
        //List<int> transferList; //这个列表的意义在于传递这个信息：该角色是否从某个客户端进行了控制权传递，如何向这个列表里添加值待定。暂时想不出办法
        //一个是在playerModeNewCommandWaiting()当中，而这两个地方的重点在于，围绕refreshSPLevelButtonsInfo的外部逻辑其实不同。

        //private bool maxAniTransferTimeReached = false;
        // After entering the state,start this corotine
        //public IEnumerator TransferMaxTimeCounter(float time)
        //   {
        //  yield return new WaitForSeconds(time);
        //  maxAniTransferTimeReached = true;
        //}


        //public void animTransferSignal(int sentFromPlayer)
        //{
        //  photonView.RPC("animTransferSignalRPC", PhotonTargets.All, sentFromPlayer);
        //}

        //[PunRPC]
        //public void animTransferSignalRPC(int sentFromPlayer)
        //{
        //  if (photonView.isMine)
        //  {
        //      return;//只给对面客户端发送这个信号
        //  }
        //  this.transferList.Add (sentFromPlayer);
        //  if (_Animation_Manger.current_animation_name != null)
        //  {
        //      StartCoroutine(TransferMaxTimeCounter(_Animation_Manger.tryAnimationClip(_Animation_Manger.current_animation_name).length));
        //  }
        //}

        //void Update()
        //{
        //if (photonView.isMine || (PhotonNetwork.offlineMode && PhotonNetwork.connected) || !PhotonNetwork.connected)
        //}

        //void FixedUpdate()
        //{
        //if (photonView.isMine || (PhotonNetwork.offlineMode && PhotonNetwork.connected) || !PhotonNetwork.connected) 
        //{
        //}

        //public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        //{
        //  if (PhotonNetwork.connected)
        //  {
        //      if (stream.isWriting)
        //      {
        //          stream.SendNext(this.current_state_num);    
        //      }
        //      else
        //      {
        //          this.current_state_num = (string)stream.ReceiveNext();
        //      }
        //  }
        //}

        //void LateUpdate()
        //{
        // 这个状态机要引入一个新功能，就是针对一个角色托管至另一个客户端时候那一瞬间的处理。
        // 那一个瞬间，我们需要的是一个主动的changestate的处理。把这状态在新的客户端转换成背景状态，然后在这个新的客户端运行状态机
        // 这个瞬间，changestate操作只能执行一次。所以我们需要一个类似flag一样的东西。。一旦这个flag成了真，那就先检查一下当前动画是否播放结束，如果结束，那就直接运行changestate。。
        // 麻烦的是这个客户端转换的信号怎么感知。
        //if (photonView.isMine || (PhotonNetwork.offlineMode && PhotonNetwork.connected) || !PhotonNetwork.connected) 
        //{

        //if (transferList.Count > 0)
        //{
        //  if (photonView.isMine) 
        //  {
        //      if (_Animation_Manger.ifChanceForAPhotonAniTransfer())
        //      {
        //          this.changeState ("Move");
        //          transferList.Clear ();
        //      }
        //      if (maxAniTransferTimeReached)
        //      {
        //          Debug.Log ("控制方转移最长期待动画迁移时间结束，强制转状态");
        //          this.changeState ("Move");
        //          transferList.Clear ();
        //          maxAniTransferTimeReached = false;
        //      }
        //  }
        //}
        //}

        //public void callStatesTransitionInitiate(int Series,int level)
        //{
        //  if (!PhotonNetwork.connected || (PhotonNetwork.connected && PhotonNetwork.offlineMode)) {
        //           this.loadStatesTransition (this._Animation_Manger.clip_path,Series,level);
        //  } else {
        //      photonView.RPC("loadStatesTransition", PhotonTargets.All, Series,level);
        //  }
        //}
    }
}