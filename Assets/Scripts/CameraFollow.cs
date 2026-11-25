using UnityEngine;

/// <summary>モグラを追従するOrthographicカメラ。地形の端ではクランプする。</summary>
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public DirtTerrain terrain;
    public float orthoSize = 6f;

    [Header("Zoom (引いて見る)")]
    [Tooltip("Zキー(押している間)、またはR3(押すたびに切り替え)で、見える範囲をこの倍率に広げる。1=変わらない")]
    public float zoomOutFactor = 2.2f;
    [Tooltip("引く・戻すときの、なめらかさ(秒)。小さいほど、すぐ切り替わる")]
    public float zoomSmoothTime = 0.2f;
    [Tooltip("オンにすると、キーを押さなくても、引いたままになる(開発用)")]
    public bool zoomLocked = false;

    float zoomVelocity;
    bool zoomToggled; // R3で「引いたまま」になっているか

    /// <summary>R3で「引いたまま」になっているか</summary>
    public bool ZoomToggled { get { return zoomToggled; } }

    /// <summary>引いたまま / ふだん の切り替え(R3を押したとき)。</summary>
    public void ToggleZoom() { zoomToggled = !zoomToggled; }
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

/// <summary>カメラを、すぐに追従先へ移す(ワープ・帰還のあとに使う)。</summary>
    public void Snap()
    {
        if (target == null) return;
        var p = Clamp(target.position);
        transform.position = new Vector3(p.x, p.y, transform.position.z);
        velocity = Vector3.zero;
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
        if (MoleInput.ZoomTogglePressed()) ToggleZoom(); // R3: 1回押すと引く、もう一回押すと戻る
        UpdateZoom(Time.unscaledDeltaTime);               // メニューで時間が止まっていても動くよう、止まらない時間を使う

        if (target == null) return;
        Vector2 goal = Clamp(target.position);
        Vector3 goal3 = new Vector3(goal.x, goal.y, transform.position.z);
        transform.position = Vector3.SmoothDamp(transform.position, goal3, ref velocity, smoothTime);
    }

    /// <summary>キーを押している間、見える範囲をなめらかに広げる(離すと戻る)。</summary>
public void UpdateZoom(float dt)
    {
        // Zキーを押している間 / R3で切り替えた状態 / デバッグで固定した状態、のどれかなら、引く
        bool zoomOut = zoomLocked || zoomToggled || MoleInput.ZoomOutHeld();
        float targetSize = orthoSize * (zoomOut ? Mathf.Max(1f, zoomOutFactor) : 1f);
        cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, targetSize, ref zoomVelocity,
            Mathf.Max(0.01f, zoomSmoothTime), Mathf.Infinity, dt);
    }
}
