using UnityEngine;

/// <summary>
/// 地層1つ分の設定。DirtTerrain の Layers に並べる。
/// 深い順(startDepth が大きい順)に、上から下へ並べること。
/// </summary>
[System.Serializable]
public class SoilLayer
{
    [Tooltip("層の名前(画面左上の表示に使う。英数字推奨)")]
    public string name = "Layer";

    [Tooltip("土の上端からの深さ(ユニット)。この深さからこの層が始まる")]
    public float startDepth = 0f;

    [Tooltip("層の上側の色")]
    public Color32 colorLight = new Color32(133, 86, 46, 255);
    [Tooltip("層の下側の色(層の中でだんだん変わる)")]
    public Color32 colorDark = new Color32(110, 70, 38, 255);

    [Tooltip("硬さ。1=基準。大きいほど、掘る速さが落ち、スタミナを多く使う")]
    public float hardness = 1f;

    [Tooltip("土の中の粒(暗い点・明るい点)の多さ。1=基準")]
    public float speckle = 1f;

    public SoilLayer() { }

    public SoilLayer(string name, float startDepth, Color32 light, Color32 dark, float hardness, float speckle)
    {
        this.name = name;
        this.startDepth = startDepth;
        this.colorLight = light;
        this.colorDark = dark;
        this.hardness = hardness;
        this.speckle = speckle;
    }
}
