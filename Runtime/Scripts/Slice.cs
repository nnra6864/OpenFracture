using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(Rigidbody))]
public class Slice : MonoBehaviour
{
    public SliceOptions sliceOptions;
    public CallbackOptions callbackOptions;

    /// <summary>
    /// The number of times this fragment has been re-sliced.
    /// </summary>
    private int currentSliceCount;

    /// <summary>
    /// Collector object that stores the produced fragments
    /// </summary>
    private GameObject fragmentRoot;

    /// <summary>
    /// Slices the attached mesh along the cut plane
    /// </summary>
    /// <param name="sliceNormalWorld">The cut plane normal vector in world coordinates.</param>
    /// <param name="sliceOriginWorld">The cut plane origin in world coordinates.</param>
    public List<GameObject> ComputeSlice(Vector3 sliceNormalWorld, Vector3 sliceOriginWorld)
    {
        var mesh = GetComponent<MeshFilter>().sharedMesh;
        if (!mesh) return null;
        
        // If the fragment root object has not yet been created, create it now
        if (!fragmentRoot)
        {
            // Create a game object to contain the fragments
            fragmentRoot = new($"{name}Slices");
            fragmentRoot.transform.SetParent(transform.parent);

            // Each fragment will handle its own scale
            fragmentRoot.transform.SetPositionAndRotation(transform.position, transform.rotation);
            fragmentRoot.transform.localScale = Vector3.one;
        }

        var sliceTemplate = CreateSliceTemplate();
        var sliceNormalLocal = transform.InverseTransformDirection(sliceNormalWorld);
        var sliceOriginLocal = transform.InverseTransformPoint(sliceOriginWorld);

        var fragments = Fragmenter.Slice(gameObject,
                         sliceNormalLocal,
                         sliceOriginLocal,
                         sliceOptions,
                         sliceTemplate,
                         fragmentRoot.transform);

        // Done with template, destroy it
        Destroy(sliceTemplate);

        // Deactivate the original object
        gameObject.SetActive(false);

        // Fire the completion callback
        callbackOptions.onCompleted?.Invoke();

        return fragments;
    }

    /// <summary>
    /// Creates a template object which each fragment will derive from
    /// </summary>
    /// <returns></returns>
    private GameObject CreateSliceTemplate()
    {
        // If pre-fracturing, make the fragments children of this object so they can easily be unfrozen later.
        // Otherwise, parent to this object's parent
        GameObject obj = new GameObject();
        obj.name = "Slice";
        obj.tag = tag;

        // Update mesh to the new sliced mesh
        obj.AddComponent<MeshFilter>();

        // Add materials. Normal material goes in slot 1, cut material in slot 2
        var meshRenderer = obj.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterials = new Material[2] {
            GetComponent<MeshRenderer>().sharedMaterial,
            sliceOptions.insideMaterial
        };

        // Copy collider properties to fragment
        var thisCollider = GetComponent<Collider>();
        var fragmentCollider = obj.AddComponent<MeshCollider>();
        fragmentCollider.convex = true;
        fragmentCollider.sharedMaterial = thisCollider.sharedMaterial;
        fragmentCollider.isTrigger = thisCollider.isTrigger;
        
        // Copy rigid body properties to fragment
        var thisRigidBody = GetComponent<Rigidbody>();
        var fragmentRigidBody = obj.AddComponent<Rigidbody>();
        fragmentRigidBody.linearVelocity = thisRigidBody.linearVelocity;
        fragmentRigidBody.angularVelocity = thisRigidBody.angularVelocity;
        fragmentRigidBody.linearDamping = thisRigidBody.linearDamping;
        fragmentRigidBody.angularDamping = thisRigidBody.angularDamping;
        fragmentRigidBody.useGravity = thisRigidBody.useGravity;
    
        // If refracturing is enabled, create a copy of this component and add it to the template fragment object
        if (sliceOptions.enableReslicing &&
           (currentSliceCount < sliceOptions.maxResliceCount))
        {
            CopySliceComponent(obj);
        }

        return obj;
    }
    
    /// <summary>
    /// Convenience method for copying this component to another component
    /// </summary>
    /// <param name="obj">The GameObject to copy this component to</param>
    private void CopySliceComponent(GameObject obj)
    {
        var sliceComponent = obj.AddComponent<Slice>();

        sliceComponent.sliceOptions = sliceOptions;
        sliceComponent.callbackOptions = callbackOptions;
        sliceComponent.currentSliceCount = currentSliceCount + 1;
        sliceComponent.fragmentRoot = fragmentRoot;
    }
}