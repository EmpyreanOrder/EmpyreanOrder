using Cinemachine;
using MalbersAnimations.Scriptables;
using System.Collections;
using UnityEngine;

namespace MalbersAnimations
{
    [AddComponentMenu("Malbers/Camera/Third Person Follow Target (Cinemachine)")]
    public class ThirdPersonFollowTarget : MonoBehaviour
    {

        [Tooltip("Cinemachine Brain Camera")]
        public CinemachineBrain Brain;

        [Tooltip("Update mode for the Aim Logic")]
        public UpdateType updateMode = UpdateType.FixedUpdate;


        [Tooltip("What object to follow")]
        public TransformReference Target;
      
        public Transform CamPivot;
 
        [Tooltip("Camera Input Values (Look X:Horizontal, Look Y: Vertical)")]
        public Vector2Reference look = new();


        [Tooltip("Invert X Axis of the Look Vector")]
        public BoolReference invertX = new();
        [Tooltip("Invert Y Axis of the Look Vector")]
        public BoolReference invertY = new();

        [Tooltip("Multiplier to rotate the X Axis")]
        public FloatReference XMultiplier = new(1);
        [Tooltip("Multiplier to rotate the Y Axis")]
        public FloatReference YMultiplier = new(1);

        [Tooltip("How far in degrees can you move the camera up")]
        public FloatReference TopClamp = new(70.0f);

        [Tooltip("How far in degrees can you move the camera down")]
        public FloatReference BottomClamp = new(-30.0f);

        private float InvertX => invertX.Value ? -1 : 1;
        private float InvertY => invertY.Value ? 1 : -1;

        // cinemachine
        private float _cinemachineTargetYaw;
        private float _cinemachineTargetPitch;
        private const float _threshold = 0.00001f;
        private bool isActive;
    
        private ICinemachineCamera Cam;

        // Start is called before the first frame update
        void Awake()
        {
            if (CamPivot == null)
            {
                CamPivot = new GameObject("CamPivot").transform;
                CamPivot.parent = transform;
                CamPivot.ResetLocal();
            }
            CamPivot.parent = null;

            CamPivot.hideFlags = HideFlags.HideInHierarchy;

            if (TryGetComponent(out Cam))
                Cam.Follow = CamPivot.transform;

            if (Brain == null) Brain = FindObjectOfType<CinemachineBrain>();
        }

        private void OnEnable()
        {
            Brain.m_CameraActivatedEvent.AddListener(CameraChanged);
        }

        private void OnDisable()
        {
            Brain.m_CameraActivatedEvent.RemoveListener(CameraChanged);
            StopAllCoroutines();
        }


        private void CameraChanged(ICinemachineCamera newActiveCam, ICinemachineCamera exit)
        {
            //CheckRotation();
            isActive = Cam == newActiveCam;
        }

        private void CheckRotation()
        {
            CameraPosition();
            var EulerAngles = Brain.transform.eulerAngles; //Get the Brain Rotation to save the movement 
            _cinemachineTargetYaw = ClampAngle(EulerAngles.y, float.MinValue, float.MaxValue);
            _cinemachineTargetPitch = EulerAngles.x > 180 ? EulerAngles.x - 360 : EulerAngles.x; //HACK!!!
            _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);
            CamPivot.rotation = Quaternion.Euler(_cinemachineTargetPitch, _cinemachineTargetYaw, 0.0f);
        }


        private void FixedUpdate()
        {
            if (updateMode == UpdateType.FixedUpdate)
            {
                CameraLogic();
            }
        }

        private void LateUpdate()
        {
            if (updateMode == UpdateType.LateUpdate)
            {
                CameraLogic();
            }
        }

        private void CameraLogic()
        {
            CameraPosition();

            if (isActive)
            {
                CameraRotation();
            }
            else
            {
                CheckRotation();
            }
        }
 

        public void SetLookX(float x) => look.x = x;
        public void SetLookY(float y) => look.y = y;
        public void SetLook(Vector2 look) => this.look.Value = look;
        
        private void CameraPosition()
        {
            if (Target.Value)
            {
                CamPivot.transform.position = Target.position;
            }
        }

        private void CameraRotation()
        {
            // if there is an input and camera position is not fixed
            if (look.Value.sqrMagnitude >= _threshold)
            {
                //Don't multiply mouse input by Time.deltaTime;
                float deltaTimeMultiplier = 1;// Time.deltaTime;

                _cinemachineTargetYaw += look.x * deltaTimeMultiplier * InvertX * XMultiplier;
                _cinemachineTargetPitch += look.y * deltaTimeMultiplier * InvertY * YMultiplier;
            }

            // clamp our rotations so our values are limited 360 degrees
            _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
            _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

            // Cinemachine will follow this target
            CamPivot.rotation = Quaternion.Euler(_cinemachineTargetPitch, _cinemachineTargetYaw, 0.0f);
        }

        public void SetTarget(Transform target) => Target.Value = target;

        private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f) lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }

#if UNITY_EDITOR
        private void Reset()
        {
            Target.UseConstant = false;
            Target.Variable = MTools.GetInstance<TransformVar>("Camera Target");


            if (CamPivot == null)
            {
                CamPivot = new GameObject("Pivot").transform;
                CamPivot.parent = transform;
                CamPivot.ResetLocal();
            }
        }
#endif
    }
}
