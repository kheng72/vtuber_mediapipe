using UnityEngine;
using System.Collections.Generic;

public class MediaPipeFullBodyHumanoid : MonoBehaviour
{
    [System.Serializable]
    public class Limb
    {
        public string name;
        public HumanBodyBones startBone;
        public HumanBodyBones midBone;
        public HumanBodyBones endBone;
        
        // MediaPipe Landmark Indices
        public int startIdx;
        public int midIdx;
        public int endIdx;
        
        public bool flipNormal = false; 
        public bool useStrictHinge = true; 
        public bool project2D = false; // Ignore MP Z-depth for this limb

        [Header("Manual Bone Correction")]
        public Vector3 startBoneOffset = Vector3.zero;
        public Vector3 midBoneOffset = Vector3.zero;

        [HideInInspector] public Quaternion startOffset;
        [HideInInspector] public Quaternion midOffset;
    }

    public List<Limb> limbs = new List<Limb>();
    
    Animator animator;
    MediaPipeReceiver receiver;

    [Header("Settings")]
    public Vector3 worldScale = new Vector3(-1, -1, 1); // Mirror X, Flip Y, Positive Z (Depth matches view)
    public float smoothSpeed = 12f;
    public Vector3 bodyRotationOffset = new Vector3(0, 0, 0); 
    public Vector3 headRotationOffset = new Vector3(0, 0, 0); 
    public Vector3 footRotationOffset = new Vector3(0, 0, 0); 
    public bool showDebugLines = true;

    [Header("Refined Grounding")]
    public bool lockHipLocalHeight = true; // Set to true to prevent hips from floating away from root

    void Start()
    {
        animator = GetComponent<Animator>();
        receiver = GetComponent<MediaPipeReceiver>();

        if (animator == null) Debug.LogError("Animator component missing!");
        if (receiver == null) Debug.LogError("MediaPipeReceiver component missing!");

        if (limbs == null || limbs.Count == 0)
        {
            SetupDefaultLimbs();
        }

        // Set Legs to project 2D by default to satisfy "ignore leg detection" request
        foreach (var limb in limbs)
        {
            if (limb.name.Contains("Leg")) limb.project2D = true;
        }

        CaptureInitialOffsets();
    }

    void SetupDefaultLimbs()
    {
        limbs = new List<Limb>
        {
            // Fully Mirrored: Character Left uses User Right data
            new Limb { name = "Left Arm",  startBone = HumanBodyBones.LeftUpperArm, midBone = HumanBodyBones.LeftLowerArm, endBone = HumanBodyBones.LeftHand, startIdx = 12, midIdx = 14, endIdx = 16 },
            new Limb { name = "Right Arm", startBone = HumanBodyBones.RightUpperArm, midBone = HumanBodyBones.RightLowerArm, endBone = HumanBodyBones.RightHand, startIdx = 11, midIdx = 13, endIdx = 15 },
            new Limb { name = "Left Leg",  startBone = HumanBodyBones.LeftUpperLeg, midBone = HumanBodyBones.LeftLowerLeg, endBone = HumanBodyBones.LeftFoot, startIdx = 24, midIdx = 26, endIdx = 28 },
            new Limb { name = "Right Leg", startBone = HumanBodyBones.RightUpperLeg, midBone = HumanBodyBones.RightLowerLeg, endBone = HumanBodyBones.RightFoot, startIdx = 23, midIdx = 25, endIdx = 27 }
        };
    }

    Vector3 GetLimbHint(Limb limb)
    {
        // For legs, the hinge axis (normal) is side-to-side (Right)
        if (limb.name.Contains("Leg")) return animator.transform.right;
        
        // For arms, we usually bend forward/back
        return animator.transform.forward;
    }

    void CaptureInitialOffsets()
    {
        foreach (var limb in limbs)
        {
            Transform tStart = animator.GetBoneTransform(limb.startBone);
            Transform tMid = animator.GetBoneTransform(limb.midBone);
            Transform tEnd = animator.GetBoneTransform(limb.endBone);

            if (tStart && tMid && tEnd)
            {
                Vector3 dir1 = (tMid.position - tStart.position).normalized;
                Vector3 dir2 = (tEnd.position - tMid.position).normalized;
                Vector3 normal = Vector3.Cross(dir1, dir2).normalized;

                Vector3 hint = GetLimbHint(limb);
                if (normal.sqrMagnitude < 0.001f) normal = hint;
                else if (Vector3.Dot(normal, hint) < 0) normal = -normal;
                
                if (limb.flipNormal) normal = -normal;

                limb.startOffset = Quaternion.Inverse(Quaternion.LookRotation(dir1, normal)) * tStart.rotation;
                limb.midOffset = Quaternion.Inverse(Quaternion.LookRotation(dir2, normal)) * tMid.rotation;
            }
        }
    }

    void LateUpdate()
    {
        if (receiver.TryGet("24", out Vector3 rHipU) && receiver.TryGet("23", out Vector3 lHipU) &&
            receiver.TryGet("12", out Vector3 rShU) && receiver.TryGet("11", out Vector3 lShU))
        {
            Vector3 vLH = Vector3.Scale(rHipU, worldScale);
            Vector3 vRH = Vector3.Scale(lHipU, worldScale);
            Vector3 vLS = Vector3.Scale(rShU, worldScale); 
            Vector3 vRS = Vector3.Scale(lShU, worldScale);

            Vector3 hipCenter = (vLH + vRH) * 0.5f;
            Vector3 shoulderCenter = (vLS + vRS) * 0.5f;

            Vector3 up = (shoulderCenter - hipCenter).normalized;
            Vector3 right = (vRH - vLH).normalized;
            Vector3 forward = Vector3.Cross(right, up).normalized;

            UpdateBodyRoot(forward, up);
            UpdateHead(forward, up);
        }

        foreach (var limb in limbs) ProcessLimb(limb);
    }

    void UpdateBodyRoot(Vector3 forward, Vector3 up)
    {
        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        if (hips)
        {
            hips.rotation = Quaternion.Slerp(hips.rotation, Quaternion.LookRotation(forward, up) * Quaternion.Euler(bodyRotationOffset), Time.deltaTime * smoothSpeed);
            
            if (lockHipLocalHeight)
            {
                // Force hips to stay near the root's Y level so GroundLock can work predictably
                Vector3 lPos = hips.localPosition;
                lPos.y = Mathf.Lerp(lPos.y, 0, Time.deltaTime * smoothSpeed);
                hips.localPosition = lPos;
            }
        }
    }

    void UpdateHead(Vector3 bodyForward, Vector3 bodyUp)
    {
        if (receiver.TryGet("8", out Vector3 rEarU) && receiver.TryGet("7", out Vector3 lEarU) && receiver.TryGet("0", out Vector3 nose))
        {
            Vector3 vLE = Vector3.Scale(rEarU, worldScale);
            Vector3 vRE = Vector3.Scale(lEarU, worldScale);
            Vector3 vNose = Vector3.Scale(nose, worldScale);

            Vector3 headCenter = (vLE + vRE) * 0.5f;
            Vector3 headForward = (vNose - headCenter).normalized;
            
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head)
            {
                head.rotation = Quaternion.Slerp(head.rotation, Quaternion.LookRotation(headForward, bodyUp) * Quaternion.Euler(headRotationOffset), Time.deltaTime * smoothSpeed);
            }
        }
    }

    void ProcessLimb(Limb limb)
    {
        if (!receiver.TryGet(limb.startIdx.ToString(), out Vector3 p1) ||
            !receiver.TryGet(limb.midIdx.ToString(), out Vector3 p2) ||
            !receiver.TryGet(limb.endIdx.ToString(), out Vector3 p3))
            return;

        Vector3 v1 = Vector3.Scale(p1, worldScale);
        Vector3 v2 = Vector3.Scale(p2, worldScale);
        Vector3 v3 = Vector3.Scale(p3, worldScale);

        Vector3 dir1 = (v2 - v1).normalized;
        Vector3 dir2 = (v3 - v2).normalized;

        Vector3 normal;
        Vector3 hint = GetLimbHint(limb);

        // --- STABLE KNEE HINGE LOGIC (LEG ONLY) ---
        if (limb.name.Contains("Leg"))
        {
            // Use the Character's Right vector as the ABSOLUTE hinge axis (normal).
            // This forces the joint to ONLY rotate in the Forward-Up plane.
            normal = animator.transform.right;

            // Project MediaPipe directions strictly onto the Forward-Up plane.
            // This eliminates depth noise (Z) and side-flips.
            Vector3 fwd = animator.transform.forward;
            Vector3 up = animator.transform.up;

            dir1 = (fwd * Vector3.Dot(dir1, fwd) + up * Vector3.Dot(dir1, up)).normalized;
            dir2 = (fwd * Vector3.Dot(dir2, fwd) + up * Vector3.Dot(dir2, up)).normalized;

            // Production-Safe: Ensure the limb doesn't "flip" inside out due to project2D.
            // If the upper leg points too far back, clamp it.
            if (Vector3.Dot(dir1, fwd) < -0.3f) dir1 = Vector3.Slerp(dir1, -up, 0.5f).normalized;
        }
        else // --- DYNAMIC LIMB LOGIC (ARMS, ETC.) ---
        {
            normal = Vector3.Cross(dir1, dir2).normalized;
            if (Vector3.Dot(normal, hint) < 0) normal = -normal;
            
            // Stability: Fallback to hint if the limb is nearly straight
            if (Vector3.Angle(dir1, dir2) < 5f || normal.sqrMagnitude < 0.001f) 
                normal = hint;
        }

        // Apply Manual Bone Correction (Production-Safe Offsets)
        if (limb.flipNormal) normal = -normal;

        // Final Pose Application
        Transform tStart = animator.GetBoneTransform(limb.startBone);
        if (tStart)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir1, normal) * limb.startOffset;
            targetRot *= Quaternion.Euler(limb.startBoneOffset);
            tStart.rotation = Quaternion.Slerp(tStart.rotation, targetRot, Time.deltaTime * smoothSpeed);
        }

        Transform tMid = animator.GetBoneTransform(limb.midBone);
        if (tMid)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir2, normal) * limb.midOffset;
            targetRot *= Quaternion.Euler(limb.midBoneOffset);
            tMid.rotation = Quaternion.Slerp(tMid.rotation, targetRot, Time.deltaTime * smoothSpeed);
        }

        if (showDebugLines) Debug.DrawRay(v2, normal * 0.2f, Color.magenta);
        // --- PRODUCTION-SAFE FOOT LOCK ---
        // Keeps feet pointing forward and level based on body orientation
        if (limb.name.Contains("Leg"))
        {
            Transform foot = animator.GetBoneTransform(limb.endBone);
            if (foot)
            {
                // Align foot to Body Forward and Up
                Quaternion footTargetRot = Quaternion.LookRotation(animator.transform.forward, animator.transform.up);
                
                // Apply manual offset for different models (e.g. pivoting toes up/down)
                footTargetRot *= Quaternion.Euler(footRotationOffset);

                foot.rotation = Quaternion.Slerp(foot.rotation, footTargetRot, Time.deltaTime * smoothSpeed);
            }
        }

    }
}
