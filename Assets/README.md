# モグラ プロジェクト フォルダ案内

番号の小さい順に、ゲームの中心から並べています。

## 00_Scenes … シーン
- SampleScene … ゲーム本体のシーン(いまはこれ1つ)

## 01_Scripts … スクリプト(役割ごとに分けています)
| フォルダ | 中身 | 主なファイル |
|---|---|---|
| Mole | モグラ本人の動き・入力・見た目・カメラ | MoleController, MoleInput, MoleThought, SegmentedMoleVisual, CameraFollow |
| World | 地面・地層・地上・暗闇・探知・大木 | DirtTerrain, SoilLayer, TerrainRepair, SurfaceArea, DarknessOverlay, GiantTree, ScanPulse, Detectable |
| Items | 地中のアイテムと配置 | ItemSpawner, ItemPickup, Obstacle |
| Emotion | 感情のかけら・塵・効果 | Emotion, EmotionDust, EmotionEffects, ShardInventory, DustSprites, ShardSprites |
| Quest | クエスト・拠点・メニュー | QuestManager, BaseManager, BaseMenu |
| UI | 画面に出す表示 | StaminaHud, EmotionHud, QuestHud, UiText |
| Debug | 開発用(F2のデバッグパネル) | DebugPanel |

## 02_Art … 絵
- Sprites/Mole … モグラの画像(mogura.png)
- アイテム・塵・かけら・大木の絵は、いまはコードで描いた仮画像です。
  本番の絵は、各スクリプトの Sprite 欄(Inspector)に入れると差し替わります。

## Settings / 直下の .asset ・ .inputactions
- Unity(URP・入力)が自動で作ったものです。触らないでください。

## プロジェクトの外(Assets の外)
- _Docs/Screenshots … 確認用のスクリーンショット(ゲームには入りません)
- _Backup … 整理前のバックアップ
