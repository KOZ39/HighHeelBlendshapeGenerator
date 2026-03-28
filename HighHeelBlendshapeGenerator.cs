using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>
/// VRoid製VRChatアバターのBodyメッシュにハイヒール用Blendshapeを追加するエディタ拡張
/// Tools > yarihcas1_lab > for VRoid > HighHeel Blendshape Generator から開く
/// </summary>
public class HighHeelBlendshapeGenerator : EditorWindow
{
    // ---- GUI フィールド ----
    private GameObject _avatarRoot;

    // 自動検出結果（GUI表示用）
    private SkinnedMeshRenderer _detectedSMR;
    private Transform           _detectedRoot;

    // Foot（左右同時）・ToeBase（左右同時）それぞれの角度
    private float _footAngle = 30f;    // J_Bip_L/R_Foot    Rotation X (deg)
    private float _toeAngle  = -30f;   // J_Bip_L/R_ToeBase Rotation X (deg)

    private string _blendshapeNameFoot = "HighHeel_Foot";
    private string _blendshapeNameToe  = "HighHeel_ToeBase";

    private Vector2 _scroll;

    // ---- VRoid 標準ボーン名 ----
    private const string BONE_FOOT_L    = "J_Bip_L_Foot";
    private const string BONE_FOOT_R    = "J_Bip_R_Foot";
    private const string BONE_TOE_L     = "J_Bip_L_ToeBase";
    private const string BONE_TOE_R     = "J_Bip_R_ToeBase";

    [MenuItem("Tools/yarihcas1_lab/for VRoid/HighHeel Blendshape Generator(ハイヒール履かせ機)")]
    public static void ShowWindow()
    {
        var win = GetWindow<HighHeelBlendshapeGenerator>("HighHeel Blendshape");
        win.minSize = new Vector2(400, 520);
    }

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.Space(6);
        GUILayout.Label("HighHeel Blendshape Generator", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "アバターを指定すると Body メッシュと Root ボーンを自動検出します。\n" +
            "Foot / ToeBase ボーンを実際に回転させて BakeMesh で差分を取得し、\n" +
            "左右同時の Blendshape を2つ生成します。\n" +
            "※ Scene ビュー上にアバターが配置されている必要があります。",
            MessageType.Info);

        EditorGUILayout.Space(8);

        // ---- アバター指定 ----
        EditorGUILayout.LabelField("■ ターゲット", EditorStyles.boldLabel);
        var prevAvatar = _avatarRoot;
        _avatarRoot = (GameObject)EditorGUILayout.ObjectField(
            "アバター (GameObject)", _avatarRoot, typeof(GameObject), true);

        // アバターが変わったら自動検出を実行
        if (_avatarRoot != prevAvatar)
            DetectComponents();

        // 検出結果の表示
        EditorGUILayout.Space(4);
        using (new EditorGUI.IndentLevelScope(1))
        {
            if (_avatarRoot == null)
            {
                EditorGUILayout.LabelField("Body SMR : —", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("Root     : —", EditorStyles.miniLabel);
            }
            else
            {
                // Body SMR
                var smrStyle = _detectedSMR != null ? EditorStyles.miniLabel : GetRedMiniLabel();
                string smrText = _detectedSMR != null
                    ? $"Body SMR : {GetPath(_avatarRoot.transform, _detectedSMR.transform)}"
                    : "Body SMR : 見つかりません";
                EditorGUILayout.LabelField(smrText, smrStyle);

                // Root
                var rootStyle = _detectedRoot != null ? EditorStyles.miniLabel : GetRedMiniLabel();
                string rootText = _detectedRoot != null
                    ? $"Root     : {GetPath(_avatarRoot.transform, _detectedRoot)}"
                    : "Root     : 見つかりません";
                EditorGUILayout.LabelField(rootText, rootStyle);
            }
        }

        EditorGUILayout.Space(8);

        // ---- 角度設定 ----
        EditorGUILayout.LabelField("■ 角度設定（左右同時）", EditorStyles.boldLabel);
        _footAngle = EditorGUILayout.Slider("Foot Rotation X（両足）",    _footAngle,  0f,  60f);
        _toeAngle  = EditorGUILayout.Slider("ToeBase Rotation X（両足）", _toeAngle,  -60f,  0f);

        EditorGUILayout.Space(8);

        // ---- Blendshape 名 ----
        EditorGUILayout.LabelField("■ Blendshape 名", EditorStyles.boldLabel);
        _blendshapeNameFoot = EditorGUILayout.TextField("Foot 用",    _blendshapeNameFoot);
        _blendshapeNameToe  = EditorGUILayout.TextField("ToeBase 用", _blendshapeNameToe);

        EditorGUILayout.Space(12);

        // ---- 既存 Blendshape 一覧 ----
        if (_detectedSMR != null && _detectedSMR.sharedMesh != null)
        {
            var mesh  = _detectedSMR.sharedMesh;
            int count = mesh.blendShapeCount;
            EditorGUILayout.LabelField($"■ 既存 Blendshape ({count} 個)", EditorStyles.boldLabel);
            for (int i = 0; i < count; i++)
                EditorGUILayout.LabelField($"  [{i}] {mesh.GetBlendShapeName(i)}", EditorStyles.miniLabel);
            EditorGUILayout.Space(8);
        }

        // ---- 実行ボタン ----
        GUI.enabled = _detectedSMR != null
                      && !string.IsNullOrEmpty(_blendshapeNameFoot)
                      && !string.IsNullOrEmpty(_blendshapeNameToe);

        if (GUILayout.Button("Blendshape を生成して保存", GUILayout.Height(36)))
            Generate();

        GUI.enabled = true;

        EditorGUILayout.EndScrollView();
    }

    // ---- 自動検出 ----
    private void DetectComponents()
    {
        _detectedSMR  = null;
        _detectedRoot = null;
        if (_avatarRoot == null) return;

        // Body: 名前が "Body" の SkinnedMeshRenderer を子孫から探す
        foreach (var smr in _avatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr.name == "Body")
            {
                _detectedSMR = smr;
                break;
            }
        }

        // Root: 名前が "Root" の Transform を子孫から探す
        foreach (Transform t in _avatarRoot.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Root")
            {
                _detectedRoot = t;
                break;
            }
        }
    }

    // パス表示用ヘルパー（アバタールートからの相対パス）
    private static string GetPath(Transform root, Transform target)
    {
        if (target == root) return root.name;
        var parts = new System.Collections.Generic.Stack<string>();
        var t = target;
        while (t != null && t != root)
        {
            parts.Push(t.name);
            t = t.parent;
        }
        return string.Join("/", parts);
    }

    // 赤文字用スタイル
    private static GUIStyle _redMiniLabel;
    private static GUIStyle GetRedMiniLabel()
    {
        if (_redMiniLabel == null)
        {
            _redMiniLabel = new GUIStyle(EditorStyles.miniLabel);
            _redMiniLabel.normal.textColor = new Color(0.9f, 0.2f, 0.2f);
        }
        return _redMiniLabel;
    }

    // =========================================================
    //  生成メイン
    // =========================================================
    private void Generate()
    {
        var smr  = _detectedSMR;
        var mesh = smr != null ? smr.sharedMesh : null;

        if (mesh == null)
        {
            EditorUtility.DisplayDialog("エラー", "Mesh が見つかりません。", "OK");
            return;
        }

        // 重複チェック
        var newNames = new HashSet<string> { _blendshapeNameFoot, _blendshapeNameToe };
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            string n = mesh.GetBlendShapeName(i);
            if (newNames.Contains(n))
            {
                if (!EditorUtility.DisplayDialog(
                    "上書き確認",
                    $"'{n}' はすでに存在します。\n同名の Blendshape を削除して再生成しますか？",
                    "上書き", "キャンセル"))
                    return;
                break;
            }
        }

        // ---- ボーン Transform を SMR.bones から取得 ----
        Transform tfFootL = FindBoneTransform(smr, BONE_FOOT_L);
        Transform tfFootR = FindBoneTransform(smr, BONE_FOOT_R);
        Transform tfToeL  = FindBoneTransform(smr, BONE_TOE_L);
        Transform tfToeR  = FindBoneTransform(smr, BONE_TOE_R);

        foreach ((Transform tf, string name) in new[]
        {
            (tfFootL, BONE_FOOT_L), (tfFootR, BONE_FOOT_R),
            (tfToeL,  BONE_TOE_L),  (tfToeR,  BONE_TOE_R)
        })
        {
            if (tf == null)
            {
                EditorUtility.DisplayDialog("エラー",
                    $"ボーン '{name}' が見つかりません。\n" +
                    "VRoid 標準のアバターを選択してください。", "OK");
                return;
            }
        }

        // ---- ベースポーズを退避 ----
        Quaternion origFootL = tfFootL.localRotation;
        Quaternion origFootR = tfFootR.localRotation;
        Quaternion origToeL  = tfToeL.localRotation;
        Quaternion origToeR  = tfToeR.localRotation;

        // BakeMesh はワールド空間で返るので SMR のワールド→ローカル行列を用意
        Matrix4x4 worldToLocal = smr.transform.worldToLocalMatrix;
        int vCount = mesh.vertexCount;

        var tempMesh = new Mesh();

        try
        {
            // ---- ① 既存 Blendshape weight を退避・ゼロ化してクリーンなベースをベイク ----
            int bsCount = smr.sharedMesh.blendShapeCount;
            var savedWeights = new float[bsCount];
            for (int i = 0; i < bsCount; i++)
            {
                savedWeights[i] = smr.GetBlendShapeWeight(i);
                smr.SetBlendShapeWeight(i, 0f);
            }

            smr.BakeMesh(tempMesh);
            var baseVerts = GetLocalVertices(tempMesh, worldToLocal);

            // ---- ② Foot 左右同時回転 → ベイク → 元に戻す ----
            tfFootL.localRotation = origFootL * Quaternion.Euler(_footAngle, 0f, 0f);
            tfFootR.localRotation = origFootR * Quaternion.Euler(_footAngle, 0f, 0f);

            smr.BakeMesh(tempMesh);
            var bakedFoot = GetLocalVertices(tempMesh, worldToLocal);

            tfFootL.localRotation = origFootL;
            tfFootR.localRotation = origFootR;

            // ---- ③ ToeBase 左右同時回転 → ベイク → 元に戻す ----
            tfToeL.localRotation = origToeL * Quaternion.Euler(_toeAngle, 0f, 0f);
            tfToeR.localRotation = origToeR * Quaternion.Euler(_toeAngle, 0f, 0f);

            smr.BakeMesh(tempMesh);
            var bakedToe = GetLocalVertices(tempMesh, worldToLocal);

            tfToeL.localRotation = origToeL;
            tfToeR.localRotation = origToeR;

            // Blendshape weight を復元
            for (int i = 0; i < bsCount; i++)
                smr.SetBlendShapeWeight(i, savedWeights[i]);

            // ---- ④ デルタ計算 ----
            var deltaFoot = new Vector3[vCount];
            var deltaToe  = new Vector3[vCount];
            var zeroN     = new Vector3[vCount];
            var zeroT     = new Vector3[vCount];

            for (int vi = 0; vi < vCount; vi++)
            {
                deltaFoot[vi] = bakedFoot[vi] - baseVerts[vi];
                deltaToe[vi]  = bakedToe[vi]  - baseVerts[vi];
            }

            // ---- ⑤ 新規 Mesh に Blendshape を追加 ----
            Mesh newMesh = DuplicateMeshWithoutBlendshapes(mesh, newNames);
            newMesh.AddBlendShapeFrame(_blendshapeNameFoot, 100f, deltaFoot, zeroN, zeroT);
            newMesh.AddBlendShapeFrame(_blendshapeNameToe,  100f, deltaToe,  zeroN, zeroT);

            // ---- ⑥ 保存 ----
            string defaultName = mesh.name + "_HighHeel";
            string defaultDir  = "Assets";
            string srcPath     = AssetDatabase.GetAssetPath(mesh);
            if (!string.IsNullOrEmpty(srcPath))
                defaultDir = System.IO.Path.GetDirectoryName(srcPath);

            string assetPath = EditorUtility.SaveFilePanelInProject(
                "新規 Mesh を保存", defaultName, "asset",
                "保存先を選択してください", defaultDir);

            if (string.IsNullOrEmpty(assetPath))
            {
                DestroyImmediate(newMesh);
                return;
            }

            newMesh.name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(newMesh, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            smr.sharedMesh = newMesh;
            EditorUtility.SetDirty(smr);

            EditorUtility.DisplayDialog("完了",
                $"新規 Mesh を生成し Blendshape を追加しました。\n\n" +
                $"  {_blendshapeNameFoot}  (Foot  両足 {_footAngle}°)\n" +
                $"  {_blendshapeNameToe}   (Toe   両足 {_toeAngle}°)\n\n" +
                $"保存先: {assetPath}",
                "OK");
        }
        catch (System.Exception e)
        {
            // 例外時も必ずボーンを元の状態に戻す
            tfFootL.localRotation = origFootL;
            tfFootR.localRotation = origFootR;
            tfToeL.localRotation  = origToeL;
            tfToeR.localRotation  = origToeR;
            EditorUtility.DisplayDialog("エラー", e.Message, "OK");
            throw;
        }
        finally
        {
            DestroyImmediate(tempMesh);
        }

        Repaint();
    }

    // =========================================================
    //  ユーティリティ
    // =========================================================

    /// <summary>
    /// SMR の bones 配列から名前でボーン Transform を探す
    /// </summary>
    private static Transform FindBoneTransform(SkinnedMeshRenderer smr, string boneName)
    {
        foreach (var bone in smr.bones)
            if (bone != null && bone.name == boneName)
                return bone;
        return null;
    }

    /// <summary>
    /// BakeMesh で得た Mesh の頂点をワールド→ローカル変換してローカル座標配列で返す
    /// </summary>
    private static Vector3[] GetLocalVertices(Mesh bakedMesh, Matrix4x4 worldToLocal)
    {
        var worldVerts = bakedMesh.vertices;
        var localVerts = new Vector3[worldVerts.Length];
        for (int i = 0; i < worldVerts.Length; i++)
            localVerts[i] = worldToLocal.MultiplyPoint3x4(worldVerts[i]);
        return localVerts;
    }

    /// <summary>
    /// 既存 Blendshape を全コピーしつつ、指定名の Blendshape だけ除いた新しい Mesh を返す
    /// </summary>
    private static Mesh DuplicateMeshWithoutBlendshapes(Mesh src, HashSet<string> excludeNames)
    {
        var dst = Object.Instantiate(src);
        dst.name = src.name;
        dst.ClearBlendShapes();

        int count  = src.blendShapeCount;
        var deltaV = new Vector3[src.vertexCount];
        var deltaN = new Vector3[src.vertexCount];
        var deltaT = new Vector3[src.vertexCount];

        for (int si = 0; si < count; si++)
        {
            string shapeName = src.GetBlendShapeName(si);
            if (excludeNames.Contains(shapeName)) continue;

            int frameCount = src.GetBlendShapeFrameCount(si);
            for (int fi = 0; fi < frameCount; fi++)
            {
                float weight = src.GetBlendShapeFrameWeight(si, fi);
                src.GetBlendShapeFrameVertices(si, fi, deltaV, deltaN, deltaT);
                dst.AddBlendShapeFrame(shapeName, weight, deltaV, deltaN, deltaT);
            }
        }

        return dst;
    }
}
