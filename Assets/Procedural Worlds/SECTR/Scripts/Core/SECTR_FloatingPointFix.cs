using UnityEngine;
using System.Collections.Generic;
using System;


/// <summary>
/// This component can be attached to the main camera / main character for a so called "floating point precision fix". Unity (& many other game engines)
/// suffer from the problem that graphic and physics calculation can become imprecise after a certain distance from the world origin. When your scene exceeds roughly 
/// 5000 units into any direction it can happen that you see symptoms like:
/// shadows flickering
/// shaky physics
/// issues in animations
/// The floating point fix component combats this by shifting all GameObjects back to the origin after a certain threshold is reached. While this is barely noticeable for the player,
/// it prevents such issues as mentioned above as the world never exceeds 5000 units because it is shifted back before things like these can happen.
/// Please note that this script requires a special setup for the sectors as well, please refer to the SECTR manual for more information.
/// </summary>
public class SECTR_FloatingPointFix : MonoBehaviour
{
    /// <summary>
    /// Singleton instance
    /// </summary>
    private static SECTR_FloatingPointFix instance = null;

    /// <summary>
    /// List of all members of the floating point fix system - these objects are the ones that will moved by the floating point fix
    /// </summary>
    private List<SECTR_FloatingPointFixMember> allMembers = new List<SECTR_FloatingPointFixMember>();

    /// <summary>
    /// List of all particle systems simulating in world space, those need special treatement when the fix performs the shifts
    /// </summary>
    private List<ParticleSystem> allWorldSpaceParticleSystems = new List<ParticleSystem>();

    /// <summary>
    /// Temporary array to keep track of particles while performing a shift.
    /// </summary>
    ParticleSystem.Particle[] currentParticles = null;



    /// <summary>
    /// Returns the current Floating Point Fix Instance in the scene
    /// </summary>
    public static SECTR_FloatingPointFix Instance { get {
            if (instance == null)
                instance = (SECTR_FloatingPointFix)FindObjectOfType(typeof(SECTR_FloatingPointFix));
            if (instance == null && Application.isPlaying)
                Debug.LogError("No Sectr Floating Point Fix Instance could be found, please add a SECTR Floating Point Fix component to the object that also contains the main sector loader.");
            return instance;
        } }
    /// <summary>
    /// Returns true if the Floating Point Fix is active in this scene
    /// </summary>
    public static bool IsActive { get
        {
            if (instance == null)
                instance = (SECTR_FloatingPointFix)FindObjectOfType(typeof(SECTR_FloatingPointFix));
            return instance != null;
        } }


    /// <summary>
    /// The distance from origin at which the floating point fix shift will be performed. Whenever the object that contains the floating point fix component
    /// moves further than this distance from 0,0,0 all floating point fix members and sectors will be shifted back to the origin. 
    /// </summary>
    public float threshold = 1000.0f;

    /// <summary>
    /// This vector3 represents the cumulative offset so far
    /// </summary>
    public Vector3 totalOffset = Vector3.zero;



    /// <summary>
    /// Makes sure we have a singleton and sets the sectors in the scene up for the floating point fix mode.
    /// </summary>
    void OnEnable()
    {
        //Singleton pattern
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }

        //set up all sectors for shifting
        foreach (SECTR_Sector sector in FindObjectsOfType<SECTR_Sector>())
        {
            sector.FloatingPointFix = true;
            sector.OverrideBounds = true;
            sector.BoundsUpdateMode = SECTR_Member.BoundsUpdateModes.Always;
        }
    }


    /// <summary>
    /// Adds a Floating Point Fix Member. Members are GameObjects that need to be shifted when the floating point fix shift occurs.
    /// Make sure to call "Remove Member" when your object gets destroyed.
    /// </summary>
    /// <param name="member">The member to add</param>
    public void AddMember(SECTR_FloatingPointFixMember member)
    {
        if (!allMembers.Contains(member))
            allMembers.Add(member);
    }

    /// <summary>
    /// Removes a Floating Point Fix Member from the tracking list. Members are GameObjects that need to be shifted when the floating point fix shift occurs.
    /// </summary>
    /// <param name="member">The member to remove</param>
    public void RemoveMember(SECTR_FloatingPointFixMember member)
    {
        if (allMembers.Contains(member))
        {
            allMembers.Remove(member);
        }
    }

    /// <summary>
    /// Adds a particle system simulated in World Space to the tracking list. All Particle Systems included in here will have their particles shifted as well when the floating point fix shift occurs.
    /// </summary>
    /// <param name="ps">The particle system to add</param>
    public void AddWorldSpaceParticleSystem(ParticleSystem ps)
    {
        if (!allWorldSpaceParticleSystems.Contains(ps))
            allWorldSpaceParticleSystems.Add(ps);
    }

    /// <summary>
    /// Removes a particle system simulated in World Space from the tracking list.
    /// </summary>
    /// <param name="ps">The particle system to remove</param>
    public void RemoveWorldSpaceParticleSystem(ParticleSystem ps)
    {
        if (allWorldSpaceParticleSystems.Contains(ps))
        {
            allWorldSpaceParticleSystems.Remove(ps);
        }
    }

    /// <summary>
    /// Converts a Vector3 to its "real" / original position as if the floating point fix would not exist.
    /// </summary>
    /// <param name="currentPosition">The </param>
    /// <returns></returns>
    public Vector3 ConvertToOriginalSpace(Vector3 position)
    {
        return position += totalOffset;
    }


void LateUpdate()
    {
        Vector3 currentPosition = gameObject.transform.position;
        currentPosition.y = 0;

        if (currentPosition.magnitude > threshold)
        {
            totalOffset -= currentPosition;
            gameObject.transform.position -= currentPosition;
            foreach (SECTR_Sector sector in SECTR_Sector.All)
            {
                sector.BoundsOverride.center -= currentPosition;
                sector.ForceUpdate(true);
            }

            foreach (SECTR_FloatingPointFixMember member in allMembers)
            {
                member.transform.position -= currentPosition;
            }

            foreach (ParticleSystem ps in allWorldSpaceParticleSystems)
            {
                bool wasPaused = ps.isPaused;
                bool wasPlaying = ps.isPlaying;

                if (!wasPaused)
                    ps.Pause();

                if (currentParticles == null || currentParticles.Length < ps.main.maxParticles)
                {
                    currentParticles = new ParticleSystem.Particle[ps.main.maxParticles];
                }

                int num = ps.GetParticles(currentParticles);

                for (int i = 0; i < num; i++)
                {
                    currentParticles[i].position -= currentPosition;
                }

                ps.SetParticles(currentParticles, num);

                if (wasPlaying)
                    ps.Play();
            }
        }
    }

}


