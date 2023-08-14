// Copyright (c) 2014 Make Code Now! LLC

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

/// \ingroup Audio
/// Propagation Source simulates the complex phenomena of audio reflections in a closed space.
///
/// In the real world, the sounds we hear are very often reflections of the actual space. As such,
/// the sound appears to be located not in a straight line to the source, but to be emanating from the
/// nearest opening that leads to the source. The Sector/Portal graph provides the perfect context
/// for efficiently but accurately determining how sounds move through an environment.
/// 
/// Propagation Sources works by attemtping to find the shortest path between itself and the
/// active AudioListener. Because the Sector/Portal graph is fairly coarse, this path plan is
/// relatively inexpensive, but it is not free. Because of this additional cost, Propagation
/// Sources should be used sparingly, where they provide the most audio bang for your CPU buck.
[ExecuteInEditMode]
[RequireComponent(typeof(SECTR_Member))]
[AddComponentMenu("Procedural Worlds/SECTR/Audio/SECTR Propagation Source")]
public class SECTR_PropagationSource : SECTR_AudioSource 
{
	#region Private Details
	private class PathSound
	{
		public SECTR_AudioCueInstance instance;
		public SECTR_Portal firstPortal;
		public SECTR_Portal secondPortal;
		public float firstDistance;
		public float secondDistance;
		public float distance;
		public Vector3 position;
		public Vector3 lastListenerPosition;
		public float weight = 1;
		public bool occluded = false;
	}
	
	private SECTR_Member cachedMember = null;
	private List<SECTR_Graph.Node> path = new List<SECTR_Graph.Node>(32);
	private List<PathSound> activeSounds = new List<PathSound>(4);
	private float directDistanceToListener = 0;
	private bool playing = false;
	private bool played = false;
	#endregion
	
	#region Public Interface
	[SECTR_ToolTip("When the listener gets within this distance of a portal, the sound direction will start to blend towards the next portal or source position.", 0, -1)]
	public float InterpDistance = 2f;
	
	/// Returns true if the Source is currently playing a sound.
	public override bool IsPlaying { get { return playing || activeSounds.Count > 0; } }
	
	/// Make some noise! Plays the Cue. 
	public override void Play()
	{
		playing = true;
		played = false;
	}
	
	/// Stops the Source from playing.
	/// <param name="stopImmediately">Overrides any fade-out specified in the Cue</param>
	public override void Stop(bool stopImmediately)
	{
		int numActiveSounds = activeSounds.Count;
		for(int soundIndex = 0; soundIndex < numActiveSounds; ++soundIndex)
		{
			PathSound sound = activeSounds[soundIndex];
			if(sound != null)
			{
				sound.instance.Stop(stopImmediately);
			}
		}
		activeSounds.Clear();
		playing = false;
		played = false;
	}
	#endregion
	
	#region Unity Interface
	void OnEnable()
	{
		
		cachedMember = GetComponent<SECTR_Member>();
		
		#if UNITY_EDITOR
		EditorApplication.update += Update;
		#endif
	}
	
	protected override void OnDisable()
	{
		base.OnDisable();
		
		cachedMember = null;
		
		#if UNITY_EDITOR
		EditorApplication.update -= Update;
		#endif
	}
	
	void Update()
	{
		if(playing && Cue != null && cachedMember.Sectors.Count > 0 && SECTR_AudioSystem.Initialized)
		{
			Transform listener = SECTR_AudioSystem.Listener;
			Vector3 listenerPosition = listener.position;
			Vector3 sourcePosition = transform.position;
			directDistanceToListener = Vector3.Distance(sourcePosition, listenerPosition);
			bool occludable = Cue.SourceCue.Spatialization == SECTR_AudioCue.Spatializations.Occludable3D;
			int numActiveSounds = activeSounds.Count;
			
			if(played && !Loop && !Cue.SourceCue.Loops && numActiveSounds == 0)
			{
				Stop(false);
				return;
			}
			
			// Only perform full propagation logic on sounds within the range of the sound.
			if(directDistanceToListener <= Cue.SourceCue.MaxDistance)
			{
				// First, find the shortest path from the source position to the listener position
				PathSound currentSound = null;
				SECTR_Graph.FindShortestPath(ref path, listenerPosition, transform.position, 0);
				int pathSize = path.Count;
				if(pathSize > 0)
				{
					// Compute the current frame's propogated sound position based on the first and second
					// portals in the path.
					SECTR_Portal firstPortal = path[0].Portal;
					SECTR_Portal secondPortal = pathSize > 1 ? path[1].Portal : null;
					bool newSound = false;
					
					// Now we need to update the sound based on the current position, but we need to
					// determine if we should update an existing instance or create a new one.
					// We can re-use an instance if it is on the same path as the current path.
					for(int soundIndex = 0; soundIndex < numActiveSounds; ++soundIndex)
					{
						// "Same path" just means that the current and next portals are identical.
						PathSound activeSound = activeSounds[soundIndex];
						if(firstPortal == activeSound.firstPortal || firstPortal == activeSound.secondPortal || secondPortal == activeSound.firstPortal)
						{
							currentSound = activeSound;
							break;
						}
					}
					
					if(currentSound == null)
					{
						currentSound = new PathSound();
						newSound = true;
					}
					
					currentSound.firstPortal = firstPortal;
					currentSound.secondPortal = secondPortal;
					currentSound.occluded = false;
					currentSound.firstDistance = 0;
					currentSound.secondDistance = 0;
					currentSound.distance = 0;
					
					// Compute path distance and occlusion together to avoid excessive walks
					SECTR_AudioSystem.OcclusionModes occlusionFlags = occludable ? SECTR_AudioSystem.AudioSystem.OcclusionFlags : 0;
					bool graphOccludable = (occlusionFlags & SECTR_AudioSystem.OcclusionModes.Graph) != 0;
					
					if(pathSize == 1 && path[0].Portal == null)
					{
						currentSound.firstDistance = directDistanceToListener;
						currentSound.secondDistance = directDistanceToListener;
					}
					else
					{
						for(int pathIndex = 0; pathIndex < pathSize; ++pathIndex)
						{
							SECTR_Portal portal = path[pathIndex].Portal;
							SECTR_Portal nextPortal = pathIndex < pathSize - 1 ? path[pathIndex + 1].Portal : null;
							Vector3 portalPosition = portal.Center;
							
							if(pathIndex == 0)
							{
								currentSound.firstDistance += Vector3.Distance(portalPosition, listenerPosition);
							}
							else if(pathIndex == 1 && portal)
							{
								currentSound.secondDistance += Vector3.Distance(portalPosition, listenerPosition);
							}
							
							float portalDistance = 0f;
							if(portal && nextPortal)
							{
								portalDistance = Vector3.Distance(portalPosition, nextPortal.Center);
							}
							else
							{
								portalDistance = Vector3.Distance(portalPosition, sourcePosition);
							}
							
							currentSound.firstDistance += portalDistance;
							if(pathIndex >= 1)
							{
								currentSound.secondDistance += portalDistance;
							}
							
							if(portal && 
							   graphOccludable && !currentSound.occluded &&
							   (portal.Flags & SECTR_Portal.PortalFlags.Closed) != 0)
							{
								currentSound.occluded = true;
							}
						}
					}
					
					// Use the default occlusion routine for all other types of occlusion.
					occlusionFlags &= ~SECTR_AudioSystem.OcclusionModes.Graph;
					if(!currentSound.occluded && occlusionFlags != 0)
					{
						currentSound.occluded = SECTR_AudioSystem.IsOccluded(sourcePosition, occlusionFlags);
					}
					
					_ComputeSoundSpatialization(listenerPosition, directDistanceToListener, currentSound);
					
					// If we couldn't find a match with an existing instance, create a new one.
					if(!currentSound.instance)
					{
						if(activeSounds.Count > 0)
						{
							currentSound.instance = SECTR_AudioSystem.Clone(activeSounds[0].instance, currentSound.position);
						}
						else
						{
							currentSound.instance = SECTR_AudioSystem.Play(Cue, currentSound.position, Loop);
						}
						if(currentSound.instance)
						{
							currentSound.instance.ForceInfinite();
							if(newSound)
							{
								activeSounds.Add(currentSound);
								++numActiveSounds;
							}
						}
						else
						{
							if(!newSound)
							{
								activeSounds.Remove(currentSound);
								--numActiveSounds;
							}
							currentSound = null;
						}
					}
					else
					{
						currentSound.instance.Position = currentSound.position;
					}
					
					if(currentSound != null)
					{
						// mark as played for use in one shot limiting
						currentSound.lastListenerPosition = listenerPosition;
						played = true;
					}
				}
				
				// Update volume and other properties of all active sound instances.
				{
					// To ensure a clean cross fade as players move around, we need to
					// compute a weight for each sound.
					int soundIndex = 0;
					float residualWeight = 1;
					const float invFalloffDistance = (1f/2f);
					for(soundIndex = 0; soundIndex < numActiveSounds; ++soundIndex)
					{
						PathSound activeSound = activeSounds[soundIndex];
						if(activeSound != currentSound)
						{
							_ComputeSoundSpatialization(listenerPosition, directDistanceToListener, activeSound);
							activeSound.weight = activeSound.instance ? (1f - Mathf.Clamp01(Vector3.Distance(activeSound.lastListenerPosition, listenerPosition) * invFalloffDistance)) : 0f;
							residualWeight -= activeSound.weight;
						}
					}
					
					// If there is a current sound and there is any weight left over,
					// give the current sound (i.e. the newest sound) the residual weight.
					// This gives priority to pre-existing sounds so that they don't pop out.
					if(currentSound != null)
					{
						currentSound.weight = Mathf.Max(0, residualWeight);
					}
					
					// Now update all sounds in the group.
					soundIndex = 0;
					float minDistance = Cue.SourceCue.MinDistance;
					float maxDistance = Cue.SourceCue.MaxDistance;
					float invDistanceDelta = 1f / (maxDistance - minDistance);
					while(soundIndex < numActiveSounds)
					{
						// Sounds with active instances get their attenuation, pitch, and occlusion set.
						PathSound activeSound = activeSounds[soundIndex];
						if(activeSound.instance)
						{
							activeSound.instance.Position = activeSound.position;
							
							// Attenuation code is duplicated from SECTR_AudioSystem, but I don't want to pay extra
							// function cal overhead so...
							float attenuation = 1;
							switch(Cue.SourceCue.Falloff)
							{
							case SECTR_AudioCue.FalloffTypes.Linear:
								attenuation = 1 - Mathf.Clamp01((activeSound.distance - minDistance) * invDistanceDelta);
								break;
							case SECTR_AudioCue.FalloffTypes.Logarithmic:
								attenuation = Mathf.Clamp01(1 / Mathf.Max(activeSound.distance - minDistance - 1, 0.001f));
								break;
							}
							// Note that this actually sets the UserVolume, so HDR sounds are still mixed
							// internally to the AudioSystem.
							activeSound.instance.Volume = attenuation * activeSound.weight * volume;
							activeSound.instance.Pitch = pitch;
							
							// Set occlusion values that were computed above.
							if(occludable)
							{
								activeSound.instance.ForceOcclusion(activeSound.occluded);
							}
						}
						
						// Sounds with no weight left get removed from the list.
						// But not the current sound, we might need that.
						if(activeSound.weight <= 0f || !activeSound.instance)
						{
							// Hard stop because we should be inaudible at this point.
							activeSound.instance.Stop(true);
							activeSounds.RemoveAt(soundIndex);
							--numActiveSounds;
						}
						else
						{
							++soundIndex;
						}
					}
				}
			}
			else // if we're out of range, shut everything down.
			{
				for(int activeIndex = 0; activeIndex < numActiveSounds; ++activeIndex)
				{
					PathSound activeSound = activeSounds[activeIndex];
					if(activeSound != null)
					{
						activeSound.instance.Stop(false);
					}
				}
				activeSounds.Clear();
			}
		}
	}
	#endregion
	
	#region Audio Source Interface
	protected override void OnVolumePitchChanged()
	{
	}
	#endregion
	
	#region PrivateMembers
	private void _ComputeSoundSpatialization(Vector3 listenerPosition, float distanceToListener, PathSound pathSound)
	{
		if(pathSound.firstPortal != null)
		{
			Vector3 firstPortalPosition = pathSound.firstPortal.Center;
			
			Vector3 finalPosition;
			float finalDistance;
			float distanceToPortalSqr = pathSound.firstPortal.BoundingBox.SqrDistance(listenerPosition);
			if(distanceToPortalSqr >= InterpDistance * InterpDistance)
			{
				finalPosition = firstPortalPosition;
				finalDistance = pathSound.firstDistance;
			}
			else
			{
				float distanceToPortal = Mathf.Sqrt(distanceToPortalSqr);
				float alpha = Mathf.Clamp01(distanceToPortal / InterpDistance);
				Vector3 secondPortalPosition = pathSound.secondPortal ? pathSound.secondPortal.Center : transform.position;
				finalPosition = Vector3.Lerp(secondPortalPosition, firstPortalPosition, alpha);
				float secondPortalDistance = pathSound.secondPortal ? pathSound.secondDistance : distanceToListener;
				finalDistance = Mathf.Lerp(secondPortalDistance, pathSound.firstDistance, alpha); 
			}
			
			pathSound.position = finalPosition;
			pathSound.distance = finalDistance;
		}
		else
		{
			// We're either in the same room, or something has gone horribly wrong. Either way, this is the best fallback.
			pathSound.position = transform.position;
			pathSound.distance = distanceToListener;
		}
	}
	#endregion
}
