using GASHAPWN.UI;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;


// TODO: Use Cinemachine Camera and set cam transform and then set tracking target as the object?
// Have an intro cam and node cam and transition between after intro cam animation

namespace GASHAPWN {
    public class CollectionManager : MonoBehaviour
    {
        public static CollectionManager Instance;

        [Header("Camera References & Settings")]
        [SerializeField] private Camera mainCamera;
        private Animator mainCameraAnimator;
        [SerializeField] private float cameraMoveDuration = 0.8f;
        [SerializeField] private AnimationCurve cameraMoveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Scene References")]
        [SerializeField] private Animator doorAnimator;
        [SerializeField] public bool isCamIntro = true;
        [SerializeField] private Transform nodesParent;

        [Header("UI References")]
        [SerializeField] private CollectionGUI collectionGUI;

        [Header("Rotation Settings")]
        [SerializeField] private float rotationSpeed = 100f;

        // NODE MANAGEMENT
        private CollectionNode currentNode;
        private List<CollectionNode> collectionNodes = new List<CollectionNode>();
        private Coroutine cameraMoveCoroutine;
        private bool isMoving = false;
        private float currentRotationInput = 0f;

        // Whether the collection system is initialized and ready
        private bool initialized = false;


        /// PUBLIC METHODS ///

        /// <summary>
        /// Initialize the collection with player data
        /// </summary>
        public void InitializeCollection(List<CollectedFigure> collectedFigures)
        {
            if (initialized)
                return;

            // Find all collection nodes in the scene
            FindAllNodes();

            // Update nodes based on collected figures
            UpdateNodesFromCollection(collectedFigures);

            // Select the first collected node
            SelectFirstCollectedNode();

            // Verify that nodes are properly linked
            VerifyNodeConnections();

            initialized = true;
        }

        // Selects a specific collection node
        public void SelectNode(CollectionNode node, bool animate = true)
        {
            if (node == null || isMoving)
                return;

            // Disable camera animator to ensure it doesn't override our positioning
            if (mainCameraAnimator != null && mainCameraAnimator.enabled)
                mainCameraAnimator.enabled = false;

            // Update node visual states
            if (currentNode != null)
                currentNode.UpdateVisualState(false);

            // Store the new current node
            currentNode = node;
            currentNode.UpdateVisualState(true);

            // Move camera to the node
            if (animate)
            {
                if (cameraMoveCoroutine != null)
                    StopCoroutine(cameraMoveCoroutine);

                cameraMoveCoroutine = StartCoroutine(MoveCamera(currentNode.transform));
            }
            else
            {
                // Instantly move camera
                mainCamera.transform.position = node.cameraPosition.position;
                mainCamera.transform.rotation = node.cameraPosition.rotation;
            }

            // Display the figure
            currentNode.DisplayFigure();

            // Update UI with figure information
            if (collectionGUI != null)
            {
                collectionGUI.SwitchFigureGUI(currentNode.associatedFigure, currentNode.isCollected, currentNode.amount);
            }
        }

        // Navigate to the next node using ForceJumpToNode
        public void NavigateNext()
        {
            if (currentNode != null && currentNode.nextNode != null)
            {
                int nextIndex = collectionNodes.IndexOf(currentNode.nextNode);
                if (nextIndex >= 0)
                {
                    ForceJumpToNode(nextIndex);
                    Audio.UI_SFXManager.Instance.Play_LeftRightButtonSelection();
                }
                else
                {
                    // Fallback to regular SelectNode if index isn't found
                    SelectNode(currentNode.nextNode);
                    Audio.UI_SFXManager.Instance.Play_LeftRightButtonSelection();
                }
            }
        }

        // Navigate to the previous node using ForceJumpToNode
        public void NavigatePrevious()
        {
            if (currentNode != null && currentNode.previousNode != null)
            {
                int prevIndex = collectionNodes.IndexOf(currentNode.previousNode);
                if (prevIndex >= 0)
                {
                    ForceJumpToNode(prevIndex);
                    Audio.UI_SFXManager.Instance.Play_LeftRightButtonSelection();
                }
                else
                {
                    // Fallback to regular SelectNode if index isn't found
                    SelectNode(currentNode.previousNode);
                    Audio.UI_SFXManager.Instance.Play_LeftRightButtonSelection();
                }
            }
        }

        // Force jump to a specific node by index
        public void ForceJumpToNode(int nodeIndex)
        {
            if (nodeIndex >= 0 && nodeIndex < collectionNodes.Count)
            {
                // Disable animator
                if (mainCameraAnimator != null)
                    mainCameraAnimator.enabled = false;

                // Get the node
                CollectionNode node = collectionNodes[nodeIndex];

                // Update current node
                if (currentNode != null)
                    currentNode.UpdateVisualState(false);

                currentNode = node;
                currentNode.UpdateVisualState(true);

                // Force camera position immediately
                mainCamera.transform.position = node.cameraPosition.position;
                mainCamera.transform.rotation = node.cameraPosition.rotation;

                // Display figure
                currentNode.DisplayFigure();

                // Update UI
                if (collectionGUI != null)
                {
                    collectionGUI.SwitchFigureGUI(currentNode.associatedFigure, currentNode.isCollected, currentNode.amount);
                }
            }
        }

        // Rotate the current figure
        public void RotateFigure(float amount)
        {
            if (currentNode != null)
            {
                currentNode.RotateFigure(amount * rotationSpeed * Time.deltaTime);
            }
        }


        /// PRIVATE METHODS ///

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(this);
                return;
            }

            // Find references if not set
            if (mainCamera == null)
                mainCamera = Camera.main;

            mainCameraAnimator = mainCamera.GetComponent<Animator>();

            if (collectionGUI == null)
                collectionGUI = FindFirstObjectByType<CollectionGUI>();
        }

        private void Start()
        {
            // Start with UI inactive
            collectionGUI.CollectionGUISetActive(false);

            // Handle Camera Intro, then initialize
            if (isCamIntro) {
                StartCoroutine(PlayIntroSequence());
            } 
            // Just initialize
            else {
                InitializeFromGameManager();
                collectionGUI.CollectionGUISetActive(true);
            }
            // If still not initialized, failsafe
            if (!initialized)
            {
                FailsafeInitialize();
            }
        }

        private void Update()
        {
            // Handle continuous rotation input
            if (currentRotationInput != 0 && currentNode != null)
            {
                currentNode.RotateFigure(currentRotationInput * rotationSpeed * UnityEngine.Time.deltaTime);
            }

            // Test navigation with number keys - for testing
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1))
            {
                ForceJumpToNode(0);
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2))
            {
                ForceJumpToNode(1);
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha3))
            {
                ForceJumpToNode(2);
            }
        }

        // Initialize collection data from GameManager
        private void InitializeFromGameManager()
        {
            if (GameManager.Instance != null)
            {
                // IN FUTURE: Select corresponding CollectionData corresponding to profile
                InitializeCollection(GameManager.Instance.currPlayerCollectionData.collection);
            }
            else
            {
                Debug.LogError("CollectionManager: GameManager not found!");
            }
        }

        // If collection did not initialize, failsafe method
        private void FailsafeInitialize()
        {     
            // Only force select if no node is currently selected or if we're not on the correct first node
            if (currentNode == null || 
                (collectionNodes.Count > 0 && currentNode != collectionNodes[0]))
            {
                Debug.Log("Failsafe: Forcing selection of first node");
                ForceJumpToNode(0);  // Always select first node
            }
        }

        // Find all collection nodes in the scene
        private void FindAllNodes()
        {
            collectionNodes.Clear();

            if (nodesParent != null)
            {
                // Find nodes under the specified parent
                CollectionNode[] nodes = nodesParent.GetComponentsInChildren<CollectionNode>();
                collectionNodes.AddRange(nodes);
            }
            else
            {
                // Find all nodes in the scene
                var nodes = FindObjectsByType<CollectionNode>(FindObjectsSortMode.InstanceID);
                collectionNodes.AddRange(nodes);
            }

            // Sort nodes by series and then by number in series
            collectionNodes.Sort((a, b) => {
                // First compare series names alphabetically
                int seriesComparison = string.Compare(
                    a.associatedFigure?.GetSeries()?.SeriesName ?? "", 
                    b.associatedFigure?.GetSeries()?.SeriesName ?? ""
                );
                
                if (seriesComparison != 0)
                    return seriesComparison;
                    
                // Then compare by number in series
                return a.associatedFigure?.GetNumberInSeries() ?? 0 - 
                    b.associatedFigure?.GetNumberInSeries() ?? 0;
            });
        }
        
        // Update nodes based on the player's collection
        private void UpdateNodesFromCollection(List<CollectedFigure> collectedFigures)
        {
            if(collectedFigures.Count == 0)
            {
                foreach(CollectionNode node in collectionNodes)
                {
                    node.isCollected = false;
                    node.UpdateVisualState(false);
                }
            }
            else
            {
                // Create a lookup for faster access
                Dictionary<string, CollectedFigure> collectedLookup = new Dictionary<string, CollectedFigure>();
                foreach (var item in collectedFigures)
                {
                    if (item.ID != null)
                    {
                        collectedLookup[item.ID] = item;
                    }
                }

                // Update each node
                foreach (CollectionNode node in collectionNodes)
                {
                    if (node.associatedFigure != null)
                    {
                        string figureId = node.associatedFigure.GetID();
                        if (collectedLookup.ContainsKey(figureId))
                        {
                            node.isCollected = true;
                            node.amount = collectedLookup[figureId].amount;
                        }
                        node.UpdateVisualState(false);
                    }
                }
            }
        }

        // Select the first collected node
        private void SelectFirstCollectedNode()
        {
            // Try to find a collected node in the first series
            string firstSeriesName = null;
            CollectionNode firstCollectedNode = null;
            
            foreach (CollectionNode node in collectionNodes)
            {
                if (node.associatedFigure == null || node.associatedFigure.GetSeries() == null)
                    continue;
                    
                string seriesName = node.associatedFigure.GetSeries().SeriesName;
                
                // Set the first series name if not already set
                if (firstSeriesName == null)
                    firstSeriesName = seriesName;
                    
                // If we're still in the first series and this node is collected
                if (seriesName == firstSeriesName && node.isCollected)
                {
                    firstCollectedNode = node;
                    break;  // Found our node, break out
                }
            }
            
            // If we found a collected node in the first series, select it
            if (firstCollectedNode != null)
            {
                SelectNode(firstCollectedNode, false);
                return;
            }
            
            // If no collected nodes found in first series, 
            // fall back to the first node regardless of collection status
            if (collectionNodes.Count > 0)
            {
                SelectNode(collectionNodes[0], false);
            }
        }
        
        // Verify node connections to ensure proper navigation
        private void VerifyNodeConnections()
        {
            foreach (var node in collectionNodes)
            {
                // Check prev/next links
                if (node.nextNode != null && node.nextNode.previousNode != node)
                {
                    node.nextNode.previousNode = node;
                }
                
                if (node.previousNode != null && node.previousNode.nextNode != node)
                {
                    node.previousNode.nextNode = node;
                }
            }
        }

        // Plays intro animation sequence then initializes collection
        private IEnumerator PlayIntroSequence()
        {
            // Enable camera animator
            if (mainCameraAnimator != null)
            {
                mainCameraAnimator.enabled = true;
                mainCameraAnimator.SetBool("isCamIntro", true);
            }

            // Open door
            if (doorAnimator != null)
            {
                doorAnimator.SetTrigger("openDoor");
            }

            // TODO: Wait until animation complete instead
            // Wait for animations to progress
            yield return new WaitForSeconds(6f);

            // Initialize collection while animations are still playing
            InitializeFromGameManager();

            if (collectionNodes.Count > 0)
            {
                Debug.Log($"Selecting first node after animation: {collectionNodes[0].name}");
                ForceJumpToNode(0);
            }

            // Activate UI
            collectionGUI.CollectionGUISetActive(true);

            // Disable animator after intro to prevent it from interfering with camera control
            if (mainCameraAnimator != null)
            {
                mainCameraAnimator.enabled = false;
            }
        }

        // Coroutine to smoothly move the camera to a target transform
        private IEnumerator MoveCamera(Transform target)
        {
            isMoving = true;

            // Ensure target is valid
            if (target == null)
            {
                isMoving = false;
                yield break;
            }

            Vector3 startPosition = mainCamera.transform.position;
            Quaternion startRotation = mainCamera.transform.rotation;

            Vector3 targetPosition = target.position;
            Quaternion targetRotation = target.rotation;

            float elapsed = 0f;

            while (elapsed < cameraMoveDuration)
            {
                elapsed += Time.deltaTime;
                float normalizedTime = elapsed / cameraMoveDuration;
                float curveValue = cameraMoveCurve.Evaluate(normalizedTime);

                // Move and rotate camera
                mainCamera.transform.position = Vector3.Lerp(startPosition, targetPosition, curveValue);
                mainCamera.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, curveValue);

                yield return null;
            }

            // Ensure we end at exactly the target
            mainCamera.transform.position = targetPosition;
            mainCamera.transform.rotation = targetRotation;

            isMoving = false;
        }


        /// DEBUG ///

        // Debug function: Adds a collection of random figures and updates the nodes
        public void AddRandomCollection(int amountOfFigures)
        {
            // Create a new collection and a checking list for already added figures
            List<CollectedFigure> randomCollection = new();
            List<Figure> randomFigures = new();

            // Create a set amount of random figures
            for (int i = 0; i < amountOfFigures; i++)
            {
                CollectedFigure randomCollectedFigure = new();
                Figure newRandomFigure = FigureManager.Instance.GetRandomFigure();

                // Check the checking list for duplicate figures
                if (randomFigures.Contains(newRandomFigure)) continue;
                else
                {
                    randomFigures.Add(newRandomFigure);
                    randomCollectedFigure.ID = newRandomFigure.GetID();
                    // Generate a random amount collected
                    randomCollectedFigure.amount = UnityEngine.Random.Range(0, 10);
                    randomCollection.Add(randomCollectedFigure);
                }
            }
            UpdateNodesFromCollection(randomCollection);
        }

        // Debug function: Removes the current collection from the nodes
        public void RemoveCollection()
        {
            foreach (CollectionNode node in collectionNodes)
            {
                if (node.associatedFigure != null)
                {
                    node.associatedFigure = null;
                    node.isCollected = false;
                    node.UpdateVisualState(false);
                }
            }
        }
    }
}