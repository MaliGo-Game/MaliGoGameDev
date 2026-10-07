using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Read-only diagnostic: measures the world camera, the largest renderers in MaliGoWorld, Mali, and the
/// player character exactly as PlayerCharacterSpawner builds it, before and after its animations play.
/// Run headless: Unity -batchmode -quit -executeMethod MaliGoSizeProbe.Run -sizeProbeOut &lt;file&gt;
/// Opens the scene but never saves it.
/// </summary>
public static class MaliGoSizeProbe
{
    [MenuItem("MaliGo/Diagnostics/Size Probe (Read-Only)")]
    public static void Run()
    {
        var log = new StringBuilder();
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MaliGoWorld.unity", OpenSceneMode.Single);

            foreach (Camera cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                log.AppendLine($"CAMERA {cam.name} ortho={cam.orthographic} orthoSize={cam.orthographicSize} fov={cam.fieldOfView} pos={cam.transform.position} rot={cam.transform.eulerAngles} near={cam.nearClipPlane} far={cam.farClipPlane} clear={cam.clearFlags}");
                var controller = cam.GetComponent<MaliGoCameraController>();
                if (controller != null)
                {
                    log.AppendLine($"  controller offset={controller.offset} rotation={controller.isometricRotation} minZoom={controller.minZoom} maxZoom={controller.maxZoom}");
                }
            }

            GameObject spawn = GameObject.Find("PlayerSpawnPoint");
            log.AppendLine("SPAWN " + (spawn != null ? spawn.transform.position.ToString() : "none"));

            log.AppendLine("LARGEST RENDERERS IN SCENE:");
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                         .OrderByDescending(r => r.bounds.size.magnitude).Take(15))
            {
                log.AppendLine($"  {PathOf(r.transform)}  size={r.bounds.size}  center={r.bounds.center}");
            }

            GameObject mali = GameObject.Find("Mali");
            if (mali != null)
            {
                log.AppendLine($"MALI root scale={mali.transform.lossyScale} pos={mali.transform.position}");
                foreach (Renderer r in mali.GetComponentsInChildren<Renderer>(true))
                {
                    log.AppendLine($"  {PathOf(r.transform)} {r.GetType().Name} size={r.bounds.size} lossyScale={r.transform.lossyScale}");
                }
            }

            GameObject house = GameObject.Find("Player_House");
            if (house != null)
            {
                Bounds hb = BoundsOf(house);
                log.AppendLine($"PLAYER_HOUSE size={hb.size}");
            }

            var catalog = Resources.Load<MaliGo.PlayerIdentity.PlayerCharacterCatalog>("PlayerCharacterCatalog");
            if (catalog == null || catalog.characterModelPrefab == null)
            {
                log.AppendLine("CATALOG missing");
            }
            else
            {
                var root = new GameObject("ProbePlayer");
                var visual = new GameObject("HumanVisual");
                visual.transform.SetParent(root.transform, false);
                GameObject model = Object.Instantiate(catalog.characterModelPrefab, visual.transform);
                model.transform.localPosition = Vector3.zero;
                model.transform.localScale = Vector3.one * 0.12f;

                log.AppendLine("PLAYER (as spawned, bind pose):");
                DumpHierarchy(model.transform, log, 0, 3);
                log.AppendLine("  meshBounds(bind)=" + MeshBounds(model));

                RuntimeAnimatorController controller = catalog.animatorController;
                log.AppendLine("ANIMATOR " + (controller != null ? controller.name : "none"));
                if (controller != null)
                {
                    foreach (AnimationClip clip in controller.animationClips.Distinct())
                    {
                        GameObject copy = Object.Instantiate(model, visual.transform);
                        copy.transform.localScale = Vector3.one * 0.12f;
                        clip.SampleAnimation(copy, Mathf.Min(0.3f, clip.length * 0.5f));
                        log.AppendLine($"PLAYER after sampling clip '{clip.name}' (length {clip.length:0.00}s):");
                        DumpHierarchy(copy.transform, log, 0, 3);
                        log.AppendLine("  meshBounds(sampled)=" + MeshBounds(copy));
                        log.AppendLine("  scale/position curves in clip:");
                        foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(clip)
                                     .Where(b => b.propertyName.StartsWith("m_LocalScale") || (b.propertyName.StartsWith("m_LocalPosition") && b.path.Split('/').Length <= 2)))
                        {
                            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, b);
                            log.AppendLine($"    '{b.path}' {b.propertyName} first={curve.keys.FirstOrDefault().value}");
                        }
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            log.AppendLine("PROBE ERROR: " + ex);
        }

        try
        {
            log.AppendLine("CLIPS IN ANIMATION FILES:");
            foreach (string path in new[] { "idle", "run", "jump" }.Select(n => $"Assets/kenney_animated-characters-protagonists/Animations/{n}.fbx"))
            {
                foreach (AnimationClip clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>())
                {
                    if (clip.name.StartsWith("__preview__"))
                    {
                        continue;
                    }

                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(clip, out string guid, out long fileId);
                    log.AppendLine($"  {path}  '{clip.name}'  length={clip.length:0.00}s loop={clip.isLooping}  fileID={fileId} guid={guid}");
                }
            }

            // The proposed fix: scale on a parent, Animator on the armature ("Root") so the clip's ''
            // path binds to the armature it was authored for.
            var catalog = Resources.Load<MaliGo.PlayerIdentity.PlayerCharacterCatalog>("PlayerCharacterCatalog");
            var parent = new GameObject("FixProbe");
            var holder = new GameObject("ScaledHolder");
            holder.transform.SetParent(parent.transform, false);
            holder.transform.localScale = Vector3.one * 0.12f;
            GameObject model = Object.Instantiate(catalog.characterModelPrefab, holder.transform);
            Transform armature = model.transform.Find("Root");
            log.AppendLine($"FIX PROBE: armature found={(armature != null)}");
            foreach (AnimationClip clip in AssetDatabase.LoadAllAssetsAtPath("Assets/kenney_animated-characters-protagonists/Animations/idle.fbx").OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")))
            {
                if (armature == null)
                {
                    break;
                }

                clip.SampleAnimation(armature.gameObject, Mathf.Min(0.3f, clip.length * 0.5f));
                log.AppendLine($"  sampled '{clip.name}' on armature: meshBounds={MeshBounds(model)}  armatureScale={armature.localScale} hips={armature.Find("HipsCtrl/Hips")?.position}");
            }
        }
        catch (System.Exception ex)
        {
            log.AppendLine("PROBE ERROR (part 2): " + ex);
        }

        string outPath = GetArg("-sizeProbeOut") ?? Path.Combine(Path.GetTempPath(), "maligo_size_probe.txt");
        File.WriteAllText(outPath, log.ToString());
        Debug.Log("[MaliGoSizeProbe] wrote " + outPath + "\n" + log);
        if (Application.isBatchMode)
        {
            EditorApplication.Exit(0);
        }
    }

    static void DumpHierarchy(Transform t, StringBuilder log, int depth, int maxDepth)
    {
        log.AppendLine($"  {new string(' ', depth * 2)}{t.name} localPos={t.localPosition} localScale={t.localScale} worldPos={t.position} lossyScale={t.lossyScale}");
        if (depth >= maxDepth)
        {
            return;
        }

        foreach (Transform child in t)
        {
            DumpHierarchy(child, log, depth + 1, maxDepth);
        }
    }

    static string MeshBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return "no renderers";
        }

        Bounds b = renderers[0].bounds;
        foreach (Renderer r in renderers)
        {
            b.Encapsulate(r.bounds);
        }

        return $"size={b.size} center={b.center}";
    }

    static Bounds BoundsOf(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        Bounds b = renderers.Length > 0 ? renderers[0].bounds : new Bounds(go.transform.position, Vector3.zero);
        foreach (Renderer r in renderers)
        {
            b.Encapsulate(r.bounds);
        }

        return b;
    }

    static string PathOf(Transform t)
    {
        return t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    }

    static string GetArg(string name)
    {
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
            {
                return args[i + 1];
            }
        }

        return null;
    }
}
