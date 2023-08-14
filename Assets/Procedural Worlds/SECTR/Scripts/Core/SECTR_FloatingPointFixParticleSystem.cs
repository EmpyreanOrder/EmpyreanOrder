using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class SECTR_FloatingPointFixParticleSystem : SECTR_FloatingPointFixMember
{

    protected new void OnEnable()
    {
        ParticleSystem ps = GetComponent<ParticleSystem>();
        //Register as Particle system
        if (ps && ps.main.simulationSpace == ParticleSystemSimulationSpace.World)
            SECTR_FloatingPointFix.Instance.AddWorldSpaceParticleSystem(ps);
    }

    protected new void OnDestroy()
    {
        if (SECTR_FloatingPointFix.IsActive)
        {
            ParticleSystem ps = GetComponent<ParticleSystem>();
            if(ps)
                SECTR_FloatingPointFix.Instance.RemoveWorldSpaceParticleSystem(ps);
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
