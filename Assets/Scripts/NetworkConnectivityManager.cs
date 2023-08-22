using System;
using UnityEngine;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using QFSW.QC;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

public class NetworkConnectivityManager : MonoBehaviour 
{
    // ----------------------------------------------------------------- // 
    // --------------------------- Constants --------------------------- // 
    // ----------------------------------------------------------------- // 

    private const int    MAX_PLAYERS			   = 2; // 2 players allowed in the game, 1 host, 1 client
    private const string DEFAULT_RELAY_JOIN_CODE   = "default_join_code";
    private const string KEY_RELAY_JOIN_CODE       = "Relay Join Code";
    private const string LOBBY_NAME			       = "Default Lobby Name";  // TODO: This should eventually be an option for the player, or randomly assigned. it probably won't be visible to the player?
    private const float  LOBBY_HEARTBEAT_TIMER_MAX = 15f;  // The amount of time in seconds to wait until a new heartbeat is sent to ensure the lobby stays up
    private const float  LOBBY_UPDATE_TIMER_MAX    = 1.1f; // The amount of time in seconds to wait until we can ask the lobby for updates. Rate limits: https://docs.unity.com/ugs/en-us/manual/lobby/manual/rate-limits
    
    // ----------------------------------------------------------------- // 
    // --------------------------- Variables --------------------------- // 
    // ----------------------------------------------------------------- // 
    
    // Public Variables
	public static NetworkConnectivityManager Instance { get; private set; }  // Make an instance of this class, so we can access it's public functions in other places
    
    // Private Variables
    private Lobby _joinedLobby;		    // The lobby that this player has joined
    private bool  _isLobbyHost = false; // True if the this player is the host of the lobby (they created the lobby), false otherwise
    private float _lobbyHeartbeatTimer; // Used to keep track of how long it's been since a heartbeat has been sent
    private float _lobbyUpdateTimer;    // Used to keep track of how long it's been since we gotten an update from the joined lobby 
    private ConcurrentQueue<string> _createdLobbyIds = new ConcurrentQueue<string>();  // This will hold the IDs of any lobbies that are created.
    
    // Serialized Field Variables
    
    
    // ----------------------------------------------------------------- // 
    // --------------------------- Functions --------------------------- // 
    // ----------------------------------------------------------------- // 
    	
	private void Awake()
	{
		Instance = this;  // Used to created an instance of this class
	}
	
    // -------------------------------------------------------------------------------------------------------------- // 
 
	private async void Start()
	{
		// Make a connection to the Unity multiplayer session	
		await UnityServices.InitializeAsync();

		AuthenticationService.Instance.SignedIn += () => {
			Debug.Log($"Signed in {AuthenticationService.Instance.PlayerId}");
		};

		// TODO: Eventually, signing in via other methods should be supported. Steam, Google, etc. 
		await AuthenticationService.Instance.SignInAnonymouslyAsync();
	}
	
    // -------------------------------------------------------------------------------------------------------------- // 
	
	private void Update() 
	{
		HandleLobbyHeartbeat();
		HandlePollForLobbyUpdates();	
	}
	
    // -------------------------------------------------------------------------------------------------------------- // 

    private void OnApplicationQuit()
    {
	    // Delete all lobbies that were created when the application quits. Only want to do this if we are the host
		if (_isLobbyHost)
		{
			while (_createdLobbyIds.TryDequeue(out var lobbyId))
			{
				Debug.Log($"Deleting lobby with ID: {lobbyId}");
				LobbyService.Instance.DeleteLobbyAsync(lobbyId);
			}
		}
    }

    // -------------------------------------------------------------------------------------------------------------- // 
    
    /// <summary>
    /// The lobby will be marked as inactive after 30 seconds of no communication. So we need to send a heartbeat
    /// every LOBBY_HEARTBEAT_TIMER_MAX seconds to ensure it stays up.
    /// </summary>
	private async void HandleLobbyHeartbeat()
	{
		// We only want to send the heartbeat if we have joined a lobby AND if we are the lobby host
		if (_joinedLobby != null && _isLobbyHost)
		{
			_lobbyHeartbeatTimer -= Time.deltaTime;
			if (_lobbyHeartbeatTimer < 0f)
			{
				_lobbyHeartbeatTimer = LOBBY_HEARTBEAT_TIMER_MAX;

				await LobbyService.Instance.SendHeartbeatPingAsync(_joinedLobby.Id);
			}
		}
	}
    
    // -------------------------------------------------------------------------------------------------------------- // 
	
    /// <summary>
    /// Ask the lobby for updates every LOBBY_UPDATE_TIMER_MAX seconds.
    /// Also, check if the lobby's relay join code has been updated. If it has, join the relay with the code.
    /// </summary>
    private async void HandlePollForLobbyUpdates()
    {
	    // We can only poll for updates if we have joined a lobby
	    if (_joinedLobby != null)
	    {
		    _lobbyUpdateTimer -= Time.deltaTime;
		    if (_lobbyUpdateTimer < 0f)
		    {
			    _lobbyUpdateTimer = LOBBY_UPDATE_TIMER_MAX;

				// Get the updated lobby and set it equal to _joinedLobby 
			    Lobby lobby = await LobbyService.Instance.GetLobbyAsync(_joinedLobby.Id);
			    _joinedLobby = lobby;
			    
				// Check if the relay join code has been updated from the default
				if (_joinedLobby.Data[KEY_RELAY_JOIN_CODE].Value != DEFAULT_RELAY_JOIN_CODE)
				{
					// If the code has been updated, and we aren't the host, join the relay with the join code	
					if (_isLobbyHost == false)
					{
						JoinRelay(_joinedLobby.Data[KEY_RELAY_JOIN_CODE].Value);
						_joinedLobby = null; // Reset the joined lobby so that we don't keep polling for updates
					}
				}
		    }
	    }
    }
    
    // -------------------------------------------------------------------------------------------------------------- // 
	
    [Command]
    public void CreateGame()
    {
	    Debug.Log("CreateGame() called");
	    
		Debug.Log("Creating Lobby");
		CreateLobby();
		
		Debug.Log("Creating Relay");
		CreateRelay();

		// This player has now created a lobby, so set declare it as being a host
		_isLobbyHost = true;
    }
    
    // -------------------------------------------------------------------------------------------------------------- // 
	
    [Command]
    public void JoinGame()
    {
	    Debug.Log("JoinGame() called");
	    
		Debug.Log("Quick joining lobby");
		QuickJoinLobby();

		// After the lobby is joined, the polling for lobby updates will check if the lobby join code has been populated.
		// If it has, then it will join the relay. This happens in HandlePollForLobbyUpdates()
    }
    
    // -------------------------------------------------------------------------------------------------------------- // 
    
    private async void CreateLobby()
    {
	    try
	    {
			// Create lobby options that will be used when the lobby is created 
			CreateLobbyOptions createLobbyOptions = new CreateLobbyOptions() {
				IsPrivate = false, // Not making this private, so that quick join works. TODO: This should eventually be an option that the player can tick on or off
				// Add a dictionary of data objects. This will allow us to define our own data to add to the lobby options	
				Data = new Dictionary<string, DataObject> {
					// This will be the relay join code. It will be updated once a relay code has been created, so that people who join the lobby will have access to the code. 
					{ KEY_RELAY_JOIN_CODE, new DataObject(DataObject.VisibilityOptions.Member, DEFAULT_RELAY_JOIN_CODE) }
				}
			};

			// Create the lobby using the lobby name, max players, and the lobby options defined above		
			var createdLobby = await LobbyService.Instance.CreateLobbyAsync(LOBBY_NAME, MAX_PLAYERS, createLobbyOptions);
			
			// Add the newly created lobby's ID to our queue. This will be used to delete the lobby when the application quits
		    _createdLobbyIds.Enqueue(createdLobby.Id);

			// Make sure to set the lobby reference so it can be used by this object 
			_joinedLobby = createdLobby;
			
			PrintPlayersInLobby(_joinedLobby);
	    }
	    catch (LobbyServiceException e)
	    {
		    Debug.Log(e);
	    } 
    }
    
    // -------------------------------------------------------------------------------------------------------------- // 
	
	private	async void QuickJoinLobby()
	{
		try
		{
			Lobby lobby = await Lobbies.Instance.QuickJoinLobbyAsync();
			_joinedLobby = lobby;
			
			Debug.Log($"Quick joined lobby with name {_joinedLobby.Name}");
			
			PrintPlayersInLobby(_joinedLobby);
		}
		catch (LobbyServiceException e)
		{
			Debug.Log(e);
		}
	}
	
    // -------------------------------------------------------------------------------------------------------------- // 
    
	private async void CreateRelay()
	{
		try
		{
			// maxConnections does not include the Host, so this number is how many players will be connecting.
			//  Subtract 1 from the MAX_PLAYERS, since MAX_PLAYERS does include the host
			Allocation allocation = await RelayService.Instance.CreateAllocationAsync(MAX_PLAYERS - 1);

			string relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

			// This will create a new Relay server using the allocation we just created	
			RelayServerData relayServerData = new RelayServerData(allocation, "dtls");
			
			// Set the relay server on the NetworkManager	
			NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

			NetworkManager.Singleton.StartHost();

			// Update the lobby with the relay join code from the newly created relay. Other players in the lobby will then be able to use it	
			Debug.Log($"Updating lobby with join code {relayJoinCode}");
			Lobby lobby = await Lobbies.Instance.UpdateLobbyAsync(_joinedLobby.Id, new UpdateLobbyOptions() {
				Data = new Dictionary<string, DataObject> {
					{ KEY_RELAY_JOIN_CODE, new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode) }
				}
			});

			// Update the joined lobby to the lobby with the new options	
			_joinedLobby = lobby;
		}
		catch (RelayServiceException e)
		{
			Debug.Log(e);
		}	
	}
	
    // -------------------------------------------------------------------------------------------------------------- // 
	
	private async void JoinRelay(string joinCode)
	{
		try
		{
			Debug.Log($"Joining relay with code {joinCode}");

			JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

			RelayServerData relayServerData = new RelayServerData(joinAllocation, "dtls");
			
			NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

			NetworkManager.Singleton.StartClient();
		}
		catch (RelayServiceException e)
		{
			Debug.Log(e);
		}	
	}
    
    // -------------------------------------------------------------------------------------------------------------- // 
    
	private void PrintPlayersInLobby(Lobby lobby)
	{
		Debug.Log($"Players in lobby {lobby.Name}");
		foreach (var player in lobby.Players)
		{
			Debug.Log(player.Id);	
		}
	}
    
    // -------------------------------------------------------------------------------------------------------------- // 
	
}
