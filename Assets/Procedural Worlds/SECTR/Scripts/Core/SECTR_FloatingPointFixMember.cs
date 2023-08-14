using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SECTR_FloatingPointFixMember : MonoBehaviour
{

    protected void OnEnable()
    {
        SECTR_FloatingPointFix.Instance.AddMember(this);
    }

    protected void OnDestroy()
    {
        if (SECTR_FloatingPointFix.IsActive)
        {
            SECTR_FloatingPointFix.Instance.RemoveMember(this);
        }
    }


    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
