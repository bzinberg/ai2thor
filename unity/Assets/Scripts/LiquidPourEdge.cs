﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif


 public class ReadOnlyAttribute : PropertyAttribute
 {
 
 }
 
 [CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
 public class ReadOnlyDrawer : PropertyDrawer
 {
     public override float GetPropertyHeight(SerializedProperty property,
                                             GUIContent label)
     {
         return EditorGUI.GetPropertyHeight(property, label, true);
     }
 
     public override void OnGUI(Rect position,
                                SerializedProperty property,
                                GUIContent label)
     {
         GUI.enabled = false;
         EditorGUI.PropertyField(position, property, label, true);
         GUI.enabled = true;
     }
 }
 
public class LiquidPourEdge : MonoBehaviour
{
    public float radius = 1.0f;
    public float radiusRaycastOffset = 0.03f;
    public float threshold = 1e-4f;
    public Mesh debugQuad = null;

    public bool renderDebugLevelPlane = false;

    public GameObject waterEmiter = null;

    public float liquidVolumeLiters = 0f; 

    [ReadOnly] public float emptyValue = 0.6f;

    [ReadOnly] public float fullValue = 0.4f;

    [ReadOnly] public float containerMaxVolumeLiters = 1f;

    [ReadOnly] public float shaderFill = 0.0f;

    [ReadOnly] public float normalizedCurrentFill = 0.0f;
    private GameObject activeFlow = null;

    private Wobble wobbleComponent = null;
    
    
    // Start is called before the first frame update
    void Start()
    {
        wobbleComponent = this.GetComponentInParent<Wobble>();

        var mr = this.transform.GetComponentInParent<MeshRenderer>();
        normalizedCurrentFill = liquidVolumeLiters / containerMaxVolumeLiters;
        if (normalizedCurrentFill > 0.0001f) {
            mr.enabled = true;
        }
        shaderFill = emptyValue - (emptyValue - fullValue) * (normalizedCurrentFill);
        mr.material.SetFloat("_FillAmount", shaderFill);
        
    }

    void SetFillAmount(float normalizedFill) {
        normalizedCurrentFill = normalizedFill;
        var mr = this.transform.GetComponentInParent<MeshRenderer>();
        shaderFill = emptyValue - (emptyValue - fullValue) * (normalizedCurrentFill);
        mr.material.SetFloat("_FillAmount", shaderFill);
    }

    void TransferLiquid(float normalizedDelta, LiquidPourEdge transferer) {
        // TODO: Calculate correctly how much this gets filled based on the transferer properties
        Debug.Log("Transfering liquid from " +  this.gameObject.name + " to  " + transferer.gameObject.name + " amount " + normalizedDelta);
        this.SetFillAmount(normalizedCurrentFill + normalizedDelta);
    }

     void TransferLiquidVolume(float liters, LiquidPourEdge transferer) {
        // TODO: Calculate correctly how much this gets filled based on the transferer properties

        transferer.liquidVolumeLiters -= liters;
        transferer.normalizedCurrentFill = transferer.liquidVolumeLiters / transferer.containerMaxVolumeLiters;

        this.liquidVolumeLiters += liters;
        Debug.Log("Transfering liquid volume from " +  this.gameObject.name + " to  " + transferer.gameObject.name + " amount " + liters + " total in new " + liquidVolumeLiters);

        this.normalizedCurrentFill = this.liquidVolumeLiters / this.containerMaxVolumeLiters;
        this.SetFillAmount(normalizedCurrentFill);
    }

     void LoseLiquidVolume(float liters) {
        // TODO: Calculate correctly how much this gets filled based on the transferer properties
        this.liquidVolumeLiters -= liters;
        this.normalizedCurrentFill = this.liquidVolumeLiters / this.containerMaxVolumeLiters;
    }

    // Update is called once per frame
    void Update()
    {

        // var mr = this.transform.GetComponentInParent<MeshRenderer>();
        // shaderFill = emptyValue - (emptyValue - fullValue) * (normalizedCurrentFill);
        // Debug.Log("fullValue " + fullValue + ", emptyValue ," + emptyValue + " normalizedCurrentFill " + normalizedCurrentFill);
        // mr.material.SetFloat("_FillAmount", shaderFill);

        // if (liquidVolumeLiters > 0) {
            var up = this.transform.parent.up;
            var edgeLowestWorld = getLowestEdgePointWorld(up);
            var waterLevelWorld = getWaterLevelPositionWorld();

            var edgeLiquidDifference = waterLevelWorld.y - edgeLowestWorld.y;
            if (edgeLiquidDifference > 0) {
                var containerRotationRadians = Mathf.Acos(Vector3.Dot(Vector3.up, up));
                Debug.Log("Release liquid edge diff: " + edgeLiquidDifference  + " water level:  " + waterLevelWorld.y + " cup edge lowest: "+ edgeLowestWorld.y + " container angle: " + (180.0f/Mathf.PI) * containerRotationRadians) ;
                
                ReleaseLiquid(edgeLiquidDifference, edgeLowestWorld, containerRotationRadians);
            }
            else if (activeFlow != null)
            {
                // Make flow smaller do not turn off
                activeFlow.transform.position = edgeLowestWorld;
                activeFlow.SetActive(false);
            }
        // }
    }

    protected void ReleaseLiquid(float edgeDifference, Vector3 edgePositionWorld, float containerRotationRadians) {
        //Debug.Log("Fluid out!!!! " + edgeDifference);
        if (activeFlow == null) {
            activeFlow = Object.Instantiate(
                this.waterEmiter,
                edgePositionWorld,
                Quaternion.identity,
                this.transform.parent
            );
        }
        else {
            activeFlow.SetActive(true);
            //activeFlow.transform.rotation = Quaternion.Inverse(this.transform.parent.rotation);
        }

        activeFlow.transform.rotation = Quaternion.identity;

        const float liquidTransferConstant = 1f;
        var normalizedFillDifference = liquidTransferConstant * edgeDifference / (emptyValue - fullValue);
        // var normalizedNew = (emptyValue - edgeDifference) / (emptyValue - fullValue);
       

        var edgeLowesPosXZ = edgePositionWorld;
        edgeLowesPosXZ.y = 0;
        var posXZ = this.transform.position;
        posXZ.y = 0;

        var lenXZ = (edgeLowesPosXZ - posXZ).magnitude;
        var angleDiff = Mathf.Atan2(edgeDifference, lenXZ);

        Debug.Log(" Angle Diff " + angleDiff * 180.0f / Mathf.PI + " lenxz: " + lenXZ + " normdiff " + normalizedFillDifference);

        var mr = this.transform.GetComponentInParent<MeshRenderer>();
        var currentFill = mr.material.GetFloat("_FillAmount");

        const float magicConstant = 1f;
        var newFill = currentFill + magicConstant * edgeDifference;

        var normalizedNew = (emptyValue - newFill) / (emptyValue - fullValue);

        normalizedCurrentFill = normalizedNew;

        // var newCurrentNormalizedFill = (emptyValue - currentFill) / (emptyValue - fullValue);

        var litersTransfer = normalizedFillDifference * containerMaxVolumeLiters;

        var newLiters = liquidVolumeLiters - litersTransfer;

        // var ratio = containerRotationRadians / (Mathf.PI / 2.0f);
       
        // litersTransfer = ratio * liquidVolumeLiters;
        //  Debug.Log("Rotation radians " + containerRotationRadians + " ratio " + ratio + " liters transfer " + litersTransfer);

        //var ratio = containerRotationRadians / (Mathf.PI / 2.0f);
        // litersTransfer = ratio * liquidVolumeLiters;
        // normalizedFillDifference = litersTransfer / containerMaxVolumeLiters;
        // newFill = currentFill + normalizedFillDifference * (emptyValue - fullValue);

        

        if (litersTransfer > liquidVolumeLiters) {
             Debug.Log("------- Liters transfer > than available liters " + litersTransfer);
            if (Mathf.Abs(containerRotationRadians) - 0.001f < (Mathf.PI / 2.0f)) {
                var ratio = containerRotationRadians / (Mathf.PI / 2.0f);
                litersTransfer = ratio * liquidVolumeLiters;
                Debug.Log("-------- Aproximate transfer of " + litersTransfer + " for angle " + containerRotationRadians * 180.0f / Mathf.PI);
                
            //  newFill = emptyValue + 1.0f;
            //  litersTransfer = liquidVolumeLiters;
            }
            else {
                newFill = emptyValue + 1.0f;
                litersTransfer = liquidVolumeLiters;
            }
            // edgeDifference = (liquidVolumeLiters / containerMaxVolumeLiters) * (emptyValue - fullValue);
        }

        // If there is too little liquid left
        if (liquidVolumeLiters < 0.001f) {
            newFill = emptyValue + 1.0f;
            litersTransfer = liquidVolumeLiters;
        }

        

        //LayerMask.GetMask("SimObjVisible"); 
        RaycastHit hit;

        var fromRay  = this.getLowestEdgePointWorld(this.getUpVector(), true);
        var raycastTrue = Physics.Raycast(fromRay, Vector3.down, out hit, 100, Physics.AllLayers & ~LayerMask.GetMask("SimObjInVisible"));

        Debug.DrawRay(fromRay, Vector3.down, Color.green, 2f);
        if (raycastTrue) {
             Debug.Log("Fluid transfer before to game object " + hit.collider.gameObject.name);
            var otherLiquidEdge = hit.collider.GetComponent<LiquidPourEdge>();
           
            if (otherLiquidEdge) {
                var mrOther = otherLiquidEdge.GetComponentInParent<MeshRenderer>();

                //otherLiquidEdge.TransferLiquid(normalizedFillDifference, this);

                otherLiquidEdge.TransferLiquidVolume(litersTransfer, this);

                Debug.Log("Transfered " + edgeDifference);
                
                // /otherFillAmmount - 7.5f * edgeDifference
                // otherLiquidEdge.TransferLiquid()
                if (mrOther) {
                    mrOther.enabled = true;
                    // var otherFillAmmount = mrOther.material.GetFloat("_FillAmount");
                    // var flowTransferConstant = 0.01f;
                    // mrOther.material.SetFloat("_FillAmount", otherFillAmmount - magicConstant * edgeDifference);
                    // Debug.Log("Fluid transfer");
                }
            }
            else {
                this.LoseLiquidVolume(litersTransfer);
            }
        }
        else {
            this.LoseLiquidVolume(litersTransfer);
            Debug.Log("Raycast fail");
        }

        mr.material.SetFloat("_FillAmount", newFill);
        
    }

    private IEnumerator DecrementWaterValue(float waitTime, MeshRenderer mr, float newFill)
    {
        while (true)
        {
            yield return new WaitForSeconds(waitTime);
            print("WaitAndPrint " + Time.time);
            mr.material.SetFloat("_FillAmount", newFill);
        }
    }

    private Vector3 getUpVector() {
        var up = this.transform.parent.up;
        // if (wobbleComponent == null) {
        //     wobbleComponent = this.transform.GetComponentInParent<Wobble>();
        // }

        
        // var wobbleRot = Quaternion.Euler(wobbleComponent.wobbleAmountX, 0, wobbleComponent.wobbleAmountZ);
        // return wobbleRot * up;

        return up;
    }

    protected Vector3 getLowestEdgePointWorld(Vector3 up, bool withOffset = false) {
        var upXZ = new Vector3(up.x, 0, up.z);

        var parentRot = this.transform.parent.rotation;
        parentRot.x = 0;
        parentRot.z = 0;

        // Quat
        
        // upXZ = Quaternion.AngleAxis(-this.transform.parent.eulerAngles.y, Vector3.up) * upXZ.normalized;
        upXZ = Quaternion.Inverse(parentRot).normalized * upXZ.normalized;
        // Debug.Log("up xz " + upXZ);
        // Debug.Log("Local Pos " + this.transform.localPosition);
        var calculatedRadius = withOffset ? this.radius + this.radiusRaycastOffset : this.radius;
        var circleLowestLocal = Vector3.zero + calculatedRadius * upXZ;
        // var circleLowestWorld = this.transform.TransformPoint(circleLowestLocal);
        var circleLowestWorld = this.transform.TransformPoint(circleLowestLocal);

        return circleLowestWorld;
    }

    protected Vector3 getWaterLevelPositionWorld() {
        var mr = this.transform.GetComponentInParent<MeshRenderer>();
        if (mr != null) {
            var fillAmount = mr.material.GetFloat("_FillAmount");

            Gizmos.color = new Color(1f, 1f, 0.0f, 0.7f);

            var pos = this.transform.parent.position;
            pos.y += 0.5f - fillAmount;
            return pos;
        }
        else {
            Debug.LogError("No mesh renderer, with liquid material to get fill value");
        }
        return Vector3.zero;
    }

    void OnDrawGizmos() {
       
        UnityEditor.Handles.color  = Color.red;

        var up =  getUpVector();
        
        UnityEditor.Handles.DrawWireDisc(this.transform.position, up, this.radius);

        // UnityEditor.Handles.color  = new Color(1.0f, 0.1f, 0.1f, 0.4f);

        UnityEditor.Handles.color  = Color.yellow;
        UnityEditor.Handles.DrawWireDisc(this.transform.position, up, this.radius + this.radiusRaycastOffset);

        var circleLowestWorld = getLowestEdgePointWorld(up);

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(circleLowestWorld, radius / 10.0f);


        var circleLowestWorldWithOffset = getLowestEdgePointWorld(up, true);

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(circleLowestWorldWithOffset, radius / 10.0f);

        Gizmos.color = new Color(1f, 1f, 0.0f, 0.7f);

        if (renderDebugLevelPlane) {
            var pos = getWaterLevelPositionWorld();

            var rot = Quaternion.identity;
            if (wobbleComponent == null) {
                wobbleComponent = this.transform.GetComponentInParent<Wobble>();
            }

            //rot = Quaternion.Euler(-wobbleComponent.wobbleAmountX * 360, 0, -wobbleComponent.wobbleAmountZ * 360); 
               // Debug.Log("Wobble " + wobbleComponent.wobbleAmountX + wobbleComponent.wobbleAmountZ )

            Gizmos.DrawMesh(this.debugQuad, pos, Quaternion.Euler(90, 0, 0) * rot, new Vector3(0.5f, 0.5f, 0.5f));
        }

        // Gizmos.color = new Color(1f, 0f, 0.0f, 0.5f);
        // var bounds = this.GetComponentInParent<MeshRenderer>().bounds;
        // Gizmos.DrawCube(this.transform.parent.position, bounds.size);


    }


#if UNITY_EDITOR

[UnityEditor.MenuItem("Thor/Set Liquid Component")]
	public static void SetLiquidComponent()
	{
        GameObject prefabRoot = Selection.activeGameObject;
        GameObject circularPourEdge = GameObject.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath("Assets/Prefabs/Systems/CircularLiquidPourEdge.prefab",typeof(GameObject)) as GameObject);
        circularPourEdge.transform.parent = prefabRoot.transform;

        var material = UnityEditor.AssetDatabase.LoadAssetAtPath("Assets/SpecialFX/DynamicMixLiquidVolume.mat",typeof(Material)) as Material;

        prefabRoot.GetComponent<MeshRenderer>().material = material;
        var wobble = prefabRoot.AddComponent<Wobble>();
        wobble.MaxWobble = 0.03f;
        wobble.Recovery = 1;
        wobble.WobbleSpeed = 1;
        wobble.wobbleAmountX = 0;
        wobble.wobbleAmountZ = 0;
        
        var liquidEdge = circularPourEdge.GetComponent<LiquidPourEdge>();
        var worldOrigin = prefabRoot.transform.position;

		Mesh mesh = liquidEdge.GetComponentInParent<MeshFilter>().sharedMesh;

        Vector3[] vertices = mesh.vertices;
        float minY = float.MaxValue;
        float maxY = float.MinValue;

        for (int i = 0; i < mesh.vertices.Length; i++) {
            minY = vertices[i].y < minY ? vertices[i].y : minY;
            maxY = vertices[i].y > maxY ? vertices[i].y : maxY;
        }
        
        var mr = liquidEdge.GetComponentInParent<MeshRenderer>();
        var offset = (maxY - minY) * 0.00001f;
        var volume = mesh_volume_calculator.VolumeOfMesh(mesh);

        liquidEdge.emptyValue = 0.5f - minY - offset;
        liquidEdge.fullValue = 0.5f - maxY + offset;

        var floatVolume = ((float) volume) * 1000.0f;
        liquidEdge.containerMaxVolumeLiters = floatVolume;

        mr.enabled = false;

        circularPourEdge.transform.localPosition = new Vector3(0, maxY, 0);

        Debug.Log("Constants, empty: " + liquidEdge.emptyValue + " full: " + liquidEdge.fullValue + "maxVolume liters: " + floatVolume + " minY: " + minY + " maxY: " + maxY + " maxY - minY: " + (maxY - minY));
	}

#endif

}


