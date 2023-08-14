using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kommt auf ein Gameobject auf dem ein Animator oder ein Animation Component sammt clips sind !!!
/// </summary>
public class EZ_AnimCycler : MonoBehaviour
{
    public KeyCode playKey = KeyCode.P;
    public KeyCode cycleThroughKey = KeyCode.C;
    public float speed = 1;
    float lastSpeed = 1;
    public float speedModif = 0.5f;
    public float maxSpeed = 15f;
    public KeyCode speedUPKey = KeyCode.UpArrow;
    public KeyCode speedDownKey = KeyCode.DownArrow;
    public KeyCode speedReset = KeyCode.Alpha0;
    /*public */
    List<AnimationClip> anims = new List<AnimationClip>();

    AnimationClip curClip;
    int curIndex = 0;
    Animator a;
    Animation legacyA;

    void Awake()
    {
        a = GetComponent<Animator>();
        legacyA = GetComponent<Animation>();
        if(a != null) a.applyRootMotion = false;
    }

    void Start()
    {
        if (a != null)
        {
            anims = new List<AnimationClip>(a.runtimeAnimatorController.animationClips);
            curClip = anims[0] != null ? anims[0] : null;
        }
        else if (legacyA != null)
        {
            anims = new List<AnimationClip>();
            foreach (AnimationState state in legacyA)
            {
                anims.Add(state.clip);
            }
            curClip = anims[0] != null ? anims[0] : null;
            legacyA.clip = curClip;
        }
        else Debug.LogError("ERROR !!! ---  kein animator oder legacy animation component auf dem gameobject !!!!");
    }

    void Update()
    {
        if (a != null)
        {
            if (Input.GetKeyDown(playKey)) // Play/Stop  --------------------------------------------------------------------------
            {
                if (curClip != null)
                {
                    // Play
                    var st_ = a.GetCurrentAnimatorStateInfo(0);
                    if (!st_.IsName(curClip.name) || speed == 0)
                    {
                        speed = lastSpeed;
                        a.speed = speed;
                        a.Play(curClip.name, 0);
                    }
                    else // Stop
                    {
                        speed = 0;
                        a.speed = speed;
                    }
                }
            }
            if (Input.GetKeyDown(cycleThroughKey)) // Cycle Through --------------------------------------------------------------------------
            {
                if (anims.Count > 0)
                {
                    if (curIndex + 1 < anims.Count)
                    {
                        curIndex++;
                        curClip = anims[curIndex];
                    }
                    else
                    {
                        curIndex = 0;
                        curClip = anims[curIndex];
                    }
                }
                else
                    Debug.LogError("! Error ! ---- Anims Liste hat keine Clips !! ", gameObject);
            }
            if (Input.GetKey(speedUPKey)) // Speed UP --------------------------------------------------------------------------
            {
                speed = Mathf.Clamp(speed + Time.deltaTime, 0, maxSpeed);
                a.speed = speed;
                lastSpeed = speed;
            }
            else if (Input.GetKey(speedDownKey)) // Speed Down  --------------------------------------------------------------------------
            {
                speed = Mathf.Clamp(speed - Time.deltaTime, 0, maxSpeed);
                a.speed = speed;
                lastSpeed = speed;
            }
            else if (Input.GetKeyDown(speedReset)) // Speed Reset --------------------------------------------------------------------------
            {
                speed = 1;
                a.speed = speed;
                lastSpeed = speed;
            }
        }
        else if (legacyA != null)
        {
            if (Input.GetKeyDown(playKey)) // Play/Stop  --------------------------------------------------------------------------
            {
                if (curClip != null)
                {
                    // Play
                    if (!legacyA.IsPlaying(curClip.name))
                    {
                        legacyA.Play(curClip.name);
                    }
                    else
                        legacyA.Stop();
                }
            }
            if (Input.GetKeyDown(cycleThroughKey)) // Cycle Through --------------------------------------------------------------------------
            {
                if (anims.Count > 0)
                {
                    if (curIndex + 1 < anims.Count)
                    {
                        curIndex++;
                        curClip = anims[curIndex];
                        legacyA.clip = curClip;
                    }
                    else
                    {
                        curIndex = 0;
                        curClip = anims[curIndex];
                        legacyA.clip = curClip;
                    }
                }
                else
                    Debug.LogError("! Error ! ---- Anims Liste hat keine Clips !! ", gameObject);
            }
            if (Input.GetKey(speedUPKey)) // Speed UP --------------------------------------------------------------------------
            {
                
                foreach (AnimationState state in legacyA)
                {
                    if (curClip.name == state.clip.name)
                    {
                        state.speed = Mathf.Clamp(state.speed + Time.deltaTime * speedModif, 0, maxSpeed);
                        break;
                    }
                }
            }
            else if (Input.GetKey(speedDownKey)) // Speed Down  --------------------------------------------------------------------------
            {
                foreach (AnimationState state in legacyA)
                {
                    if (curClip.name == state.clip.name)
                    {
                        state.speed = Mathf.Clamp(state.speed - Time.deltaTime * speedModif, 0, maxSpeed);
                        break;
                    }
                }
            }
            else if (Input.GetKeyDown(speedReset)) // Speed Reset --------------------------------------------------------------------------
            {
                foreach (AnimationState state in legacyA)
                {
                    state.speed = 1;
                }
            }
        }
    }
}
