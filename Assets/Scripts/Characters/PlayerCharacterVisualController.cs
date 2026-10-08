using MaliGo.Core;
using UnityEngine;

namespace MaliGo.Characters
{
    /// <summary>
    /// Drives the human player Kenney 3D model for 2.5D isometric gameplay: which legs play (Idle, or the
    /// Idle/Run "Locomotion" blend), how fast they play so the planted foot keeps pace with the ground, which way
    /// the body faces, a slight lean into speeding up, and keeping the soles on the ground.
    /// No extra body bob is added: the clips already bob the hips (0.04 model units, measured), and moving this
    /// root up and down would lift the planted foot off the ground - the very float this is meant to remove.
    /// </summary>
    public class PlayerCharacterVisualController : MonoBehaviour
    {
        static readonly int SpeedHash = Animator.StringToHash("Speed");
        static readonly int GaitHash = Animator.StringToHash("Gait");

        /// <summary>Damping (seconds) of the Gait blend weight and of the lean, so neither pops.</summary>
        const float GaitDampSeconds = 0.12f;
        const float LeanSmoothSeconds = 0.12f;

        [Header("Animation")]
        [SerializeField] Animator animator;
        [SerializeField] float rotationSpeed = 12f;
        [SerializeField] float walkSpeedThreshold = 0.15f;
        [SerializeField] float runAnimSpeed = 1f;

        [Header("Appearance")]
        [SerializeField] Renderer[] skinRenderers;

        public Animator Animator => animator;
        public Renderer[] SkinRenderers => skinRenderers;

        bool hasGaitParameter;
        bool moving;
        float yaw;
        float lean;
        float leanVelocity;

        void Awake()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (skinRenderers == null || skinRenderers.Length == 0)
            {
                skinRenderers = GetComponentsInChildren<Renderer>();
            }

            yaw = transform.eulerAngles.y;
        }

        void Start()
        {
            hasGaitParameter = HasParameter(GaitHash);
            RestOnGround();
        }

        /// <summary>
        /// Puts this visual root's soles on the ground. Kenney's model has its pivot exactly at its soles
        /// (measured from the FBX: mesh bottom at 0, toe joints at 0.013 model units in the idle, rest and planted
        /// run poses), but a CharacterController floats its capsule skinWidth above whatever it stands on, so
        /// with the visual at the capsule's bottom the feet hovered by the skin width.
        /// </summary>
        void RestOnGround()
        {
            CharacterController controller = GetComponentInParent<CharacterController>();
            if (controller == null || controller.transform != transform.parent)
            {
                return;
            }

            float capsuleBottom = controller.center.y - controller.height * 0.5f;
            Vector3 local = transform.localPosition;
            local.y = capsuleBottom - controller.skinWidth;
            transform.localPosition = local;
        }

        /// <summary>
        /// Called every frame by the movement controller. <paramref name="facingYaw"/> is the body's heading
        /// (already turned at a capped rate), <paramref name="groundSpeed"/> how fast the body really moves over the
        /// ground (u/s), <paramref name="forwardAcceleration"/> its change (u/s²) for the lean.
        /// </summary>
        public void UpdateLocomotion(float facingYaw, float groundSpeed, float forwardAcceleration)
        {
            yaw = facingYaw;
            float dt = Time.deltaTime;

            float targetLean = MovementMath.LeanDegrees(groundSpeed, forwardAcceleration);
            lean = Mathf.SmoothDamp(lean, targetLean, ref leanVelocity, LeanSmoothSeconds, Mathf.Infinity, dt);
            // Yaw and lean on this root, whose pivot is at the soles: the lean tips the body over its feet and
            // never lifts a planted foot. The Animator drives the bones below and is never fought.
            transform.rotation = Quaternion.Euler(lean, yaw, 0f);

            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return;
            }

            moving = MovementMath.IsMoving(groundSpeed, moving);
            // "Speed" only picks Idle or Locomotion (crossfaded in the controller at 0.05, or 0.1-0.15 in the
            // fallback controllers built by MaliGoPlayerCharacterSetup / KenneyRuntimeCatalogFactory); 0 means stand.
            animator.SetFloat(SpeedHash, moving ? Mathf.Max(groundSpeed, 0.2f) : 0f);

            if (!moving)
            {
                animator.speed = 1f;
                return;
            }

            float gait = 1f;
            if (hasGaitParameter)
            {
                animator.SetFloat(GaitHash, MovementMath.GaitForSpeed(groundSpeed), GaitDampSeconds, dt);
                gait = animator.GetFloat(GaitHash);
            }

            // Playback = ground speed / the blend's own foot speed, so the planted foot keeps pace with the
            // ground (MovementMath.PlaybackRate; strides measured from the FBX).
            animator.speed = runAnimSpeed * MovementMath.PlaybackRate(groundSpeed, gait, ModelScale());
        }

        /// <summary>Older entry point (velocity-driven); turns toward the velocity and drives the legs from it.</summary>
        public void UpdateVisual(Vector2 input, bool isMoving, Vector3 worldVelocity)
        {
            Vector3 flat = new Vector3(worldVelocity.x, 0f, worldVelocity.z);
            float facing = yaw;
            if (isMoving && flat.sqrMagnitude > 0.0001f)
            {
                float target = MovementMath.YawOf(flat.x, flat.z);
                facing = Mathf.LerpAngle(yaw, target, 1f - Mathf.Exp(-rotationSpeed * Time.deltaTime));
            }

            UpdateLocomotion(facing, isMoving ? Mathf.Max(flat.magnitude, walkSpeedThreshold) : 0f, 0f);
        }

        /// <summary>World size of one model unit: the model object's scale (the Animator sits on its armature
        /// child, see CharacterRig), 0.12 for the spawned player.</summary>
        float ModelScale()
        {
            Transform model = animator.transform.parent;
            float scale = model != null ? model.lossyScale.y : animator.transform.lossyScale.y * 0.01f;
            return scale > 0f ? scale : MovementMath.DefaultModelScale;
        }

        bool HasParameter(int hash)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return false;
            }

            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash == hash)
                {
                    return true;
                }
            }

            return false;
        }

        public void SetSkinMaterial(Material material)
        {
            if (material == null || skinRenderers == null)
            {
                return;
            }

            foreach (Renderer renderer in skinRenderers)
            {
                if (renderer != null)
                {
                    renderer.sharedMaterial = material;
                }
            }
        }

        public void SetSkinTexture(Texture2D texture)
        {
            if (texture == null || skinRenderers == null)
            {
                return;
            }

            foreach (Renderer renderer in skinRenderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                var mat = renderer.material;
                if (mat.HasProperty("_BaseMap"))
                {
                    mat.SetTexture("_BaseMap", texture);
                }
                else if (mat.HasProperty("_MainTex"))
                {
                    mat.SetTexture("_MainTex", texture);
                }
            }
        }
    }
}
