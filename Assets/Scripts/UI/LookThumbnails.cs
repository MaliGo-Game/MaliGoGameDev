using System.Collections.Generic;
using MaliGo.Data;
using MaliGo.PlayerIdentity;
using UnityEngine;

namespace MaliGo.UI
{
    /// <summary>
    /// The small character portraits on the look screen's outfit cards: one model per outfit
    /// (<see cref="CharacterLooks.Outfits"/>) standing in a row at world (0, -560, 0), well away from the
    /// <see cref="LookPreview"/> model, filmed by one orthographic camera into one <c>RenderTexture</c> strip; each
    /// card shows its cell through <see cref="UvRect"/>. The models wear the half-size skin textures in the picked
    /// tone (<see cref="SetTone"/>).
    ///
    /// Rendered on demand, not every frame: the camera and the animators run for a few frames after a build or a
    /// tone change (long enough for the idle pose to land) and are then switched off, so the cards are still
    /// pictures that cost nothing while the player decides; the big preview is the one that moves. The strip is
    /// redrawn when the app regains focus, in case the GPU dropped the texture. <c>OnDestroy</c> releases the
    /// materials (and their shared textures), the models, the camera and the texture.
    /// </summary>
    public class LookThumbnails : MonoBehaviour
    {
        public static readonly Vector3 RowOrigin = new Vector3(0f, -560f, 0f);
        const int CellPixelHeight = 256;
        const float ModelScale = 0.12f;
        // Turned a little from facing the camera, so the cards read as figures rather than flat fronts.
        const float FacingDegrees = 195f;
        const float TopRoom = 0.05f;
        // Share of the figure (from the top of the head) shown in a cell.
        const float VisibleFigure = 0.72f;
        const int RenderFrames = 3;

        PlayerCharacterCatalog catalog;
        RenderTexture texture;
        Camera stripCamera;
        readonly List<GameObject> models = new List<GameObject>();
        readonly List<Renderer[]> modelRenderers = new List<Renderer[]>();
        readonly List<Animator> animators = new List<Animator>();
        readonly List<Material> materials = new List<Material>();
        int framesLeft;

        public Texture Texture => texture;
        public int Count => models.Count;

        /// <summary>The strip's cell for outfit <paramref name="index"/>, for a <c>RawImage.uvRect</c>.</summary>
        public Rect UvRect(int index)
        {
            float width = models.Count > 0 ? 1f / models.Count : 1f;
            return new Rect(index * width, 0f, width, 1f);
        }

        /// <summary>
        /// Builds the strip under <paramref name="parent"/> (destroyed with it). Each cell is
        /// <paramref name="cellAspect"/> (width / height) like the card area it fills, and the bottom
        /// <paramref name="bottomReserve"/> of a cell (0..0.5) is left empty for the card's label. Null when the
        /// catalog or its model is missing (cards then show their labels only).
        /// </summary>
        public static LookThumbnails Create(Transform parent, PlayerCharacterCatalog catalog, float cellAspect,
            float bottomReserve, AppearanceData tone)
        {
            if (parent == null || catalog == null || catalog.characterModelPrefab == null)
            {
                return null;
            }

            var go = new GameObject("LookThumbnails", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var thumbnails = go.AddComponent<LookThumbnails>();
            thumbnails.Build(catalog, Mathf.Clamp(cellAspect, 0.3f, 3f), Mathf.Clamp(bottomReserve, 0f, 0.5f), tone);
            return thumbnails;
        }

        void Build(PlayerCharacterCatalog source, float cellAspect, float bottomReserve, AppearanceData tone)
        {
            catalog = source;
            int count = CharacterLooks.Outfits.Length;

            for (int i = 0; i < count; i++)
            {
                GameObject model = Instantiate(catalog.characterModelPrefab);
                model.name = "LookThumbnail_" + CharacterLooks.Outfits[i].id;
                model.transform.position = RowOrigin;
                model.transform.rotation = Quaternion.Euler(0f, FacingDegrees, 0f);
                model.transform.localScale = Vector3.one * ModelScale;

                // On the armature, as in the world and the big preview (see CharacterRig).
                Animator animator = MaliGo.Characters.CharacterRig.AttachAnimator(model, catalog.animatorController);
                if (animator != null)
                {
                    animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                    animators.Add(animator);
                }

                models.Add(model);
                modelRenderers.Add(model.GetComponentsInChildren<Renderer>(true));
                materials.Add(null);
            }

            // Height of one figure, from the first model's bind pose.
            Bounds bounds = new Bounds(RowOrigin + Vector3.up * 0.23f, new Vector3(0.4f, 0.47f, 0.2f));
            bool found = false;
            foreach (Renderer r in modelRenderers[0])
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

            // Head to mid-thigh fills the cell above the label: a portrait, so faces and tones read at card size.
            float figureHeight = Mathf.Max(bounds.size.y, 0.05f);
            float cellHeight = figureHeight * VisibleFigure / Mathf.Max(0.3f, 1f - bottomReserve - TopRoom);
            float cellWidth = cellHeight * cellAspect;
            float headTop = bounds.max.y;
            for (int i = 0; i < count; i++)
            {
                models[i].transform.position = RowOrigin + new Vector3(i * cellWidth, 0f, 0f);
            }

            int cellPixelWidth = Mathf.Max(16, Mathf.RoundToInt(CellPixelHeight * cellAspect));
            texture = new RenderTexture(cellPixelWidth * count, CellPixelHeight, 16, RenderTextureFormat.ARGB32)
            {
                name = "LookThumbnails_RT",
                // One sample, and no MSAA on the camera, as in LookPreview.
                antiAliasing = 1
            };
            texture.Create();

            // At the scene root like LookPreview's camera (not under the scaled canvas); destroyed in OnDestroy.
            var cameraObject = new GameObject("LookThumbnails_Camera");
            stripCamera = cameraObject.AddComponent<Camera>();
            stripCamera.allowMSAA = false;
            stripCamera.orthographic = true;
            stripCamera.orthographicSize = cellHeight * 0.5f;
            stripCamera.clearFlags = CameraClearFlags.SolidColor;
            // White and see-through: where the texture keeps alpha the card's own fill shows around the figure,
            // and where it does not the cell is plain white like an unselected card.
            stripCamera.backgroundColor = new Color(1f, 1f, 1f, 0f);
            stripCamera.nearClipPlane = 0.05f;
            stripCamera.farClipPlane = 30f;
            stripCamera.targetTexture = texture;
            stripCamera.depth = -11f;
            cameraObject.transform.position = new Vector3(
                RowOrigin.x + (count - 1) * cellWidth * 0.5f,
                headTop + TopRoom * cellHeight - cellHeight * 0.5f,
                RowOrigin.z - 10f);
            cameraObject.transform.rotation = Quaternion.identity;

            SetTone(tone);
        }

        /// <summary>Re-skins every figure in <paramref name="tone"/>'s skin tone and redraws the strip.</summary>
        public void SetTone(AppearanceData tone)
        {
            if (catalog == null)
            {
                return;
            }

            int toneIndex = CharacterLooks.ToneIndex(tone);
            for (int i = 0; i < models.Count; i++)
            {
                AppearanceData look = CharacterLooks.Create(i, toneIndex);
                Material next = catalog.CreateRuntimeSkinMaterial(look, thumbnail: true);
                if (next == null)
                {
                    continue;
                }

                foreach (Renderer r in modelRenderers[i])
                {
                    if (r == null)
                    {
                        continue;
                    }

                    Material[] shared = r.sharedMaterials;
                    for (int m = 0; m < shared.Length; m++)
                    {
                        shared[m] = next;
                    }

                    r.sharedMaterials = shared;
                }

                PlayerCharacterCatalog.ReleaseRuntimeSkinMaterial(materials[i]);
                materials[i] = next;
            }

            Redraw();
        }

        void Redraw()
        {
            framesLeft = RenderFrames;
            if (stripCamera != null)
            {
                stripCamera.enabled = true;
            }

            foreach (Animator animator in animators)
            {
                if (animator != null)
                {
                    animator.enabled = true;
                }
            }
        }

        /// <summary>Counts down the frames still to draw (this frame renders after Update); on the first frame after
        /// them the camera and animators go off and the strip stays a still.</summary>
        void Update()
        {
            if (framesLeft > 0)
            {
                framesLeft--;
                return;
            }

            if (stripCamera == null || !stripCamera.enabled)
            {
                return;
            }

            stripCamera.enabled = false;

            foreach (Animator animator in animators)
            {
                if (animator != null)
                {
                    animator.enabled = false;
                }
            }
        }

        void OnApplicationFocus(bool focused)
        {
            if (focused && texture != null)
            {
                Redraw();
            }
        }

        void OnDestroy()
        {
            if (stripCamera != null)
            {
                stripCamera.targetTexture = null;
                Destroy(stripCamera.gameObject);
            }

            for (int i = 0; i < materials.Count; i++)
            {
                PlayerCharacterCatalog.ReleaseRuntimeSkinMaterial(materials[i]);
            }

            materials.Clear();

            foreach (GameObject model in models)
            {
                if (model != null)
                {
                    Destroy(model);
                }
            }

            models.Clear();

            if (texture != null)
            {
                texture.Release();
                Destroy(texture);
                texture = null;
            }
        }
    }
}
