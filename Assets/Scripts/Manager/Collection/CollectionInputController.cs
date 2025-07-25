using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using EasyTransition;
using UnityEngine.InputSystem.Composites;

namespace GASHAPWN
{
    /// <summary>
    /// Interface between Input and Collection
    /// </summary>
    [RequireComponent(typeof(CollectionManager))]
    public class CollectionInputController : MonoBehaviour
    {
        // reference to CollectionManager
        private CollectionManager collectionManager;
        // Reference to "Cancel" InputAction
        private InputAction cancelAction;

        private float rotationValue = 0f;
        private float navigateCooldown = 0.2f;
        private float navigateTimer = 0f;

        [Header("Debug")]
            // Toggle Debug.Log for CollectionInputController
            [SerializeField] private bool debugLog = true;


        /// PRIVATE METHODS ///

        private void Awake()
        {
            collectionManager = GetComponent<CollectionManager>();
        }

        private void OnEnable()
        {
            var inputActionAsset = GetComponent<PlayerInput>().actions;
            cancelAction = inputActionAsset["Cancel"];

            cancelAction.performed += HandleCancel;
            if (!cancelAction.enabled) { cancelAction.Enable(); }
        }

        private void Update()
        {
            // Update navigation cooldown timer
            if (navigateTimer > 0)
                navigateTimer -= Time.deltaTime;
                
            // Apply continuous rotation if value is non-zero
            if (Mathf.Abs(rotationValue) > 0.01f && collectionManager != null)
            {
                collectionManager.RotateFigure(rotationValue * Time.deltaTime);
            }
        }

        private void HandleCancel(InputAction.CallbackContext context)
        {
            TransitionManager.Instance().Transition("MainMenu", 0);
            GameManager.Instance.UpdateGameState(GameState.Title);
        }

        private void OnDisable()
        {
            cancelAction.performed -= HandleCancel;
            cancelAction.Disable();
        }

        /// PUBLIC METHODS ///

        public void OnNavigate(InputAction.CallbackContext context)
        {
            Vector2 input = context.ReadValue<Vector2>();
            
            // Only navigate when input exceeds threshold and cooldown is complete
            if (navigateTimer <= 0)
            {
                if (Mathf.Abs(input.x) > 0.5f)
                {
                    if (input.x > 0)
                        collectionManager.NavigateNext();
                    else
                        collectionManager.NavigatePrevious();
                    
                    // Set cooldown to prevent rapid navigation
                    navigateTimer = navigateCooldown;
                    DebugLog($"Navigation: {(input.x > 0 ? "Next" : "Previous")}");
                }
            }
        }
        
        // Rotation handler - used to rotate the figure
        public void OnRotate(InputAction.CallbackContext context)
        {
            // Get the raw rotation value from the input device
            rotationValue = context.ReadValue<float>() * 100f;

            // Debug.Log to verify we're getting both positive and negative values
            DebugLog($"Rotation value: {rotationValue}");
        }
        
        public void OnSubmit(InputAction.CallbackContext context)
        {
            DebugLog("Selection performed");
            Audio.UI_SFXManager.Instance.Play_GeneralButtonSelection();
        }


        /// DEBUG ///
        
        private void DebugLog(string message)
        {
            if (debugLog)
                Debug.Log($"CollectionInputController: {message}");
        }
    }
}