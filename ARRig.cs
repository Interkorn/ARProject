using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SpatialTracking;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Создаёт всю AR-«обвязку» кодом: AR Session, XR Origin, камеру с фоном-видео,
/// менеджеры плоскостей / лучей / якорей. Благодаря этому сцены в проекте пустые
/// и не требуют ручной настройки в редакторе.
/// </summary>
public class ARRig
{
    public Camera cam;
    public ARRaycastManager raycast;
    public ARPlaneManager planes;
    public ARAnchorManager anchors;

    static readonly System.Collections.Generic.List<ARRaycastHit> hits = new();

    public static ARRig Create()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.62f, 0.62f, 0.62f);

        var lightGo = new GameObject("Directional Light");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 0.9f;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        var rig = new ARRig();

        var sessionGo = new GameObject("AR Session");
        sessionGo.AddComponent<ARSession>();
        sessionGo.AddComponent<ARInputManager>();

        // Всё собираем в неактивном объекте, чтобы менеджеры стартовали уже с настройками.
        var originGo = new GameObject("XR Origin");
        originGo.SetActive(false);
        var offsetGo = new GameObject("Camera Offset");
        offsetGo.transform.SetParent(originGo.transform, false);
        var camGo = new GameObject("AR Camera") { tag = "MainCamera" };
        camGo.transform.SetParent(offsetGo.transform, false);

        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 30f;
        camGo.AddComponent<AudioListener>();

        var pose = camGo.AddComponent<TrackedPoseDriver>();
        pose.SetPoseSource(TrackedPoseDriver.DeviceType.GenericXRDevice, TrackedPoseDriver.TrackedPose.ColorCamera);
        camGo.AddComponent<ARCameraManager>();
        camGo.AddComponent<ARCameraBackground>();

        var xr = originGo.AddComponent<XROrigin>();
        xr.Origin = originGo;
        xr.CameraFloorOffsetObject = offsetGo;
        xr.Camera = cam;

        rig.cam = cam;
        rig.raycast = originGo.AddComponent<ARRaycastManager>();
        rig.anchors = originGo.AddComponent<ARAnchorManager>();
        rig.planes = originGo.AddComponent<ARPlaneManager>();
        rig.planes.requestedDetectionMode = PlaneDetectionMode.Horizontal;
        rig.planes.planePrefab = MakePlanePrefab();

        originGo.SetActive(true);
        return rig;
    }

    static GameObject MakePlanePrefab()
    {
        // «Префаб» живёт внутри неактивного контейнера — сам по себе в сцене не отображается.
        var holder = new GameObject("Prefab Holder");
        holder.SetActive(false);
        var p = new GameObject("AR Plane");
        p.transform.SetParent(holder.transform, false);
        p.AddComponent<MeshFilter>();
        var mr = p.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.15f, 0.8f, 1f, 0.28f) };
        p.AddComponent<ARPlane>();
        p.AddComponent<ARPlaneMeshVisualizer>();
        return p;
    }

    /// <summary>Луч от точки экрана до горизонтальной плоскости, найденной ARCore.</summary>
    public bool RaycastFloor(Vector2 screenPoint, out Pose pose)
    {
        pose = default;
        if (raycast.Raycast(screenPoint, hits, TrackableType.PlaneWithinPolygon))
        {
            foreach (var h in hits)
            {
                var plane = planes.GetPlane(h.trackableId);
                if (plane != null && plane.alignment == PlaneAlignment.HorizontalUp)
                {
                    pose = h.pose;
                    return true;
                }
            }
        }
        return false;
    }

    public void ShowPlanes(bool show)
    {
        planes.enabled = show;
        planes.SetTrackablesActive(show);
    }

    /// <summary>Создаёт «якорь» ARCore в точке — объекты, привязанные к нему, не «плывут».</summary>
    public GameObject CreateAnchor(Vector3 position, Quaternion rotation)
    {
        var go = new GameObject("Anchor");
        go.transform.SetPositionAndRotation(position, rotation);
        go.AddComponent<ARAnchor>();
        return go;
    }
}
