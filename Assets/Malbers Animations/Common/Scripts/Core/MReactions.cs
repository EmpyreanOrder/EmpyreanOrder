
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif


namespace MalbersAnimations.Reactions
{
    [AddComponentMenu("Malbers/Animal Controller/Reactions")]
    public class MReactions : MonoBehaviour
    {
        [Tooltip("Try to find a target on Enable. (Search first in the hierarchy then in the parents)")]
        public bool FindTarget = false;

        [SerializeField] private Component Target;

        [SerializeReference, SubclassSelector]
        public Reaction reaction;

        private void OnEnable()
        {
            if (FindTarget)
                Target = GetComponent(reaction.ReactionType) ?? GetComponentInParent(reaction.ReactionType);
        }

        [ContextMenu("Find Target")]
        private void GetTarget()
        {
            Target = GetComponent(reaction.ReactionType) ?? GetComponentInParent(reaction.ReactionType);
            MTools.SetDirty(this);
        }

        public void React()
        {
            if (reaction != null)
            {
                reaction.React(Target);
            }
            else
            {
                Debug.LogError("Reaction is Empty. Please use any reaction", this);
            }
        }

        public void React(Component newAnimal)
        {

            if (reaction != null)
            {
                Target = reaction.VerifyComponent(newAnimal);
                reaction.TryReact(Target);
            }
            else
            {
                Debug.LogError("Reaction is Empty. Please use any reaction", this);
            }
        }

        public void React(GameObject newAnimal) => React(newAnimal.transform);

    }

#if UNITY_EDITOR

    [CustomEditor(typeof(MReactions))]
    public class MReactionEditor : Editor
    {
        SerializedProperty FindTarget, Target, reaction;

        private GUIContent _SearchIcon;

        private void OnEnable()
        {
            FindTarget = serializedObject.FindProperty("FindTarget");
            Target = serializedObject.FindProperty("Target");
            reaction = serializedObject.FindProperty("reaction"); 
        }
 

        public override void OnInspectorGUI()
        {
            serializedObject.Update();


            using (new GUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(Target);

                if (_SearchIcon == null)
                {
                    _SearchIcon = EditorGUIUtility.IconContent("Search Icon");
                    _SearchIcon.tooltip = "Find Target On Enable";
                }

                FindTarget.boolValue =  GUILayout.Toggle(FindTarget.boolValue, _SearchIcon, EditorStyles.miniButton, GUILayout.Width(28),GUILayout.Height(20));
            }

            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(reaction);
                EditorGUI.indentLevel--;
            }
            serializedObject.ApplyModifiedProperties();
        }


    }
#endif
}

