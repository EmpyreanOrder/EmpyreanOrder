using System;
using System.Collections.Generic;
using UnityEngine;

namespace MalbersAnimations.Reactions
{
    [System.Serializable]

    [AddTypeMenu("* Multiple Reactions")]

    public class ListReaction : Reaction
    {
        public override Type ReactionType => typeof(Component);

        [SerializeReference, SubclassSelector]
        public List<Reaction> reactions = new();

        protected override bool _TryReact(Component component)
        {
            if (reactions != null)
            {
                var TryResult = true;

                foreach (var r in reactions)
                {
                    TryResult = TryResult && r.TryReact(component);
                }

                return TryResult;
            }
            return false;
        }
    }
}
