﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Valve.VR.InteractionSystem;

[RequireComponent(typeof(Interactable))]
public class InteractablePart : MonoBehaviour {

    // Interactable related
    private Hand.AttachmentFlags attachmentFlags = Hand.defaultAttachmentFlags & (~Hand.AttachmentFlags.SnapOnAttach) & (~Hand.AttachmentFlags.DetachOthers) & (~Hand.AttachmentFlags.VelocityMovement);

    private Interactable interactable;

    // Part replacement related
    public enum Mode { OutlinePart, InteractablePart };
    [Tooltip("Dictates the properties and behavier of the part")]
    public Mode partMode;
    public bool checkRotation = true;
    [Tooltip("Amount of deviation from a perfect rotation match on all axes")]
    public float acceptableDegrees = 10f;
    public bool checkPosition = true;
    [Tooltip("Amount of deviation from a perfect overlap in position")]
    public float acceptableMeters = 0.1f;
    [Tooltip("Material used for showing where replacement part is supposed to go. Default is OrangeOutline")]
    public Material defaultOutlineMaterial;
    [Tooltip("Material used for showing the user's replacement part is acceptable. Default is GreenOutline")]
    public Material acceptablePlacementMaterial;
    [Tooltip("Material used for showing the user's replacement part is not acceptable. Default is RedOutline")]
    public Material unacceptablePlacementMaterial;
    [Tooltip("Number of frames to skip between triggers")]
    public int frameSkip = 30;
    private int frameCount = 0;
    public UnityEvent onAcceptablePlacement;
    public UnityEvent onUnacceptablePlacement;

    private Dictionary<int, Material[]> originalMaterials;
    private Dictionary<string, Collider> originalColliders;
    private List<GameObject> allGameObjects;
    private bool isAcceptableRotation = false;
    private bool isAcceptablePosition = false;
    private Bounds selfGroupBounds;
    private Bounds otherGroupBounds;
    private bool selfBoundsExpired = true;
    private bool otherBoundsExpired = true;

    //-------------------------------------------------
    void Awake()
    {
        if (defaultOutlineMaterial == null)
        {
            defaultOutlineMaterial = Resources.Load("Materials/OutlineMatOrange") as Material;
        }
        if (acceptablePlacementMaterial == null)
        {
            acceptablePlacementMaterial = Resources.Load("Materials/OutlineMatGreen") as Material;
        }
        if (unacceptablePlacementMaterial == null)
        {
            unacceptablePlacementMaterial = Resources.Load("Materials/OutlineMatRed") as Material;
        }
    }


    // Use this for initialization
    void Start () {
        interactable = this.GetComponent<Interactable>();
        allGameObjects = FindAllGameObjectsAtOrBelow(gameObject);
        originalMaterials = SaveOriginalMaterials(allGameObjects);
        originalColliders = SaveOriginalColliders(allGameObjects);


        // Extra init stuff
        switch (partMode)
        {
            case Mode.OutlinePart:

                interactable.highlightOnHover = false;
                interactable.enabled = false;
                // Make sure main gameobject has a rigidbody so that a compound rigidbody can be created (any lower down rigidbodies will be destroyed later when applying defaults)
                if (gameObject.GetComponent<Rigidbody>() == null)
                {
                    gameObject.AddComponent<Rigidbody>().useGravity = false;
                }
                // TODO figure out why default material is not being set in ApplyModeDefaults, but this works for now
                ApplyMaterialToList(allGameObjects, defaultOutlineMaterial);
                break;
        }
        ApplyModeDefaults(allGameObjects);
    }
	

	// Update is called once per frame
	void Update () {
		
	}


    void FixedUpdate()
    {
        frameCount++;
    }


    void OnTriggerStay(Collider other)
    {
        Debug.Log(name + " OnTriggerStay");
        Collider matchingCollider;

        // TODO implement tag system to check that other is part of a replacement part
        if (frameCount % frameSkip != 0
            || partMode != Mode.OutlinePart
            || !originalColliders.TryGetValue(other.name, out matchingCollider))
        {
            return;
        }
        frameCount = 1;
        // Confirmed that the other part is an intended replacement
        // Condition checking
        if (!checkRotation || IsWithinRangeOfRotation(matchingCollider.transform.rotation, other.transform.rotation, acceptableDegrees))
        {
            isAcceptableRotation = true;
        }
        if ((!checkRotation || isAcceptableRotation) && (!checkPosition || CollidersWithinLimit(matchingCollider, other, acceptableMeters)))
        {
            isAcceptablePosition = true;
        }
        Debug.Log("IsAcceptableRotation: " + isAcceptableRotation + " IsAcceptablePosition: " + isAcceptablePosition);
        if ((!checkRotation || isAcceptableRotation) && (!checkPosition || isAcceptablePosition))
        {
            OnAcceptablePlacement();
            ApplyMaterialToList(allGameObjects, acceptablePlacementMaterial);
        }
        else
        {
            OnUnacceptablePlacement();
            ApplyMaterialToList(allGameObjects, unacceptablePlacementMaterial);
        }
        isAcceptableRotation = false;
        isAcceptablePosition = false;
    }


    void OnTriggerExit(Collider other)
    {
        isAcceptableRotation = false;
        isAcceptablePosition = false;
        otherBoundsExpired = true;
        if (partMode == Mode.OutlinePart)
            ApplyMaterialToList(allGameObjects, defaultOutlineMaterial);
    }


    void ApplyModeDefaultsToSelf()
    {
        ApplyModeDefaults(gameObject);
    }


    void ApplyModeDefaultsToChildren()
    {
        // Using transform because you cannot get children as gameobjects
        Transform[] transforms = GetComponentsInChildren<Transform>();
        foreach (Transform childTransform in transforms)
        {
            ApplyModeDefaults(childTransform.gameObject);
        }
    }


    void ApplyModeDefaults(List<GameObject> gameObjects)
    {
        Debug.Log("Total objects: " + gameObjects.Count);
        for (int i = 0; i < gameObjects.Count; i++)
        {
            ApplyModeDefaults(gameObjects[i]);
        }
    }


    void ApplyModeDefaults(GameObject obj)
    {
        Collider collider = obj.GetComponent<Collider>();
        Rigidbody rigidbody = obj.GetComponent<Rigidbody>();
        Renderer renderer = obj.GetComponent<Renderer>();
        switch (partMode)
        {

            case Mode.OutlinePart:
                if (collider != null)
                {
                    collider.isTrigger = true;
                    collider.enabled = true;
                }
                if (rigidbody != null)
                {
                    if (rigidbody != gameObject.GetComponent<Rigidbody>())
                    {
                        Destroy(rigidbody);
                    }
                    else
                    {
                        rigidbody.useGravity = false;
                        rigidbody.isKinematic = true;
                    }
                }
                if (renderer != null)
                {
                    renderer.materials = GetArrayOfMaterial(defaultOutlineMaterial, renderer.materials.Length);
                }
                break;
            case Mode.InteractablePart:
                if (collider != null)
                {
                    collider.isTrigger = false;
                    collider.enabled = true;
                }
                if (renderer != null)
                    renderer.materials = GetOriginalMaterials(renderer);
                break;
        }
    }


    private Dictionary<int, Material[]> SaveOriginalMaterials(List<GameObject> gameObjects)
    {
        Dictionary<int, Material[]> ogMaterials = new Dictionary<int, Material[]>();
        foreach (GameObject obj in gameObjects)
        {
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
                ogMaterials.Add(obj.GetInstanceID(), renderer.materials);
        }
        return ogMaterials;
    }


    private Dictionary<string, Collider> SaveOriginalColliders(List<GameObject> gameObjects)
    {
        Dictionary<string, Collider> ogColliders = new Dictionary<string, Collider>();
        foreach (GameObject obj in gameObjects)
        {
            Collider collider = obj.GetComponent<Collider>();
            ogColliders.Add(obj.name, collider);
        }
        return ogColliders;
    }


    private Dictionary<int, Rigidbody> SaveOriginalRigidbodies(List<GameObject> gameObjects)
    {
        Dictionary<int, Rigidbody> ogRigidbodies = new Dictionary<int, Rigidbody>();
        foreach (GameObject obj in gameObjects)
        {
            Rigidbody rigidbody = obj.GetComponent<Rigidbody>();
            ogRigidbodies.Add(obj.GetInstanceID(), rigidbody);
        }
        return ogRigidbodies;
    }


    private void ApplyMaterialToList(List<GameObject> gameObjects, Material mat)
    {
        for (int i = 0; i < gameObjects.Count; i++)
        {
            Renderer renderer = gameObjects[i].GetComponent<Renderer>();
            if (renderer != null)
                renderer.materials = GetArrayOfMaterial(mat, renderer.materials.Length);
        }
    }


    private Material[] GetOriginalMaterials(Renderer renderer)
    {
        if (renderer != null)
        {
            int InstanceId = renderer.gameObject.GetInstanceID();
            if (originalMaterials.ContainsKey(InstanceId))
            {
                return originalMaterials[InstanceId];
            }

        }
        return null;
    }


    private Material[] GetArrayOfMaterial(Material mat, int size)
    {
        Material[] materials = new Material[size];
        for (int i = 0; i < size; i++)
        {
            materials[i] = mat;
        }
        return materials;
    }


    private List<GameObject> FindAllGameObjectsAtOrBelow(GameObject start)
    {
        List<GameObject> gameObjects = new List<GameObject>();
        FindAllGameObjectsAtOrBelow(gameObject, gameObjects);
        return gameObjects;
    }

    private void FindAllGameObjectsAtOrBelow(GameObject start, List<GameObject> objects)
    {
        objects.Add(start);
        Transform[] transforms = start.GetComponentsInChildren<Transform>();

        foreach (Transform childTransform in transforms)
        {
            if (childTransform.parent == start.transform)
                FindAllGameObjectsAtOrBelow(childTransform.gameObject, objects);
        }
    }

    private bool IsWithinRangeOfRotation(Quaternion rot1, Quaternion rot2, float limit)
    {
        // Debug.Log("Rotation between objects: " + Quaternion.Angle(rot1, rot2));
        return Quaternion.Angle(rot1, rot2) <= limit ? true : false;
    }

    private bool CollidersWithinLimit(Collider collider1, Collider collider2, float limit)
    {
        return Vector3.Distance(collider1.bounds.center, collider2.bounds.center) <= limit ? true : false;
    }

    private bool IsWithinRangeOfCenter(Transform otherTransform, float limit)
    {
        if (selfBoundsExpired)
        {
            selfGroupBounds = CalculateGroupedBounds(this.transform);
            selfBoundsExpired = false;
        }
        if (otherBoundsExpired)
        {
            otherGroupBounds = CalculateGroupedBounds(otherTransform);
            otherBoundsExpired = false;
        }
        // Debug.Log("Distance between objects: " + Vector3.Distance(selfGroupBounds.center, otherGroupBounds.center));
        return Vector3.Distance(selfGroupBounds.center, otherGroupBounds.center) <= limit ? true : false;
    }


    private Bounds CalculateGroupedBounds(Transform trans)
    {
        Renderer[] renderers = trans.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers[0].bounds; // Might need to check that renderer is null first
        foreach (Renderer renderer in renderers)
        {
            if (renderer != null)
                bounds.Encapsulate(renderer.bounds);
        }
        return bounds;
    }


    private void OnAcceptablePlacement()
    {
        onAcceptablePlacement.Invoke();
    }


    private void OnUnacceptablePlacement()
    {
        onUnacceptablePlacement.Invoke();
    }

    public void VibrateController(Hand hand, float durationSec, float frequency, float amplitude)
    {
        StartCoroutine(VibrateControllerContinuous(hand, durationSec, frequency, amplitude));
    }


    IEnumerator VibrateControllerContinuous(Hand hand, float durationSec, float frequency, float amplitude)
    {
        // if true the pulse will happen in a sawtooth pattern like this /|/|/|/|
        // else it will happen opposite like this |\|\|\|\
        hand.TriggerHapticPulse(durationSec, frequency, amplitude);
        yield break;

    }

    //-------------------------------------------------
    // Called when a Hand starts hovering over this object
    //-------------------------------------------------
    private void OnHandHoverBegin(Hand hand)
    {
        VibrateController(hand, 0.15f, 5f, 1f);
    }


    //-------------------------------------------------
    // Called when a Hand stops hovering over this object
    //-------------------------------------------------
    private void OnHandHoverEnd(Hand hand)
    {
    }


    //-------------------------------------------------
    // Called every Update() while a Hand is hovering over this object
    //-------------------------------------------------
    private void HandHoverUpdate(Hand hand)
    {
        switch (partMode)
        {
            case Mode.InteractablePart:
                GrabTypes startingGrabType = hand.GetGrabStarting();
                bool isGrabEnding = hand.IsGrabEnding(this.gameObject);

                if (interactable.attachedToHand == null && startingGrabType != GrabTypes.None)
                {
                    // Call this to continue receiving HandHoverUpdate messages,
                    // and prevent the hand from hovering over anything else
                    hand.HoverLock(interactable);

                    // Attach this object to the hand
                    hand.AttachObject(gameObject, startingGrabType, attachmentFlags);
                }
                else if (isGrabEnding)
                {
                    // Detach this object from the hand
                    hand.DetachObject(gameObject);

                    // Call this to undo HoverLock
                    hand.HoverUnlock(interactable);
                }
                break;
            case Mode.OutlinePart:
                break;
        }
        
    }


    //-------------------------------------------------
    // Called when this GameObject becomes attached to the hand
    //-------------------------------------------------
    private void OnAttachedToHand(Hand hand)
    {
    }


    //-------------------------------------------------
    // Called when this GameObject is detached from the hand
    //-------------------------------------------------
    private void OnDetachedFromHand(Hand hand)
    {
    }


    //-------------------------------------------------
    // Called every Update() while this GameObject is attached to the hand
    //-------------------------------------------------
    private void HandAttachedUpdate(Hand hand)
    {
    }


    //-------------------------------------------------
    // Called when this attached GameObject becomes the primary attached object
    //-------------------------------------------------
    private void OnHandFocusAcquired(Hand hand)
    {
    }


    //-------------------------------------------------
    // Called when another attached GameObject becomes the primary attached object
    //-------------------------------------------------
    private void OnHandFocusLost(Hand hand)
    {
    }
}
