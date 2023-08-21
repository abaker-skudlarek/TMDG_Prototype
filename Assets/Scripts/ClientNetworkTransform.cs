using UnityEngine;
using System.Collections;
using Unity.Netcode.Components;

public class ClientNetworkTransform : NetworkTransform 
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
    
    
    // ----------------------------------------------------------------- // 
    // --------------------------- Functions --------------------------- // 
    // ----------------------------------------------------------------- // 

    protected override bool OnIsServerAuthoritative()
    {
		// This means we will be Client Authoritative
		// This is BAD for competitive games, because the client will control it's position, meaning cheating is easier
		// TODO: Update this to be server authoritative ASAP
	    return false;
    }
	
    // -------------------------------------------------------------------------------------------------------------- // 
	
}
