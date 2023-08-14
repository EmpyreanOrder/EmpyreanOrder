using MalbersAnimations.Controller;
using MalbersAnimations.Scriptables;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MalbersAnimations
{
    [AddComponentMenu("Malbers/Animal Controller/Mode Align")]
    public class MModeAlign : MonoBehaviour
    {
        [RequiredField] public MAnimal animal;

        [ContextMenuItem("Attack Mode", "AddAttackMode")]
        public List<ModeID> modes = new List<ModeID>();

        [Tooltip("It will search any gameobject that is a Animals on the Radius. ")]
        [FormerlySerializedAs("AnimalsOnly")]
        public bool Animals = true;
        public LayerReference Layer = new LayerReference(0);
        [Tooltip("Radius used for the Search")]
        [Min(0)] public float SearchRadius = 2f;
        [Tooltip("Radius used push closer/farther the Target when playing the Mode"), UnityEngine.Serialization.FormerlySerializedAs("DistanceRadius")]
        [Min(0)] public float Distance = 0;
        [Tooltip("Multiplier To apply to AITarget found. Set this to Zero to ignore IAI Targets")]
        [Min(0)] public float TargetMultiplier = 1;
        [Tooltip("Time needed to complete the Position aligment")]
        [Min(0)] public float AlignTime = 0.3f;
        [Tooltip("Time needed to complete the Rotation aligment")]
        [Min(0)] public float LookAtTime = 0.15f;
        //[Tooltip("Push the Found Animal/Target instead of this character")]
        //public bool pushTarget = true;
        public Color debugColor = new(1, 0.5f, 0, 0.2f);

        public bool debug;

        void Awake()
        {
            if (animal == null)
                animal = this.FindComponent<MAnimal>();

            if (modes == null || modes.Count == 0)
            {
                Debug.LogWarning("Please Add Modes to the Mode Align. ", this);
                enabled = false;
            }
        }

        void OnEnable()
        { animal.OnModeStart.AddListener(StartingMode); }

        void OnDisable()
        { animal.OnModeStart.RemoveListener(StartingMode); }

        void StartingMode(int ModeID, int ability)
        {
            if (!isActiveAndEnabled) return;

            if (modes == null || modes.Count == 0 || modes.Exists(x => x.ID == ModeID))
            {

                if (!AlignAnimalsOnly()) //Search first Animals ... if did not find anyone then search for colliders
                    AlignCollider();
            }
        }

        private bool AlignAnimalsOnly()
        {
            MAnimal ClosestAnimal = null;
            float ClosestDistance = float.MaxValue;

            foreach (var a in MAnimal.Animals)
            {
                if (a == animal ||                                              //We are the same animal
                    a.ActiveStateID.ID == StateEnum.Death                       //The Animal is death
                    || a.Sleep                                                  //The Animal is sleep (Not working)
                    || !a.enabled                                               //The Animal Component is disabled    
                    || !MTools.Layer_in_LayerMask(a.gameObject.layer, Layer)    //Is not using the correct layer
                    ||! a.gameObject.activeInHierarchy 
                    )
                    continue; //Don't Find yourself or don't find death animals

                if (animal.transform.SameHierarchy(a.transform)) continue; //Meaning that the animal is mounting the other animal.


                var animalsDistance = Vector3.Distance(transform.position, a.Center);

                if (SearchRadius >= animalsDistance && ClosestDistance >= animalsDistance)
                {
                    ClosestDistance = animalsDistance;
                    ClosestAnimal = a;
                }
            }

            if (ClosestAnimal)
            {
                var ClosestAI = ClosestAnimal.FindInterface<IAITarget>();

                if (TargetMultiplier == 0) ClosestAI = null;
                StartAligning(ClosestAnimal.Center, ClosestAI);
                if (debug) Debug.Log($"[{name}], Alinging to [{ClosestAnimal.name}]", this);

                return true;
            }
            return false;
        }

        private void AlignCollider()
        {
            var pos = animal.Center;

            var AllColliders = Physics.OverlapSphere(pos, SearchRadius, Layer.Value, QueryTriggerInteraction.Ignore);

            Collider ClosestCollider = null;
          
            float ClosestDistance = float.MaxValue;

            foreach (var col in AllColliders)
            {
                if (col.transform.SameHierarchy(animal.transform)) continue; //Don't Find yourself
                if (!col.gameObject.activeInHierarchy) continue;  //Don't Find invisible colliders
                if (col.gameObject.isStatic) continue; //Don't Find Static colliders
                if (!col.enabled) continue; //Don't disable colliders

                var DistCol = (transform.position - col.transform.position).sqrMagnitude;

                if (ClosestDistance > DistCol)
                {
                    ClosestDistance = DistCol;
                    ClosestCollider = col;
                }
            }
            if (ClosestCollider)
            {
                var ClosestAI = ClosestCollider.FindInterface<IAITarget>();
                if (TargetMultiplier == 0) ClosestAI = null;
                StartAligning(ClosestCollider.transform.position, ClosestAI);

                if (debug) Debug.Log($"[{name}], Alinging to [{ClosestCollider.transform.name}]", this);
            }
        }

        private void StartAligning(Vector3 TargetCenter, IAITarget isAI)
        {
            TargetCenter.y = animal.transform.position.y;
            if (debug) Debug.DrawLine(transform.position, TargetCenter, Color.red, 3f);
            StartCoroutine(MTools.AlignLookAtTransform(animal.transform, TargetCenter, LookAtTime));

            var Dis = Distance * animal.ScaleFactor;
          
            if (isAI != null)
            {
                Dis = isAI.StopDistance() * TargetMultiplier;
                TargetCenter = isAI.GetPosition();
            }


            //Align Look At the Zone
            if (Dis > 0)
            {
                StartCoroutine(MTools.AlignTransformRadius(animal.transform, TargetCenter, AlignTime, Dis * animal.ScaleFactor));
            }
        }



#if UNITY_EDITOR

        [ContextMenu("Add Attack Mode")]
        private void AddAttackMode()
        {
            Reset();
        }


        void Reset()
        {
            ModeID modeID = MTools.GetResource<ModeID>("Attack1");
            animal = gameObject.FindComponent<MAnimal>();
            modes = new List<ModeID>();
            modes.Add(modeID);
            MTools.SetDirty(this);
        }


        void OnDrawGizmosSelected()
        {
            if (animal && debug)
            {
                UnityEditor.Handles.color = debugColor;
                UnityEditor.Handles.DrawSolidDisc(transform.position, Vector3.up, SearchRadius);
                var c = debugColor; c.a = 1;
                UnityEditor.Handles.color = c;
                UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, SearchRadius);

                UnityEditor.Handles.color = (c + Color.white) / 2;
                UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, Distance);

            }
        }
#endif
    }

#if UNITY_EDITOR

    [CustomEditor(typeof(MModeAlign)), CanEditMultipleObjects]
    public class MModeAlignEditor : Editor
    {
        SerializedProperty animal, modes, AnimalsOnly, Layer, debug, LookRadius, DistanceRadius, AlignTime, LookAtTime, TargetMultiplier, debugColor/*, pushTarget*/;
        private void OnEnable()
        {
            animal = serializedObject.FindProperty("animal");
            modes = serializedObject.FindProperty("modes");
            AnimalsOnly = serializedObject.FindProperty("Animals");
            Layer = serializedObject.FindProperty("Layer");
            LookRadius = serializedObject.FindProperty("SearchRadius");
            DistanceRadius = serializedObject.FindProperty("Distance");
            AlignTime = serializedObject.FindProperty("AlignTime");
            LookAtTime = serializedObject.FindProperty("LookAtTime");
            debugColor = serializedObject.FindProperty("debugColor");
            TargetMultiplier = serializedObject.FindProperty("TargetMultiplier");
            debug = serializedObject.FindProperty("debug");
            
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            MalbersEditor.DrawDescription($"Execute a LookAt towards the closest Animal or GameObject  when is playing a Mode on the list");

            using (new GUILayout.VerticalScope(EditorStyles.helpBox))

            {
                using (new GUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(animal);
                    if (debug.boolValue)
                    EditorGUILayout.PropertyField(debugColor, GUIContent.none, GUILayout.Width(36));

                    MalbersEditor.DrawDebugIcon(debug);
                }


                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(modes, true);
                EditorGUI.indentLevel--;
                EditorGUILayout.PropertyField(AnimalsOnly);
                EditorGUILayout.PropertyField(Layer);
            }


            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(LookRadius);
                if (LookRadius.floatValue > 0)
                    EditorGUILayout.PropertyField(LookAtTime);
            }

            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(DistanceRadius);

                if (DistanceRadius.floatValue > 0)
                    EditorGUILayout.PropertyField(AlignTime);

                EditorGUILayout.PropertyField(TargetMultiplier);

            }

            serializedObject.ApplyModifiedProperties();
        }
    }
#endif

}