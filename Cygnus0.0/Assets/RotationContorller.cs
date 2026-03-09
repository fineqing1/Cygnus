using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class RotationContorller : MonoBehaviour
{
    public bool isDragable;//后续用if判断
    bool isDown=false;
    public float rotateSpeed=10;
    Transform tempParent;//临时父对象
    Transform oldparent;//原始父对象

    //If()
   
    

    private void Awake()
    {
        tempParent = new GameObject("TempParent").transform;
    }
    // Start is called before the first frame update
    void Start()
    {
        oldparent = transform.parent;
        tempParent.position = oldparent == null ? transform.position:oldparent.position;
        tempParent.rotation=Quaternion.identity;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0)) 
            {
            isDown = true;
            transform.parent = tempParent;
            }
        if(Input.GetMouseButtonUp(0))
            {
            isDown = false;
            transform.parent = oldparent;
            tempParent.rotation=Quaternion.identity;
            }
        if (isDown)
            {
            float mx = Input.GetAxis("Mouse X");
            float my = Input.GetAxis("Mouse Y");
            Quaternion qx = Quaternion.AngleAxis(-mx*rotateSpeed, Vector3.up);
            Quaternion qy = Quaternion.AngleAxis(my*rotateSpeed, Vector3.right);
            tempParent.rotation = tempParent.rotation * qx * qy;
            }
    }
}
