using UnityEngine;

namespace MaliGo.Characters
{
    /// <summary>
    /// Kenney's animation clips (idle.fbx, run.fbx) are authored for the character's armature ("Root"), not
    /// the model's top object: their root path keys the armature's scale to 100. With the Animator on the
    /// model's top object, every frame of animation overwrote the 0.12 world-fit scale there with 100, making
    /// the character about 800 times too big and drifting it far from its CharacterController. Putting the
    /// Animator on the armature binds every curve to the bone it was written for and leaves the world-fit
    /// scale alone (measured with Assets/Editor/MaliGoSizeProbe.cs: 0.48 units tall before and after).
    /// </summary>
    public static class CharacterRig
    {
        public const string ArmatureName = "Root";

        public static Animator AttachAnimator(GameObject modelInstance, RuntimeAnimatorController controller)
        {
            if (modelInstance == null)
            {
                return null;
            }

            Transform armature = modelInstance.transform.Find(ArmatureName);
            Transform host = armature != null ? armature : modelInstance.transform;

            // Any Animator left elsewhere in the model would still drive the wrong object (and
            // GetComponentInChildren would find it first), so remove it before adding ours.
            foreach (Animator existing in modelInstance.GetComponentsInChildren<Animator>(true))
            {
                if (existing.transform != host)
                {
                    Object.DestroyImmediate(existing);
                }
            }

            Animator animator = host.GetComponent<Animator>();
            if (animator == null)
            {
                animator = host.gameObject.AddComponent<Animator>();
            }

            if (armature != null)
            {
                // Generic clips bind by transform path from the Animator; the model's avatar was built for
                // the top object and would map the curves one level off.
                animator.avatar = null;
            }

            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            if (controller != null)
            {
                animator.runtimeAnimatorController = controller;
            }

            return animator;
        }
    }
}
