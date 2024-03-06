using UnityEngine;
using System.Collections;
using System.Collections.Generic;
//using UnityEngine.UI;
//using UnityEngine.SceneManagement;
//using System.IO;
//using System.Xml;
//using System.Xml.Serialization;
//using System;
//using System.Linq;
//#if UNITY_EDITOR
//using UnityEditor;
//#endif

///// <summary>
///// Connects to Photon Cloud, then enables GUI to create/join game rooms,
///// which loads the actual Race scene.
///// Inherits from PUNBehavior, overriding standard Photon's Event methods.
///// </summary>
//public class PUNMenu : PunBehaviour {

//	// References to GUI game objects, so they can be enabled/disabled and
//	// used to show useful messages to player.
//    public bool AutoConnect = true;
//    public Text messages;
//    private JoinRoomState state = JoinRoomState.others;
//    public byte Version = 1;
//    /// <summary>if we don't want to connect in Start(), we have to "remember" if we called ConnectUsingSettings()</summary>
//    private bool ConnectInUpdate = true;

//    enum JoinRoomState:int
//    {
//        others = 0,
//        searchingForOpponent = 1,
//    }

//	// Connects to photon
//	void Start () {
//		//TeamSet.Instance.loadCustomerInfo ("未定位置");
//		// in case weŕe getting back from a race, already connected to lobby

//		//if (PhotonNetwork.connected) 
//		//{
//		//	return;
//		//}
//	}

//    void Update()
//    {
//        //if (ConnectInUpdate && AutoConnect && !PhotonNetwork.connected)
//        //{
//        //    Debug.Log("Update() was called by Unity. Scene is loaded. Let's connect to the Photon Master Server. Calling: PhotonNetwork.ConnectUsingSettings();");
//        //    ConnectInUpdate = false;
//        //    PhotonNetwork.ConnectUsingSettings(Version + "." + SceneManagerHelper.ActiveSceneBuildIndex);//这貌似是个原则上只运行一次的东西
//        //}

//        //switch(this.state)
//        //{
//        //    case JoinRoomState.others:
//        //        break;
//        //    case JoinRoomState.searchingForOpponent:
//        //        if (PhotonNetwork.room.PlayerCount > 1)
//        //        {
//        //            this.state = JoinRoomState.others;
//        //            if (PhotonNetwork.isMasterClient)
//        //            {
//        //                CallLoadRace();
//        //            }
//        //        }
//        //        break;
//        //}
//    }



//    // For each valid game room, creates a join button.
//    public override void OnReceivedRoomListUpdate () {

//	}

//	// Called when finished editing nickname (which will also serve as 
//	// room name - if player creates one)
//	public void EnteredJoinRoomButton() {
//		PhotonNetwork.ConnectUsingSettings("v1.0");
//		//messages.text = "Connecting...";
//	}

//    // When connected to Photon, enable nickname editing (too)
//    public override void OnConnectedToMaster()
//    {
//		PhotonNetwork.JoinLobby ();
//		//messages.text = "Entering lobby...";        
//    }

//	// When connected to Photon Lobby, disable nickname editing and messages text, enables room list
//	public override void OnJoinedLobby () {
//		//messages.gameObject.SetActive(false);
//	}

//	// Called from UI
//	public void CreateGame () {
//		RoomOptions options = new RoomOptions();
//		options.MaxPlayers = 2;
//		PhotonNetwork.CreateRoom(PhotonNetwork.player.NickName, options, TypedLobby.Default);
//	}

//    public void JoinRandomRoom()
//    {
//        PhotonNetwork.JoinRandomRoom();
//    }

//	public override void OnPhotonCreateRoomFailed(object[] codeMessage) {
//		if ((short) codeMessage [0] == ErrorCode.GameIdAlreadyExists) {
//			PhotonNetwork.playerName += "-2";
//			CreateGame ();
//		}
//	}

//	private bool checkSameNameOnPlayersList(string name) {
//		foreach (PhotonPlayer pp in PhotonNetwork.otherPlayers) {
//			if (pp.NickName.Equals(name)) {
//				return true;
//			}
//		}
//		return false;
//	}

//	// if we join (or create) a room, no need for the create button anymore;
//	public override void OnJoinedRoom () {
//		if (checkSameNameOnPlayersList (PhotonNetwork.playerName)) {
//			string newName = PhotonNetwork.playerName;
//			do {
//				newName += "-2";
//			} while (checkSameNameOnPlayersList (newName));
//			PhotonNetwork.playerName = newName;
//		}
//        messages.text = "Searching for opponent";
//        state = JoinRoomState.searchingForOpponent;
//		SetCustomProperties(PhotonNetwork.player, 0, PhotonNetwork.playerList.Length - 1);
//	}

//	// (masterClient only) enables start race button
//	public override void OnCreatedRoom () {
//        messages.text = "Searching for opponent";
//		SetCustomProperties(PhotonNetwork.player, 0, PhotonNetwork.playerList.Length - 1);
//        state = JoinRoomState.searchingForOpponent;
//	}

//	// If master client, for every newly connected player, sets the custom properties for him
//	// car = 0, position = last (size of player list)
//	public override void OnPhotonPlayerConnected (PhotonPlayer newPlayer) {
//		if (PhotonNetwork.isMasterClient) {
//			SetCustomProperties (newPlayer, 0, PhotonNetwork.playerList.Length - 1);
//			//photonView.RPC("UpdateTrack", PhotonTargets.All, trackIndex);
//		}
//	}

//	// when a player disconnects from the room, update the spawn/position order for all
//	public override void OnPhotonPlayerDisconnected(PhotonPlayer disconnetedPlayer) {
//		if (PhotonNetwork.isMasterClient) {
//			int playerIndex = 0;
//			foreach (PhotonPlayer p in PhotonNetwork.playerList) {
//				SetCustomProperties(p, (int) p.CustomProperties["car"], playerIndex++);
//			}
//		}
//	}

//	public override void OnPhotonPlayerPropertiesChanged (object[] playerAndUpdatedProps) {
//		//UpdatePlayerList ();
//	}

//	public virtual void OnPhotonRandomJoinFailed()
//	{
//		Debug.Log("OnPhotonRandomJoinFailed() was called by PUN. No random room available, so we create one. Calling: PhotonNetwork.CreateRoom(null, new RoomOptions() {maxPlayers = 4}, null);");
//		PhotonNetwork.CreateRoom(null, new RoomOptions() { MaxPlayers = 2 }, null);
//	}
		
//	// masterClient only. Calls an RPC to start the race on all clients. Called from GUI
//	public void CallLoadRace() {
//		PhotonNetwork.room.IsOpen = false;
//		photonView.RPC("LoadRace", PhotonTargets.All);
//	}

//	// Loads race level (called once from masterClient)
//	// Use LoadLevel from Photon, otherwise it messes up the GOs created in
//	// between level changes
//	// The level loaded is related to the track chosen by the Master Client (updated via RPC).
//	[PunRPC]
//	public void LoadRace () {
//		FightSceneModeManager.Instance.setSceneMode (SceneMode.netFight);
//		PhotonNetwork.LoadLevel("NetDebugScene");
//	}

//	// sets and syncs custom properties on a network player (including masterClient)
//	private void SetCustomProperties(PhotonPlayer player, int car, int position) {
//		ExitGames.Client.Photon.Hashtable customProperties = new ExitGames.Client.Photon.Hashtable() { { "spawn", position }, {"car", car} };
//		player.SetCustomProperties(customProperties);
//	}
//}
