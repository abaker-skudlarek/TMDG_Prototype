using UnityEngine;
using System.Collections;
using QFSW.QC;
using Unity.Netcode;
using UnityEngine.UI;

public class NetworkUI : MonoBehaviour 
{
    // ----------------------------------------------------------------- // 
    // --------------------------- Constants --------------------------- // 
    // ----------------------------------------------------------------- // 



    // ----------------------------------------------------------------- // 
    // --------------------------- Variables --------------------------- // 
    // ----------------------------------------------------------------- // 
    
    // Public Variables
    
    
    // Private Variables
    
    
    // Serialized Field Variables
    [SerializeField] private Button createGameButton;
    [SerializeField] private Button joinGameButton;
    
    // ----------------------------------------------------------------- // 
    // --------------------------- Functions --------------------------- // 
    // ----------------------------------------------------------------- // 
    	
	private void Awake() 
	{
		createGameButton.onClick.AddListener(() => {
			Debug.Log("CreateGameButton clicked");
			
			NetworkConnectivityManager.Instance.CreateGame();
			
			Hide();
		});	
			
		joinGameButton.onClick.AddListener(() => {
			Debug.Log("JoinGameButton clicked");
			
			NetworkConnectivityManager.Instance.JoinGame();
			
			Hide();
		});	
	}
	
    // -------------------------------------------------------------------------------------------------------------- // 

    private void Hide()
    {
	    gameObject.SetActive(false);
    }
    
    // -------------------------------------------------------------------------------------------------------------- // 
	
}
