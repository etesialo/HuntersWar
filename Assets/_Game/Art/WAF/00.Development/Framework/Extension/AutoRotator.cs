using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AutoRotator : MonoBehaviour
{
    public bool forceForward;
   // public float forwardSpeed = 10f;
    [SerializeField] private float speed_x = 0.0f;
    [SerializeField] private float speed_y = 0.0f;
    [SerializeField] private float speed_z = 0.0f;

    Vector3 initLocalPosition;
    Quaternion initLocalRotation;

    Vector3? prePosition;
    private int initFrameCount;
    void Awake()
    {
        initLocalPosition = transform.localPosition;
        initLocalRotation = transform.localRotation;
    }

    //bool fixedUpdate = false;
    //void FixedUpdate()
    //{
    //    fixedUpdate = true;
    //}

    // Update is called once per frame
    void Update()
    {
        if (forceForward)
        {
            if(initFrameCount < Time.frameCount)
            {

                if (prePosition == null)
                    prePosition = transform.position;

                //if (!fixedUpdate)
                //{
                //    prePosition = transform.position;
                //    return;
                //}

                Vector3 targetDir = (transform.position - prePosition.Value);
                if (targetDir.sqrMagnitude > Mathf.Epsilon)
                {
                    Vector3 newDir = Vector3.RotateTowards(
                                        transform.forward, targetDir,
                                        Mathf.PI, 0);
                    transform.rotation = Quaternion.LookRotation(newDir);
                    prePosition = transform.position;
                }
            }
        }
        else
            transform.Rotate(Time.deltaTime * speed_x, Time.deltaTime * speed_y, Time.deltaTime * speed_z);

        //fixedUpdate = false;
    }

    public void InitForceForward()
    {
        prePosition = transform.position;
        initFrameCount = Time.frameCount;
    }

    void OnDisable()
    {
        transform.localPosition = initLocalPosition;
        transform.localRotation = initLocalRotation;
        prePosition = null;
    }

    public void SetSpeed(float x, float y, float z)
    {
        speed_x = x;
        speed_y = y;
        speed_z = z;
    }
}
