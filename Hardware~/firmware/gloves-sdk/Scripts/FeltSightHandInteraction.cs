using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;

namespace FeltSight
{
    public class FeltSightHandInteraction : MonoBehaviour
    {
        [Header("Hand Tracking")]
        [SerializeField] private XRHandTrackingEvents handTrackingEvents;
        [SerializeField] private Transform[] fingerTipTransforms = new Transform[5]; // Thumb to Pinky

        [Header("Raycast Settings")]
        [SerializeField] private float raycastDistance = 1.0f;
        [SerializeField] private LayerMask lidarMeshLayer;
        [SerializeField] private float hapticIntensityMultiplier = 1.0f;

        [Header("Haptic Feedback")]
        [SerializeField] private float minHapticIntensity = 0.1f;
        [SerializeField] private float maxHapticIntensity = 1.0f;
        [SerializeField] private float surfaceTypeMultiplier = 1.5f;

        private ARMeshManager meshManager;
        private Dictionary<MeshClassification, float> surfaceTypeIntensities = new Dictionary<MeshClassification, float>
        {
            { MeshClassification.None, 0.1f },
            { MeshClassification.Wall, 0.3f },
            { MeshClassification.Floor, 0.4f },
            { MeshClassification.Ceiling, 0.2f },
            { MeshClassification.Table, 0.5f },
            { MeshClassification.Seat, 0.4f },
            { MeshClassification.Window, 0.2f },
            { MeshClassification.Door, 0.3f }
        };

        private void Awake()
        {
            meshManager = FindObjectOfType<ARMeshManager>();
            if (meshManager == null)
            {
                Debug.LogError("ARMeshManager not found in scene!");
            }
        }

        private void OnEnable()
        {
            if (handTrackingEvents != null)
            {
                handTrackingEvents.jointsUpdated.AddListener(OnHandJointsUpdated);
            }
        }

        private void OnDisable()
        {
            if (handTrackingEvents != null)
            {
                handTrackingEvents.jointsUpdated.RemoveListener(OnHandJointsUpdated);
            }
        }

        private void OnHandJointsUpdated(XRHandJointsUpdatedEventArgs args)
        {
            if (!args.hand.isTracked) return;

            // Update finger tip positions
            for (int i = 0; i < 5; i++)
            {
                XRHandJointID jointID = GetFingerTipJointID(i);
                if (args.hand.GetJoint(jointID).TryGetPose(out Pose pose))
                {
                    fingerTipTransforms[i].position = pose.position;
                    fingerTipTransforms[i].rotation = pose.rotation;
                }
            }

            // Perform raycasts and update haptic feedback
            UpdateHapticFeedback(args.hand);
        }

        private void UpdateHapticFeedback(XRHand hand)
        {
            for (int i = 0; i < 5; i++)
            {
                if (!hand.GetJoint(GetFingerTipJointID(i)).TryGetPose(out Pose pose)) continue;

                // Create ray from finger tip
                Ray ray = new Ray(pose.position, pose.forward);
                RaycastHit hit;

                // Perform raycast against LiDAR mesh
                if (Physics.Raycast(ray, out hit, raycastDistance, lidarMeshLayer))
                {
                    // Get mesh classification
                    MeshClassification classification = GetMeshClassification(hit.collider);
                    float baseIntensity = surfaceTypeIntensities[classification];

                    // Calculate distance-based intensity
                    float distanceIntensity = 1.0f - (hit.distance / raycastDistance);
                    float finalIntensity = Mathf.Lerp(minHapticIntensity, maxHapticIntensity, 
                        baseIntensity * distanceIntensity * hapticIntensityMultiplier);

                    // Update haptic feedback
                    FeltSightGlovesManager.Instance.SetFingerIntensity(i, finalIntensity);

                    // Debug visualization
                    Debug.DrawLine(pose.position, hit.point, Color.green);
                    Debug.Log($"Finger {i}: Hit {classification} at {hit.distance:F2}m, Intensity: {finalIntensity:F2}");
                }
                else
                {
                    // No hit, turn off haptic feedback
                    FeltSightGlovesManager.Instance.SetFingerIntensity(i, 0f);
                    Debug.DrawRay(pose.position, pose.forward * raycastDistance, Color.red);
                }
            }
        }

        private MeshClassification GetMeshClassification(Collider collider)
        {
            // Get the mesh classification from the collider's gameObject
            // This assumes you have a component on your mesh that stores the classification
            var meshInfo = collider.GetComponent<ARMeshClassificationInfo>();
            if (meshInfo != null)
            {
                return meshInfo.Classification;
            }
            return MeshClassification.None;
        }

        private XRHandJointID GetFingerTipJointID(int fingerIndex)
        {
            switch (fingerIndex)
            {
                case 0: return XRHandJointID.ThumbTip;
                case 1: return XRHandJointID.IndexTip;
                case 2: return XRHandJointID.MiddleTip;
                case 3: return XRHandJointID.RingTip;
                case 4: return XRHandJointID.LittleTip;
                default: return XRHandJointID.Invalid;
            }
        }

        // Helper class to store mesh classification info
        public class ARMeshClassificationInfo : MonoBehaviour
        {
            public MeshClassification Classification;
        }
    }
} 