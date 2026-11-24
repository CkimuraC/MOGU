using UnityEngine;

/// <summary>モグラを追従するOrthographicカメラ。地形の端ではクランプする。</summary>
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public DirtTerrain terrain;
    public float orthoSize = 6f;
    public float smoothTime = 0.08f;
    public Color backgroundColor = new Color(0.08f, 0.05f, 0.03f);

    Camera cam;
    Vector3 velocity;

    void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = orthoSize;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = backgroundColor;
        if (target == null)
        {
            var mole = FindFirstObjectByType<MoleController>();
            if (mole != null) target = mole.transform;
        }
        if (terrain == null) terrain = FindFirstObjectByType<DirtTerrain>();
    }

    void Start()
    {
        if (target != null)
        {
            var p = Clamp(target.position);
            transform.position = new Vector3(p.x, p.y, transform.position.z);
        }
    }

    Vector2 Clamp(Vector2 p)
    {
        if (terrain == null) return p;
        Rect r = SurfaceArea.Expand(terrain.WorldRect); // 地上の分もカメラが動ける
        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        p.x = r.width <= halfW * 2f ? r.center.x : Mathf.Clamp(p.x, r.xMin + halfW, r.xMax - halfW);
        p.y = r.height <= halfH * 2f ? r.center.y : Mathf.Clamp(p.y, r.yMin + halfH, r.yMax - halfH);
        return p;
    }

    void LateUpdate()
    {
        if (target == null) return;
        Vector2 goal = Clamp(target.position);
        Vector3 goal3 = new Vector3(goal.x, goal.y, transform.position.z);
        transform.position = Vector3.SmoothDamp(transform.position, goal3, ref velocity, smoothTime);
    }
}
