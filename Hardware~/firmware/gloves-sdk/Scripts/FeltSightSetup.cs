using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace FeltSight
{
    public class FeltSightSetup : MonoBehaviour
    {
        [Header("Required Components")]
        [SerializeField] private ARMeshManager meshManager;
        [SerializeField] private XRHandTrackingEvents handTrackingEvents;
        [SerializeField] private FeltSightHandInteraction handInteraction;

        [Header("Layer Settings")]
        [SerializeField] private string lidarMeshLayerName = "LiDARMesh";

        private void Awake()
        {
            // Ensure ARMeshManager exists
            if (meshManager == null)
            {
                meshManager = FindObjectOfType<ARMeshManager>();
                if (meshManager == null)
                {
                    Debug.LogError("ARMeshManager not found! Please add it to your scene.");
                    return;
                }
            }

            // Ensure XRHandTrackingEvents exists
            if (handTrackingEvents == null)
            {
                handTrackingEvents = FindObjectOfType<XRHandTrackingEvents>();
                if (handTrackingEvents == null)
                {
                    Debug.LogError("XRHandTrackingEvents not found! Please add it to your scene.");
                    return;
                }
            }

            // Create or find FeltSightHandInteraction
            if (handInteraction == null)
            {
                handInteraction = FindObjectOfType<FeltSightHandInteraction>();
                if (handInteraction == null)
                {
                    GameObject handInteractionObj = new GameObject("FeltSightHandInteraction");
                    handInteraction = handInteractionObj.AddComponent<FeltSightHandInteraction>();
                }
            }

            // Setup LiDAR mesh layer
            SetupLiDARMeshLayer();

            // Configure mesh manager
            ConfigureMeshManager();
        }

        private void SetupLiDARMeshLayer()
        {
            // Create layer if it doesn't exist
            int layerIndex = LayerMask.NameToLayer(lidarMeshLayerName);
            if (layerIndex == -1)
            {
                Debug.LogWarning($"Layer '{lidarMeshLayerName}' not found. Creating new layer.");
                // Note: You'll need to manually add the layer in Unity's Layer settings
                Debug.Log("Please add the layer manually in Unity's Layer settings");
            }

            // Set up layer mask for raycasting
            handInteraction.SetLayerMask(1 << LayerMask.NameToLayer(lidarMeshLayerName));
        }

        private void ConfigureMeshManager()
        {
            // Enable mesh classification if available
            if (meshManager.descriptor.supportsMeshClassification)
            {
                meshManager.meshPrefab = CreateMeshPrefab();
                meshManager.meshClassificationEnabled = true;
            }
            else
            {
                Debug.LogWarning("Mesh classification not supported on this device.");
            }
        }

        private GameObject CreateMeshPrefab()
        {
            // Create a prefab with mesh collider and classification info
            GameObject prefab = new GameObject("LiDARMeshPrefab");
            
            // Add mesh filter and renderer
            prefab.AddComponent<MeshFilter>();
            MeshRenderer renderer = prefab.AddComponent<MeshRenderer>();
            renderer.material = new Material(Shader.Find("Standard"));
            renderer.material.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);

            // Add mesh collider
            MeshCollider collider = prefab.AddComponent<MeshCollider>();
            collider.convex = false;

            // Add classification info component
            prefab.AddComponent<FeltSightHandInteraction.ARMeshClassificationInfo>();

            // Set layer
            prefab.layer = LayerMask.NameToLayer(lidarMeshLayerName);

            return prefab;
        }

        private void OnValidate()
        {
            // Validate layer name
            if (string.IsNullOrEmpty(lidarMeshLayerName))
            {
                Debug.LogError("LiDAR mesh layer name cannot be empty!");
                lidarMeshLayerName = "LiDARMesh";
            }
        }
    }
} 