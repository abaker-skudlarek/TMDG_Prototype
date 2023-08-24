using UnityEngine;
using System.Collections;
using StarterAssets;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class SniperCameraController : MonoBehaviour 
{
    // ----------------------------------------------------------------- // 
    // --------------------------- Constants --------------------------- // 
    // ----------------------------------------------------------------- // 
    
    private const string KEYBOARD_MOUSE_CONTROL_SCHEME = "KeyboardMouse";
    private const string MAIN_CAMERA_TAG			   = "MainCamera";
    private const float  INPUT_THRESHOLD			   = 0.01f;

    // ----------------------------------------------------------------- // 
    // --------------------------- Variables --------------------------- // 
    // ----------------------------------------------------------------- // 
    
    // Public Variables
    
    
    // Private Variables
    private GameObject			_mainCamera;  // Reference to the main Cinemachine Brain camera
	private PlayerInput			_playerInput; // Reference to the PlayerInput component on this object
	private StarterAssetsInputs _input;		  // Reference to the StarterAssetsInputs Class. Used to process character and mouse input 
											  // TODO: in the actual game, we'd probably have a different class per player. The Sniper player doesn't need input for sprinting, for example
	private	float _xAxisRotation;			  // Used to rotate up and down on the X axis
	private float _yAxisRotation;		      // Used to rotate left and right on the Y axis

	// Serialized Field Variables
	[Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]	
	[SerializeField] private GameObject CinemachineCameraTarget;
	[Tooltip("Speed of camera movement in response to physical mouse movement")]
	public float MouseSensitivity = 1f;	
    [Tooltip("Rotation speed of the camera")]
    [SerializeField] private float RotationSpeed = 0.5f;
    [Tooltip("The total amount of degrees the camera can be moved left and right")]
    [SerializeField] private float YAxisRangeOfMotion;
    [Tooltip("The total amount of degrees the camera can be moved up and down")]
    [SerializeField] private float XAxisRangeOfMotion;
    
    // ----------------------------------------------------------------- // 
    // --------------------------- Functions --------------------------- // 
    // ----------------------------------------------------------------- // 
    	
	private void Awake() 
	{
		if (_mainCamera == null)
		{
			_mainCamera = GameObject.FindGameObjectWithTag(MAIN_CAMERA_TAG); 
		}
	}
	
    // -------------------------------------------------------------------------------------------------------------- // 
 
	private void Start()
	{
		_input = GetComponent<StarterAssetsInputs>();
		_playerInput = GetComponent<PlayerInput>();
	}
	
    // -------------------------------------------------------------------------------------------------------------- // 
	
	private void Update() 
	{
	
	}
	
    // -------------------------------------------------------------------------------------------------------------- // 
    
    // Camera movement should be done in LateUpdate() instead of Update()
	private void LateUpdate() 
	{
		CameraMovement();		
	}
	
    // -------------------------------------------------------------------------------------------------------------- // 
	
    private void CameraMovement()
    {
	    if (_input.look.sqrMagnitude >= INPUT_THRESHOLD) 
	    {
		    // If the current device is a mouse, we don't want to multiply by delta time. If it's a controller, we do
		    float deltaTimeMultiplier = IsCurrentDeviceMouse() ? 1.0f : Time.deltaTime;
		    
			// Adjust our pitch and rotation velocity based on the look of the proper axis
		    _xAxisRotation += _input.look.y * RotationSpeed * MouseSensitivity * deltaTimeMultiplier;
		    _yAxisRotation += _input.look.x * RotationSpeed * MouseSensitivity * deltaTimeMultiplier;
			  	
		    // Clamp our pitch and rotation before applying it. Divide the total range of motion by 2, so we have equal movement on both sides
		    _xAxisRotation = ClampAngle(_xAxisRotation, -(XAxisRangeOfMotion/2), (XAxisRangeOfMotion/2));
		    _yAxisRotation = ClampAngle(_yAxisRotation, -(YAxisRangeOfMotion/2), (YAxisRangeOfMotion/2));
		 	
		    // Rotate the whole object left, right, up, and down
		    transform.rotation = Quaternion.Euler(_xAxisRotation, _yAxisRotation, 0.0f);
	    }
    }
	 
    // -------------------------------------------------------------------------------------------------------------- // 
	
    /// <summary>
    /// Returns true if the current control scheme is keyboard and mouse, false otherwise
    /// </summary>
    private bool IsCurrentDeviceMouse()
    {
		return _playerInput.currentControlScheme == KEYBOARD_MOUSE_CONTROL_SCHEME;
    }
    
    // -------------------------------------------------------------------------------------------------------------- // 
    
	/// <summary>
	///	Used to ensure that the angle we want to clamp is not over or under 360 degrees before we try to clamp 
	/// </summary>
    private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
    {
	    if (lfAngle < -360f) lfAngle += 360f;
		if (lfAngle > 360f)  lfAngle -= 360f;
		return Mathf.Clamp(lfAngle, lfMin, lfMax);
    }
    
    // -------------------------------------------------------------------------------------------------------------- // 
	
}
