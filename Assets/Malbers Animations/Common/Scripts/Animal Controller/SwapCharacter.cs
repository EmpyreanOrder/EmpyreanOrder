using MalbersAnimations.Events;
using System.Collections.Generic;
using UnityEngine;

namespace MalbersAnimations.Controller
{

    public class SwapCharacter : MonoBehaviour
    {
        public List<MAnimal> Characters = new List<MAnimal>();

        [Tooltip("Force Fall State if the animal is not grounded")]
        public StateID Fall;
        public GameObjectEvent OnSwap = new GameObjectEvent();
        private MAnimal currentChar;
        private int currentCharIndex;


        private void Start()
        {
            if (Characters.Count > 1)
            {
                //Check first if they are Prefabs. If it is then Instantiate it on the scene
                for (int i = 1; i < Characters.Count; i++)
                {
                    if (Characters[i].gameObject.IsPrefab())
                        Characters[i] = Instantiate(Characters[i]);
                }



                    Characters[0].gameObject.SetActive(true); //Enable the First One!
                currentChar = Characters[0];

                for (int i = 1; i < Characters.Count; i++)
                {
                    Characters[i].gameObject.SetActive(false); //all the other
                }
            }
        }


        public void Swap(int Index)
        {
            var NextIndex = Index % Characters.Count;

            var NewCharacter = Characters[NextIndex];


            if (currentChar != NewCharacter)
            {
                Swap(currentChar, NewCharacter);
                currentChar = NewCharacter;
                currentCharIndex = NextIndex;
                OnSwap.Invoke(NewCharacter.gameObject);
            }
        }

        public void Swap()
        {
            Swap(currentCharIndex + 1);
        }


        public void Swap(MAnimal Old, MAnimal New)
        {
            var OldState = Old.ActiveStateID;


            if (Old.Grounded)
                New.OverrideStartState = OldState;
            else
            {
                New.OverrideStartState = Fall;
            }

            New.gameObject.SetActive(true);

            New.TeleportRot(Old.transform);

            //   Debug.Log($" MoveSmoth: {New.MovementAxisRaw}");

            New.Move_Direction = Old.Move_Direction;
            New.MovementAxisRaw = Old.MovementAxisRaw;
            New.MovementAxis = Old.MovementAxis;
            New.DeltaPos = Old.DeltaPos;
            New.DeltaRootMotion = Old.DeltaRootMotion;
            New.InertiaPositionSpeed = Old.HorizontalVelocity * New.DeltaTime;
            New.t.position = Old.t.position;

            New.SetMaxMovementSpeed();
            New.TargetSpeed = Old.TargetSpeed;
            New.Gravity = Old.Gravity;
            New.GravityTime = Old.GravityTime;
            New.HorizontalSpeed = Old.HorizontalSpeed;
            New.HorizontalVelocity = Old.HorizontalVelocity;
            // Debug.Log($" MoveSmoth After: {New.MovementAxisRaw}");

            Old.gameObject.SetActive(false);
        }


        private void Reset()
        {
            Fall = MTools.GetInstance<StateID>("Fall");
        }
    }
}
