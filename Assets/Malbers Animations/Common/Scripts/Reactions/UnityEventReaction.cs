using MalbersAnimations.Events;
using UnityEngine;
 
namespace MalbersAnimations.Reactions
{
    [System.Serializable]

    [AddTypeMenu("Unity Reactions/Unity Event")]

    public class UnityEventReaction : Reaction
    {
        public override System.Type ReactionType => typeof(Component);

        public ComponentEvent Invoke = new ComponentEvent();

        protected override bool _TryReact(Component component)
        {
            Invoke.Invoke(component);
            return true;
        }
    }
}
