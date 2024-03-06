//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;
//using System;
//using System.Linq;

////出于很多原因，最终我们还是发现需要一个“第三者”作为一个战斗场景的裁判。因为针对掉线重连等问题得有个参考。
////而且，这个组件本身牵扯到一个很重要的概念，那就是转交物体的控制权。
////不光是这个裁判物体在一方掉线的情况下可能牵扯到自身的控制权转交，而且我们直觉认为这个单位是把所有AI角色进行转交任务的执行者。

//public class fightingSceneNetworkManager : PunBehaviour {

//	List<playerNetInfo> playersInfoList; // 当下这个量唯一的作用就是检查所有玩家是不是都已经准备好。并且当下playerNetInfo这个量是会随玩家掉线消失的。
//	private IDictionary<string, List<GameObject>> TeamMembers;
//	public SceneStep step;

//	void Awake()
//	{
//	}
		
//	// Use this for initialization
//	void Start () {
//		step = SceneStep.Preparing;
//	}

//	// Update is called once per frame
//	void Update () {
//		SceneStateMachine ();
//	}

//	public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
//	{
//		if (stream.isWriting)
//		{
//			stream.SendNext(this.step);	
//		}
//		else
//		{
//			this.step = (SceneStep)stream.ReceiveNext();
//		}
//	}

//	// when a player disconnects from the room, update the spawn/position order for all
//	public override void OnPhotonPlayerDisconnected(PhotonPlayer disconnetedPlayer) {
//		Debug.Log (photonView.owner.ID);
//		if (photonView.owner.ID == disconnetedPlayer.ID)
//		{
//			Debug.Log (disconnetedPlayer.ID +"after"+ PhotonNetwork.player.ID);
//			photonView.TransferOwnership (PhotonNetwork.player.ID);
//		}
//	}

//	// 检查每个玩家是不是都“做好准备战斗”
//	// 在正式版本游戏中，我们匹配好玩家后，进入了房间应该是不需要这个准备号令，参考英雄联盟。但非常重要的是从战斗条件上说所有玩家的信息是不是都在战斗场景中得以读取。
//	// 就是说战斗场景到底有没有为这场战都做好全方位的“准备”从而自动启动战斗，这个事情我们可能想的不够。再一个，如果战斗是靠倒计时机制进行，那怎么能保证双方真的是同时开始战斗
//	public bool checkIfEveryPlayerIsReady()
//	{
//		playersInfoList = Transform.FindObjectsOfType<playerNetInfo>().ToList();
//		foreach (playerNetInfo _playerNetInfo in playersInfoList)
//		{
//			if (!_playerNetInfo.ifFightReady)
//			{
//				return false;
//			}
//		}
//		return true;
//	}

//	//所有牵扯到playersInfoList的函数都可能因为一方掉线产生计算错误。
//	public int getOpponentPlayerID(int myID)
//	{
//		foreach (playerNetInfo _playerNetInfo in playersInfoList)
//		{
//			if (_playerNetInfo.playerID != myID)
//			{
//				return _playerNetInfo.playerID;
//			}
//		}
//		return -1;
//	}

//	public void transferCharactersOwnerShip(List<GameObject> myOwningCharacters, int fromPlayerID,int toPlayerPhotonID)
//	{
//		foreach (GameObject one in myOwningCharacters)
//		{
//			one.GetComponent<AIStateRunner> ().animTransferSignal(fromPlayerID);
//			one.GetComponent<PhotonView> ().TransferOwnership (toPlayerPhotonID);
//		}
//	}
		
//	public bool checkIfEveryTeamHasMember()
//	{
//		this.arrangeMemberOfEveryTeam ();
//		if (this.TeamMembers != null)
//		{
//			if (this.TeamMembers.Count < 2)
//			{
//				return false;
//			}
//			foreach (KeyValuePair<string, List<GameObject>> MembersOfOneTeam in this.TeamMembers)
//			{
//				if (MembersOfOneTeam.Value.Count() == 0)
//				{
//					return false;
//				}
//			}
//		}else{
//			return false;
//		}
//		return true;
//	}

//	public void arrangeMemberOfEveryTeam()
//	{
//		IDictionary<string, List<GameObject>> _dic = new Dictionary<string, List<GameObject>>();
//		List<AIStateRunner> _charsOfTheFight = Transform.FindObjectsOfType<AIStateRunner>().ToList();	
//		List<string> tags = new List<string>();
//		foreach (AIStateRunner _AIStateRunner in _charsOfTheFight)
//		{
//			if (!tags.Contains(_AIStateRunner.gameObject.tag))
//			{
//				tags.Add(_AIStateRunner.gameObject.tag);
//				_dic.Add(new KeyValuePair<string,List<GameObject>>(_AIStateRunner.gameObject.tag, new List<GameObject>(){_AIStateRunner.gameObject}));
//			}else{
//				List<GameObject> membersOfTheTag = new List<GameObject>();
//				_dic.TryGetValue (_AIStateRunner.gameObject.tag,out membersOfTheTag);
//				membersOfTheTag.Add (_AIStateRunner.gameObject);//这个环节有我们对C#的一点疑问。
//			}
//		}
//		this.TeamMembers = _dic;
//	}

//	public string getWinnerTag()
//	{
//		arrangeMemberOfEveryTeam();
//		IDictionary<string,bool> TeamAllDeadDictionary = TeamDeadDictionary();
//		List<string> AliveTeams = new List<string>();
//		foreach(KeyValuePair<string,bool> _KeyValuePair in TeamAllDeadDictionary)
//		{
//			if (!_KeyValuePair.Value)
//			{
//				AliveTeams.Add (_KeyValuePair.Key);
//			}
//		}
//		if (AliveTeams.Count == 1) {
//			return AliveTeams [0];
//		} else {
//			return null;
//		}
//	}

//	public IDictionary<string,bool> TeamDeadDictionary()
//	{		
//		IDictionary<string,bool> TeamAllDeadDictionary = new Dictionary<string, bool>();
//		foreach (KeyValuePair<string,List<GameObject>> _KeyValuePair in this.TeamMembers)
//		{
//			bool thisTeamAllDead = true;
//			foreach(GameObject _char in _KeyValuePair.Value)
//			{
//				if (_char.GetComponent<AIStateRunner>().current_state_num != "death")
//				{
//					thisTeamAllDead = false;
//				}
//			}
//			TeamAllDeadDictionary.Add(new KeyValuePair<string, bool>(_KeyValuePair.Key,thisTeamAllDead));
//		}
//		return TeamAllDeadDictionary;
//	}
		
//	// 两个玩家共同连线一个游戏，没有一方可以完全决定游戏，所以整个战斗流程其实从进入房间后就是要走一个一去不复返的过程。
//	// 另外各个客户端的manager中的scenestep其实是应该随时与这个东西上的step进行较对。
//	public void SceneStateMachine()
//	{
//		switch(this.step)
//		{
//			case SceneStep.Preparing:
//			if (checkIfEveryPlayerIsReady())
//			{
//				this.step = SceneStep.Fighting;
//			}
//			break;
//			case SceneStep.Fighting:
//			if (getWinnerTag() != null)
//			{
//				this.step = SceneStep.FightOver;
//			}
//			break;
//			case SceneStep.FightOver:
//			break;
//		}
//	}

//	public List<GameObject> getTeamMembers(string teamTag)
//	{
//		arrangeMemberOfEveryTeam ();
//		List<GameObject> Members = new List<GameObject>();
//		this.TeamMembers.TryGetValue (teamTag,out Members);
//		return Members;
//	}
//}

