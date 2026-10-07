using UnityEngine;

namespace MaliGo.Characters
{
    /// <summary>
    /// Drives the human player Kenney 3D model for 2.5D isometric gameplay.
    /// Handles locomotion animation and facing on the XZ plane.
    /// </summary>
    public class PlayerCharacterVisualController : MonoBehaviour
    {
        static readonly int SpeedHash = Animator.StringToHash("Speed");

        [Header("Animation")]
        [SerializeField] Animator animator;
        [SerializeField] float rotationSpeed = 12f;
        [SerializeField] float walkSpeedThreshold = 0.15f;
        [SerializeField] float runAnimSpeed = 1f;

        [Header("Appearance")]
        [SerializeField] Renderer[] skinRenderers;

        public Animator Animator => animator;
        public Renderer[] SkinRenderers => skinRenderers;

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
        }

        public void UpdateVisual(Vector2 input, bool isMoving, Vector3 worldVelocity)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return;
            }

            float speed = isMoving ? Mathf.Max(worldVelocity.magnitude, walkSpeedThreshold) : 0f;
            animator.SetFloat(SpeedHash, speed);

            if (isMoving && worldVelocity.sqrMagnitude > 0.0001f)
            {
                Vector3 flatDirection = new Vector3(worldVelocity.x, 0f, worldVelocity.z).normalized;
                if (flatDirection.sqrMagnitude > 0.0001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(flatDirection, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                }
            }

            // The run clip is the only moving state, so its playback follows the ground speed: at the 1.0 u/s walk
            // it plays at ~0.8x so the feet neither skate nor spin in place (MovementMath.RunPlaybackRate).
            animator.speed = isMoving ? runAnimSpeed * MaliGo.Core.MovementMath.RunPlaybackRate(speed) : 1f;
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
