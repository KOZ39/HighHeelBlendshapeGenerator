# HighHeel Blendshape Generator

VRoid製VRChatアバターのBodyメッシュに、ハイヒール用のBlendshapeを自動生成するUnityエディタ拡張です。

## 概要

ハイヒールを履かせる際に必要な「足首の持ち上げ」と「つま先の補正」を、Blendshapeとして Bodyメッシュに追加します。  
アーマチュアのボーンを実際に回転させて `BakeMesh` で変形後の頂点座標を取得し、その差分をBlendshapeのデルタとして記録するため、スキンウェイトを忠実に反映した正確な変形が得られます。

| Blendshape 名 | 対象ボーン | 内容 |
|---|---|---|
| `HighHeel_Foot` | `J_Bip_L_Foot` / `J_Bip_R_Foot` | 足首の角度（つま先立ち） |
| `HighHeel_ToeBase` | `J_Bip_L_ToeBase` / `J_Bip_R_ToeBase` | つま先の補正 |

## 動作環境

- Unity 2019.4 以降
- VRoid Studio で出力したアバター（VRM → Unity インポート済み）
- VRChat SDK（VRCSDK3-AVATAR）

## インストール

`HighHeelBlendshapeGenerator.cs` を Unity プロジェクトの `Assets/Editor/` フォルダに配置してください。

```
Assets/
└── Editor/
    └── HighHeelBlendshapeGenerator.cs
```

## 使い方

### 1. ウィンドウを開く

Unity メニューバーから **Tools > HighHeel Blendshape Generator** を選択します。

### 2. アバターを指定する

**アバター (GameObject)** フィールドに、Sceneビュー上に配置済みのアバターのルートオブジェクトをドラッグ＆ドロップします。

指定すると自動的に以下を検出し、結果をウィンドウ内に表示します。

- `Body` という名前の `SkinnedMeshRenderer`
- `Root` という名前の `Transform`

検出に失敗した場合は赤文字で表示されます。

### 3. 角度を設定する

| スライダー | 説明 | 推奨範囲 |
|---|---|---|
| **Foot Rotation X（両足）** | 足首の持ち上げ角度 | 20° 〜 35° |
| **ToeBase Rotation X（両足）** | つま先の補正角度 | -15° 〜 -25° |

左右は常に同じ角度で生成されます。

### 4. Blendshape名を確認する

デフォルトは `HighHeel_Foot` / `HighHeel_ToeBase` です。必要に応じて変更してください。

### 5. 生成・保存する

**「Blendshape を生成して保存」** ボタンを押すと保存先ダイアログが表示されます。  
任意の場所に `.asset` ファイルとして保存すると、自動的に SkinnedMeshRenderer の `sharedMesh` が新しいメッシュに差し替えられます。

> **元のメッシュアセットは変更されません。** 新規の `.asset` ファイルが作成されます。

## 技術仕様

### Blendshape 生成フロー

```
① 既存 Blendshape weight をすべて 0 に退避
   → BakeMesh でベース頂点座標を取得

② J_Bip_L/R_Foot を左右同時に Rotation X 回転
   → BakeMesh で変形後頂点座標を取得
   → ボーンを元の角度に戻す

③ J_Bip_L/R_ToeBase を左右同時に Rotation X 回転
   → BakeMesh で変形後頂点座標を取得
   → ボーンを元の角度に戻す

④ Blendshape weight を復元

⑤ delta = (変形後) - (ベース) をそれぞれ Blendshape として登録

⑥ 新規 Mesh アセットとして保存
   SkinnedMeshRenderer.sharedMesh を差し替え
```

### 自動検出ロジック

アバター GameObject 以下を `GetComponentsInChildren` で走査し、以下を検出します。

- 名前が `"Body"` の `SkinnedMeshRenderer` → Blendshape 追加対象
- 名前が `"Root"` の `Transform` → 検出確認用（表示のみ）

ボーンの取得は `SkinnedMeshRenderer.bones` から直接行うため、VRoid 標準の命名規則（`J_Bip_*`）に依存します。

### 安全性への配慮

- 処理中に例外が発生した場合でも、`try/catch/finally` によりボーンは必ず元の `localRotation` に復元されます
- 既存の同名 Blendshape が存在する場合は上書き確認ダイアログを表示します
- 元のメッシュアセットへの書き込みは一切行いません

## 注意事項

- アバターは **Scene ビューに配置されたインスタンス**である必要があります（Project ウィンドウの Prefab アセットは不可）
- VRChat Avatar Descriptor の **Foot IK** を使用する場合、ハイヒールシューズの高さに合わせて別途 IK の設定が必要です
- 法線・タンジェントのデルタはゼロとして登録しています。シェーダーによっては法線補正が必要になる場合があります

## ライセンス

MIT License
