using MaliGo.Data;
using MaliGo.PlayerIdentity;
using MaliGo.UI.Kit;
using UnityEngine;
using UnityEngine.UI;

namespace MaliGo.UI
{
    /// <summary>
    /// The turning character on character creation's look screen (DESIGN_SPEC §5.4.13): a 560 x 560
    /// <c>RawImage</c> showing a 512 x 512 <c>RenderTexture</c> drawn by a dedicated camera pointed at a model
    /// instance of <c>PlayerCharacterCatalog.characterModelPrefab</c> placed at world (0, -500, 0). The skin is a
    /// runtime material from <c>CreateRuntimeSkinMaterial</c>; the model turns 20 deg/s on unscaled time (static with
    /// reduce motion). The model is the bare FBX, so an <c>Animator</c> with <c>catalog.animatorController</c> is
    /// added (idle, no T-pose). No 3D light is added (the scene uses Renderer2D with a Light2D only).
    /// <c>OnDestroy</c> destroys the runtime material, the model and the camera, and releases and destroys the
    /// texture. <see cref="Create"/> returns null when the catalog or the model is missing (cards only).
    /// </summary>
    public class LookPreview : MonoBehaviour
    {
        public static readonly Vector3 ModelPosition = new Vector3(0f, -500f, 0f);
        public const float ImageSize = 560f;
        public const int TextureSize = 512;
        public const float TurnDegreesPerSecond = 20f;
        const float FieldOfView = 30f;
        const float ModelScale = 0.12f;

        PlayerCharacterCatalog catalog;
        RenderTexture texture;
        Camera previewCamera;
        GameObject model;
        Material runtimeMaterial;
        Renderer[] renderers;
        RawImage image;

        public RectTransform Rect => image != null ? image.rectTransform : null;

        /// <summary>Builds the preview under <paramref name="parent"/> (the RawImage is 560 x 560, centred on its
        /// anchor; position it as needed). Null when the catalog or its model is missing.</summary>
        public static LookPreview Create(RectTransform parent, PlayerCharacterCatalog catalog, AppearanceData appearance)
        {
            if (parent == null || catalog == null || catalog.characterModelPrefab == null)
            {
                return null;
            }

            var go = new GameObject("LookPreview", typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(ImageSize, ImageSize);

            var preview = go.AddComponent<LookPreview>();
            if (!preview.Build(catalog, appearance))
            {
                Destroy(go);
                return null;
            }

            return preview;
        }

        bool Build(PlayerCharacterCatalog source, AppearanceData appearance)
        {
            catalog = source;

            texture = new RenderTexture(TextureSize, TextureSize, 24, RenderTextureFormat.ARGB32)
            {
                name = "LookPreview_RT",
                // One sample, and the camera below asks for no MSAA: on device the pipeline requested
                // 2 samples against a 1-sample attachment and logged an error every frame.
                antiAliasing = 1
            };
            texture.Create();

            image = gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;

            model = Instantiate(catalog.characterModelPrefab);
            model.name = "LookPreview_Model";
            model.transform.position = ModelPosition;
            model.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            model.transform.localScale = Vector3.one * ModelScale;

            // On the armature, as in the world (see CharacterRig): otherwise the idle clip resets the
            // model's scale to 100 and the preview shows nothing recognisable.
            Animator animator = MaliGo.Characters.CharacterRig.AttachAnimator(model, catalog.animatorController);

            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            renderers = model.GetComponentsInChildren<Renderer>(true);
            SetAppearance(appearance);

            var cameraObject = new GameObject("LookPreview_Camera");
            previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.allowMSAA = false;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = UiTheme.Tint;
            previewCamera.fieldOfView = FieldOfView;
            previewCamera.nearClipPlane = 0.05f;
            previewCamera.farClipPlane = 30f;
            previewCamera.targetTexture = texture;
            previewCamera.depth = -10f;
            Frame();
            return true;
        }

        /// <summary>Points the camera at the model's bounds so the whole body fits with a little room.</summary>
        void Frame()
        {
            Bounds bounds = new Bounds(ModelPosition + Vector3.up * 0.9f, Vector3.one * 1.8f);
            bool found = false;
            foreach (Renderer r in renderers)
            {
                if (r == null)
                {
                    continue;
                }

                if (!found)
                {
                    bounds = r.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            float halfHeight = Mathf.Max(bounds.extents.y, 0.1f) * 1.15f;
            float distance = halfHeight / Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad);
            Vector3 centre = bounds.center;
            previewCamera.transform.position = centre + new Vector3(0f, 0f, -distance);
            previewCamera.transform.LookAt(centre);
            previewCamera.farClipPlane = distance + 10f;
        }

        /// <summary>Re-skins the model for <paramref name="appearance"/> (the previous runtime material is destroyed and
        /// its shared skin texture released).</summary>
        public void SetAppearance(AppearanceData appearance)
        {
            if (catalog == null || renderers == null)
            {
                return;
            }

            Material next = catalog.CreateRuntimeSkinMaterial(appearance);
            if (next == null)
            {
                return;
            }

            foreach (Renderer r in renderers)
            {
                if (r == null)
                {
                    continue;
                }

                Material[] materials = r.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = next;
                }

                r.sharedMaterials = materials;
            }

            if (runtimeMaterial != null)
            {
                PlayerCharacterCatalog.ReleaseRuntimeSkinMaterial(runtimeMaterial);
            }

            runtimeMaterial = next;
        }

        void Update()
        {
            if (model == null || UiTween.ReduceMotion)
            {
                return;
            }

            model.transform.Rotate(0f, TurnDegreesPerSecond * Time.unscaledDeltaTime, 0f, Space.World);
        }

        void OnDestroy()
        {
            if (previewCamera != null)
            {
                previewCamera.targetTexture = null;
                Destroy(previewCamera.gameObject);
            }

            if (runtimeMaterial != null)
            {
                PlayerCharacterCatalog.ReleaseRuntimeSkinMaterial(runtimeMaterial);
                runtimeMaterial = null;
            }

            if (model != null)
            {
                Destroy(model);
                model = null;
            }

            if (image != null)
            {
                image.texture = null;
            }

            if (texture != null)
            {
                texture.Release();
                Destroy(texture);
                texture = null;
            }
        }
    }
}
