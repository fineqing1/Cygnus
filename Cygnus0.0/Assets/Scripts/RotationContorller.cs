using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class RotationContorller : MonoBehaviour
{
    public bool isdragable;//后续用if判断
    bool isDown=false;
    public float rotateSpeed=10;
    Transform tempParent;//临时父对象
    Transform oldparent;//原始父对象

    /// <summary>松手后相对初始姿态的累计旋转（欧拉角，单位：度），以 oldparent 为坐标系；逆时针10度再顺时针10度记为(0,0,0)</summary>
    public Vector3 Currentangle;

    /// <summary>初始姿态（相对父物体），在 Awake 中记录，坐标系原点为 oldparent</summary>
    Quaternion initialLocalRotation;

    private void Awake()
    {
        tempParent = new GameObject("TempParent").transform;
        initialLocalRotation = transform.localRotation;
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
        if (isdragable)
        {
            if (Input.GetMouseButtonDown(0))
            {
                isDown = true;
                transform.parent = tempParent;
            }

            if (Input.GetMouseButtonUp(0))
            {
                isDown = false;
                transform.parent = oldparent;
                // 相对初始姿态的累计旋转（在 oldparent 坐标系下）
                Quaternion cumulativeDelta = Quaternion.Inverse(initialLocalRotation) * transform.localRotation;
                Currentangle = cumulativeDelta.eulerAngles;
                tempParent.rotation = Quaternion.identity;
            }

            if (isDown)
            {
                float mx = Input.GetAxis("Mouse X");
                float my = Input.GetAxis("Mouse Y");
                Quaternion qx = Quaternion.AngleAxis(-mx * rotateSpeed, Vector3.up);
                Quaternion qy = Quaternion.AngleAxis(my * rotateSpeed, Vector3.right);
                tempParent.rotation = tempParent.rotation * qx * qy;
            }
        }
    }
}
