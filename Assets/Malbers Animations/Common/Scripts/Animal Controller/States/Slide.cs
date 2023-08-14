using MalbersAnimations.Scriptables;
using UnityEngine;

namespace MalbersAnimations.Controller
{
    public class Slide : State
    {
        public override string StateName => "Slide";


        [Tooltip("Lerp value for the Aligment to the surface")]
        public FloatReference OrientLerp = new(10f);


        //[Tooltip("Keep the Animal aligned with the terrain slope")]
        //public bool IgnoreRotation = true;
        public FloatReference RotationAngle = new(30f);
        public FloatReference SideMovement = new(5f);

        [Tooltip("Exit Speed when there's no Slope")]
        public FloatReference ExitSpeed = new(0.5f);


        [Header("Exit Status")]
        [Tooltip("The Exit Status will be set to 1 if the Exit Condition was the Exit Speed")]
        public IntReference ExitSpeedStatus = new(1);
        [Tooltip("The Exit Status will be set to 2 if the Exit Condition was that there's no longer a Ground Changer")]
        public IntReference NoChangerStatus = new(2);


      

        //Enter From Fall if the Slope is greater than Maximum
        //Check the Ground if Fall touched it with maximum slope
        //Align to the Ground

        //Exit on Normal Slope??
        //Do not exit if the Current Plataform is a Slider <Component>

        //Aceleration depending the slope and the Input (Speed Modifiers)
        //Orient to the Slide Direction

        public override bool TryActivate()
        {
            return TrySlideGround();
        }


        public override void OnPlataformChanged(Transform newPlatform)
        {
            if (CanBeActivated && TrySlideGround())
            {
                Activate();
            }
            else if (IsActiveState && !animal.InGroundChanger)
            {
                Debugging("[Allow Exit] No Ground Changer");
                SetExitStatus(NoChangerStatus);
                AllowExit();
            }
        }


        /// <summary>  The State moves always forward  </summary>
        public override bool KeepForwardMovement => true;

        public override void Activate()
        {
            base.Activate();
            IgnoreRotation = animal.GroundChanger.SlideData.IgnoreRotation;
        }

        private bool TrySlideGround()
        {
            if (animal.InGroundChanger
                && animal.GroundChanger.SlideData.Slide
                && animal.SlopeDirectionAngle > animal.GroundChanger.SlideData.MinAngle
                && animal.HorizontalSpeed > ExitSpeed
                //&& !animal.DeepSlope
                )
            {
                return true;
            }

            return false;
        }


        public override void InputAxisUpdate()
        {
            var move = animal.RawInputAxis;

            if (AlwaysForward) animal.RawInputAxis.z = 1;

            DeltaAngle = move.x;
            var NewInputDirection = Vector3.ProjectOnPlane( animal.SlopeDirection, animal.UpVector);

            if (animal.MainCamera)
            {
                //Normalize the Camera Forward Depending the Up Vector IMPORTANT!
                var Cam_Forward = Vector3.ProjectOnPlane(animal.MainCamera.forward, UpVector).normalized;
                var Cam_Right = Vector3.ProjectOnPlane(animal.MainCamera.right, UpVector).normalized;

                move = (animal.RawInputAxis.z * Cam_Forward) + (animal.RawInputAxis.x * Cam_Right);
                DeltaAngle = Vector3.Dot(animal.Right, move);
            }

            NewInputDirection = Quaternion.AngleAxis(RotationAngle * DeltaAngle, animal.Up) * NewInputDirection;
            animal.MoveFromDirection(NewInputDirection);  //Move using the slope Direction instead

           // MDebug.Draw_Arrow(transform.position, NewInputDirection, Color.green);

          

            moveSmooth = Vector3.Lerp(moveSmooth,Vector3.Project( move,animal.Right), animal.DeltaTime * CurrentSpeed.lerpPosition);


            MDebug.Draw_Arrow(transform.position, moveSmooth, Color.white);

        }

        Vector3 moveSmooth;

        float DeltaAngle;

        public override Vector3 Speed_Direction()
        {
            var NewInputDirection = animal.SlopeDirection;

            if (!IgnoreRotation)
            {
                NewInputDirection = Quaternion.AngleAxis(RotationAngle * DeltaAngle, animal.Up) * NewInputDirection;
            }

            return NewInputDirection;
        }

        private bool IgnoreRotation;

        public override void OnStateMove(float deltatime)
        {
            if (InCoreAnimation)
            {

                var Right = Vector3.Cross(animal.Up, animal.SlopeDirection);

                Right = Vector3.Project(animal.MovementAxisSmoothed, Right);

              if (animal.debugGizmos)  MDebug.Draw_Arrow(transform.position, Right, Color.red );

                

                animal.AdditivePosition += deltatime * SideMovement * moveSmooth;

                //Orient to the Ground
                animal.AlignRotation(animal.SlopeNormal, deltatime, OrientLerp);


                if (IgnoreRotation)
                {
                    animal.AlignRotation(animal.Forward, animal.SlopeDirection, deltatime, OrientLerp); //Make your own Aligment
                    animal.UseAdditiveRot = false; //Remove Rotations
                }

                if (!animal.Grounded)
                {
                    animal.UseGravity = true;
                }
            }
        }


        public override void TryExitState(float DeltaTime)
        {
            if (animal.HorizontalSpeed <= ExitSpeed)
            {
                Debugging("[Allow Exit] Speed is Slow");
                SetExitStatus(ExitSpeedStatus);
                AllowExit();
                return;
            }
            if (animal.GroundChanger == null || !animal.GroundChanger.SlideData.Slide)
            {
                Debugging("[Allow Exit] No Ground Changer");

                SetExitStatus(NoChangerStatus);
                AllowExit();
                return;
            }
            if (!animal.GroundChanger.SlideData.Slide)
            {
                Debugging("[Allow Exit] Ground Changer Slide Data is False");

                SetExitStatus(NoChangerStatus);
                AllowExit();
                return;
            }
        }


        private void Reset()
        {
            ID = MTools.GetInstance<StateID>("Slide");

            General = new AnimalModifier()
            {
                RootMotion = true,
                Grounded = true,
                Sprint = true,
                OrientToGround = false,
                CustomRotation = true,
                IgnoreLowerStates = true,
                AdditivePosition = true,
                AdditiveRotation = true,
                Gravity = false,
                modify = (modifier)(-1),
            };
        }
    }
}