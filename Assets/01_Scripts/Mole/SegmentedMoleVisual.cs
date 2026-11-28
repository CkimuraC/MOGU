using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// モグラの体が、カーブで曲がる表現の試作。
///
/// ■ しくみ
///   いまの1枚絵(32×24)を、縦に N 個(標準4)に切る。
///   前の部分(頭)は、進む向きにつく。後ろの部分は、モグラが通った道(軌跡)を、遅れてたどる。
///   各部分は、その場所の道の向きに、回る。だから、カーブでは、体が「くの字」に曲がる。
///
/// ■ 元の絵
///   元の1枚絵(Visual)は、消さずに、表示だけ止める。Bend Enabled をオフにすると、元に戻る。
///
/// ■ 調整
///   デバッグパネル(F2)の「モグラの体(曲がる表現)」で、再生中に変えられる。
/// </summary>
public class SegmentedMoleVisual : MonoBehaviour
{
    public static SegmentedMoleVisual Instance { get; private set; }

    [Tooltip("オン: 体が曲がる(試作)。オフ: 元の1枚絵")]
    public bool bendEnabled = true;
    [Range(2, 8)]
    [Tooltip("体を切る数。多いほどなめらかに曲がる(ドットの切れ目は増える)")]
    public int segmentCount = 4;
    [Range(0, 3)]
    [Tooltip("切れ目の重なり(画素)。曲がったときのすき間を、うめる")]
    public int overlapPx = 1;
    [Tooltip("頭の向きが、進む向きに追いつく速さ。大きいほど、すぐ向く")]
    public float turnResponse = 14f;
    [Tooltip("向きを調べるときの、前後の幅(ユニット)。大きいほど、体がゆるやかに曲がる")]
    public float tangentSpan = 0.25f;

    Transform origVisual;
    SpriteRenderer origSr;
    readonly List<Transform> pieces = new List<Transform>();
    readonly List<SpriteRenderer> pieceSrs = new List<SpriteRenderer>();
    readonly List<Sprite> pieceSprites = new List<Sprite>();
    readonly List<float> pieceOx = new List<float>();   // 体の中心から、前へのずれ(ユニット。1枚絵のとき)
    float pieceOy;
    int builtCount = -1, builtOverlap = -1;
    Sprite builtFrom;
    float bodyLengthUnits = 1f;

    readonly List<Vector2> trail = new List<Vector2>(); // 古い → 新しい
    Vector2 lastPos;
    bool hasLast;
    Vector2 heading = Vector2.right;
    int facing = 1;

    void Awake() { Instance = this; }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        ClearPieces();
    }

    void LateUpdate()
    {
        if (origVisual == null)
        {
            var v = transform.Find("Visual");
            if (v == null) return;
            origVisual = v;
            origSr = v.GetComponent<SpriteRenderer>();
        }
        if (origSr == null || origSr.sprite == null) return;

        if (!bendEnabled)
        {
            origSr.enabled = true;
            SetPiecesActive(false);
            hasLast = false;
            return;
        }

        if (builtFrom != origSr.sprite || builtCount != segmentCount || builtOverlap != overlapPx) BuildPieces();
        if (pieces.Count == 0) return;

        origSr.enabled = false;
        SetPiecesActive(true);

        float scale = Mathf.Abs(origVisual.localScale.y);
        if (scale < 0.01f) scale = 1f;
        Vector2 c = transform.position;

        TrackMotion(c, scale);

        for (int k = 0; k < pieces.Count; k++)
        {
            float s = -pieceOx[k] * scale; // 正: 後ろ / 負: 前
            Vector2 p = SampleBehind(c, s);
            Vector2 a = SampleBehind(c, s + tangentSpan);
            Vector2 b = SampleBehind(c, s - tangentSpan);
            Vector2 t = b - a;
            if (t.sqrMagnitude < 1e-6f) t = heading; else t.Normalize();

            float ang = Mathf.Atan2(t.y, t.x) * Mathf.Rad2Deg;
            if (facing < 0) ang -= 180f; // 左向きは、絵を左右反転しているので、半回転ぶんずらす
            Quaternion rot = Quaternion.Euler(0f, 0f, ang);

            Vector3 off = rot * new Vector3(0f, pieceOy * scale, 0f);
            pieces[k].position = new Vector3(p.x + off.x, p.y + off.y, transform.position.z);
            pieces[k].rotation = rot;
            pieces[k].localScale = new Vector3(facing * scale, scale, 1f);
            pieceSrs[k].sortingOrder = origSr.sortingOrder + k; // 頭がいちばん手前
            pieceSrs[k].sortingLayerID = origSr.sortingLayerID;
            pieceSrs[k].color = origSr.color;
        }
    }

    // 進む向きと、通った道(軌跡)を、記録する
    void TrackMotion(Vector2 c, float scale)
    {
        if (!hasLast) { lastPos = c; trail.Clear(); hasLast = true; }
        Vector2 d = c - lastPos;
        float dm = d.magnitude;

        if (dm > 1.5f)
        {
            // ワープ(拠点・帰還など): 軌跡を捨てて、まっすぐな体にする
            trail.Clear();
        }
        else if (dm > 0.0005f)
        {
            Vector2 dir = d / dm;
            if (Mathf.Abs(dir.x) > 0.1f) facing = dir.x > 0f ? 1 : -1;
            float k = 1f - Mathf.Exp(-turnResponse * Mathf.Max(Time.deltaTime, 0.0001f));
            heading = Vector2.Lerp(heading, dir, k);
            if (heading.sqrMagnitude < 1e-6f) heading = dir; else heading.Normalize();
        }
        lastPos = c;

        // 軌跡に、0.04ユニットごとに、点を足す
        if (trail.Count == 0 || (trail[trail.Count - 1] - c).sqrMagnitude > 0.04f * 0.04f) trail.Add(c);

        // 体の長さ(+余裕)より古い点は、捨てる
        float maxLen = bodyLengthUnits * scale + 1f;
        float acc = 0f;
        Vector2 prev = c;
        int keepFrom = 0;
        for (int i = trail.Count - 1; i >= 0; i--)
        {
            acc += (trail[i] - prev).magnitude;
            prev = trail[i];
            if (acc > maxLen) { keepFrom = i; break; }
        }
        if (keepFrom > 0) trail.RemoveRange(0, keepFrom);
    }

    // 体の中心から、s ユニット後ろの、道の上の点。s が負なら、前(進む向きの延長)
    Vector2 SampleBehind(Vector2 c, float s)
    {
        if (s <= 0f) return c - heading * s;

        Vector2 prev = c;
        float remaining = s;
        Vector2 backDir = -heading;
        for (int i = trail.Count - 1; i >= 0; i--)
        {
            Vector2 seg = trail[i] - prev;
            float len = seg.magnitude;
            if (len < 1e-5f) continue;
            backDir = seg / len;
            if (len >= remaining) return prev + backDir * remaining;
            remaining -= len;
            prev = trail[i];
        }
        return prev + backDir * remaining; // 軌跡が足りないときは、まっすぐのばす
    }

    // 1枚絵を、縦に切る(左=しっぽ → 右=頭)
    void BuildPieces()
    {
        ClearPieces();
        var sprite = origSr.sprite;
        builtFrom = sprite;
        builtCount = segmentCount;
        builtOverlap = overlapPx;

        Rect rect = sprite.rect;
        float ppu = sprite.pixelsPerUnit;
        Vector2 pivotPx = sprite.pivot;
        int n = Mathf.Clamp(segmentCount, 2, 8);
        bodyLengthUnits = rect.width / ppu;

        for (int k = 0; k < n; k++)
        {
            float e0 = Mathf.Round(rect.x + rect.width * k / n);
            float e1 = Mathf.Round(rect.x + rect.width * (k + 1) / n);
            float x0 = k == 0 ? e0 : e0 - overlapPx; // しっぽ側へ、少し重ねる
            var sub = new Rect(x0, rect.y, e1 - x0, rect.height);
            var sp = Sprite.Create(sprite.texture, sub, new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
            sp.name = "MolePiece" + k;
            pieceSprites.Add(sp);

            var go = new GameObject("Piece" + k);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sp;
            pieces.Add(go.transform);
            pieceSrs.Add(sr);

            pieceOx.Add(((x0 + e1) * 0.5f - (rect.x + pivotPx.x)) / ppu);
            pieceOy = ((rect.y + rect.height * 0.5f) - (rect.y + pivotPx.y)) / ppu;
        }
        trail.Clear();
        hasLast = false;
    }

    void ClearPieces()
    {
        for (int i = 0; i < pieces.Count; i++) if (pieces[i] != null) Destroy(pieces[i].gameObject);
        for (int i = 0; i < pieceSprites.Count; i++) if (pieceSprites[i] != null) Destroy(pieceSprites[i]);
        pieces.Clear(); pieceSrs.Clear(); pieceSprites.Clear(); pieceOx.Clear();
        builtFrom = null; builtCount = -1; builtOverlap = -1;
    }

    void SetPiecesActive(bool on)
    {
        for (int i = 0; i < pieces.Count; i++) if (pieces[i] != null && pieces[i].gameObject.activeSelf != on) pieces[i].gameObject.SetActive(on);
    }
}
