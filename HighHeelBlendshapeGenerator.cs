using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Humanoid アバターの SkinnedMeshRenderer にハイヒール用 Blendshape を追加するエディタ拡張
/// </summary>
public class HighHeelBlendshapeGenerator : EditorWindow
{
    // ---- GUI フィールド ----
    private GameObject _avatarRoot;
    private Animator _detectedAnimator;
    private SkinnedMeshRenderer _targetSMR;

    // Foot / Toes のローカル回転オフセット（左右同時）
    private Vector3 _footRotation = new Vector3(30f, 0f, 0f);
    private Vector3 _toeRotation  = new Vector3(-30f, 0f, 0f);
    private bool _generateToeBlendshape = true;

    private string _blendshapeNameFoot = "HighHeel_Foot";
    private string _blendshapeNameToe  = "HighHeel_ToeBase";

    private Vector2 _scroll;
    private Vector2 _blendshapeScroll;

    private static readonly string[] PREFERRED_MESH_NAMES =
    {
        "Body_base", "body_2", "Body"
    };
    private const float BLENDSHAPE_LIST_MAX_HEIGHT = 220f;

    [MenuItem("Tools/yarihcas1_lab/HighHeel Blendshape Generator(ハイヒール履かせ機)")]
    public static void ShowWindow()
    {
        var win = GetWindow<HighHeelBlendshapeGenerator>("HighHeel Blendshape");
        win.minSize = new Vector2(420, 560);
    }

    // =========================================================
    //  GUI
    // =========================================================
    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.Space(6);
        GUILayout.Label("HighHeel Blendshape Generator", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Humanoid Avatar から Foot / Toes ボーンを取得し、実際に回転させて BakeMesh の差分から Blendshape を生成します。\n" +
            "Humanoid Avatar bone mapping is used for Foot / Toes. The tool rotates the bones and captures BakeMesh deltas to generate blendshapes.\n\n" +
            "対象メッシュは自動選択されますが、衣装など別の SkinnedMeshRenderer を指定できます。\n" +
            "The target mesh is selected automatically, but you can choose another SkinnedMeshRenderer such as clothing.\n\n" +
            "※ アバターは Scene 上に配置され、Humanoid Avatar が設定されている必要があります。\n" +
            "* The avatar must be placed in the Scene and configured as a Humanoid Avatar.",
            MessageType.Info);

        EditorGUILayout.Space(8);

        // ---- ターゲット ----
        EditorGUILayout.LabelField("■ ターゲット / Target", EditorStyles.boldLabel);
        var prevAvatar = _avatarRoot;
        _avatarRoot = (GameObject)EditorGUILayout.ObjectField(
            "アバター / Avatar", _avatarRoot, typeof(GameObject), true);

        if (_avatarRoot != prevAvatar)
            DetectComponents();

        var prevTargetSMR = _targetSMR;
        _targetSMR = (SkinnedMeshRenderer)EditorGUILayout.ObjectField(
            "対象メッシュ / Target Mesh", _targetSMR, typeof(SkinnedMeshRenderer), true);

        if (_targetSMR != null && _targetSMR != prevTargetSMR)
            DetectAvatarFromTargetSMR();

        EditorGUILayout.Space(4);
        using (new EditorGUI.IndentLevelScope(1))
        {
            bool isHumanoid = _detectedAnimator != null && _detectedAnimator.isHuman;
            var animatorStyle = isHumanoid ? EditorStyles.miniLabel : GetRedMiniLabel();
            string animatorText = isHumanoid
                ? "Humanoid Animator : " + GetPath(_avatarRoot != null ? _avatarRoot.transform : null, _detectedAnimator.transform)
                : "Humanoid Animator : 見つかりません / Not found";
            EditorGUILayout.LabelField(animatorText, animatorStyle);

            var meshStyle = _targetSMR != null && _targetSMR.sharedMesh != null
                ? EditorStyles.miniLabel : GetRedMiniLabel();
            string meshText = _targetSMR != null && _targetSMR.sharedMesh != null
                ? "Target Mesh : " + GetPath(_avatarRoot != null ? _avatarRoot.transform : null, _targetSMR.transform)
                : "Target Mesh : 見つかりません / Not found";
            EditorGUILayout.LabelField(meshText, meshStyle);
        }

        EditorGUILayout.Space(8);

        // ---- 回転設定 ----
        EditorGUILayout.LabelField("■ 回転設定（左右同時）/ Rotation Settings (Both Sides)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "リグごとにローカル軸の向きが異なるため、必要に応じて XYZ を調整してください。\n" +
            "Local axes differ by rig. Adjust XYZ values as needed.",
            MessageType.None);
        _footRotation = EditorGUILayout.Vector3Field("Foot 回転 / Foot Rotation", _footRotation);

        bool hasToes = HasBothToes();
        bool targetUsesToes = TargetUsesBothToes();
        using (new EditorGUI.DisabledScope(!hasToes))
            _generateToeBlendshape = EditorGUILayout.ToggleLeft(
                "Toes を生成 / Generate Toes", _generateToeBlendshape);
        using (new EditorGUI.DisabledScope(!hasToes || (_targetSMR != null && !targetUsesToes)))
            _toeRotation = EditorGUILayout.Vector3Field("Toes 回転 / Toes Rotation", _toeRotation);
        if (!hasToes)
            EditorGUILayout.HelpBox(
                "Toes ボーンが両側にないため、Foot Blendshape のみ生成します。\n" +
                "Both Toes bones were not found, so only the Foot blendshape will be generated.",
                MessageType.Warning);
        else if (_targetSMR != null && !targetUsesToes)
            EditorGUILayout.HelpBox(
                "対象メッシュは Toes ボーンを使用していないため、Toes Blendshape を生成できません。\n" +
                "The target mesh does not use the Toes bones, so a Toes blendshape cannot be generated.",
                MessageType.Warning);

        EditorGUILayout.Space(8);

        // ---- Blendshape 名 ----
        EditorGUILayout.LabelField("■ Blendshape 名 / Blendshape Names", EditorStyles.boldLabel);
        _blendshapeNameFoot = EditorGUILayout.TextField("Foot 用 / For Foot", _blendshapeNameFoot);
        using (new EditorGUI.DisabledScope(!_generateToeBlendshape || !hasToes))
            _blendshapeNameToe = EditorGUILayout.TextField("ToeBase 用 / For ToeBase", _blendshapeNameToe);

        EditorGUILayout.Space(12);

        // ---- 既存 Blendshape 一覧 ----
        if (_targetSMR != null && _targetSMR.sharedMesh != null)
        {
            var mesh = _targetSMR.sharedMesh;
            EditorGUILayout.LabelField($"■ 既存 Blendshape ({mesh.blendShapeCount} 個 / items)", EditorStyles.boldLabel);

            float listHeight = Mathf.Min(
                mesh.blendShapeCount * (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing),
                BLENDSHAPE_LIST_MAX_HEIGHT);
            _blendshapeScroll = EditorGUILayout.BeginScrollView(
                _blendshapeScroll, GUILayout.Height(Mathf.Max(listHeight, EditorGUIUtility.singleLineHeight)));
            for (int i = 0; i < mesh.blendShapeCount; i++)
                EditorGUILayout.LabelField($"  [{i}] {mesh.GetBlendShapeName(i)}", EditorStyles.miniLabel);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.Space(8);
        }

        // ---- Blendshape 生成 ----
        EditorGUILayout.LabelField("■ Blendshape 生成 / Generate", EditorStyles.boldLabel);
        bool canGenerate = CanGenerate();
        GUI.enabled = canGenerate;
        if (GUILayout.Button("Blendshape を生成して保存 / Generate & Save", GUILayout.Height(36)))
            Generate();
        GUI.enabled = true;

        EditorGUILayout.EndScrollView();
    }

    // =========================================================
    //  自動検出
    // =========================================================
    private void DetectComponents()
    {
        _detectedAnimator = null;
        _targetSMR = null;
        if (_avatarRoot == null) return;

        foreach (var animator in _avatarRoot.GetComponentsInChildren<Animator>(true))
        {
            if (animator.isHuman)
            {
                _detectedAnimator = animator;
                break;
            }
        }

        if (_detectedAnimator == null) return;

        foreach (var meshName in PREFERRED_MESH_NAMES)
        {
            foreach (var smr in _avatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.name == meshName)
                {
                    _targetSMR = smr;
                    return;
                }
            }
        }

        Transform leftFoot = GetHumanoidBone(HumanBodyBones.LeftFoot);
        Transform rightFoot = GetHumanoidBone(HumanBodyBones.RightFoot);
        foreach (var smr in _avatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (RendererUsesBone(smr, leftFoot) && RendererUsesBone(smr, rightFoot))
            {
                _targetSMR = smr;
                return;
            }
        }
    }

    /// <summary>
    /// 対象メッシュの親から Humanoid Animator を探してアバターに設定する
    /// </summary>
    private void DetectAvatarFromTargetSMR()
    {
        _avatarRoot = null;
        _detectedAnimator = null;
        if (_targetSMR == null) return;

        foreach (var animator in _targetSMR.GetComponentsInParent<Animator>(true))
        {
            if (animator.isHuman)
            {
                _avatarRoot = animator.gameObject;
                _detectedAnimator = animator;
                return;
            }
        }
    }

    // =========================================================
    //  生成メイン
    // =========================================================
    private void Generate()
    {
        if (!CanGenerate())
        {
            EditorUtility.DisplayDialog(
                "エラー / Error",
                "Humanoid Avatar、対象メッシュ、Foot ボーンを確認してください。\n" +
                "Please check the Humanoid Avatar, target mesh, and Foot bones.", "OK");
            return;
        }

        var smr = _targetSMR;
        var mesh = smr.sharedMesh;
        Transform footL = GetHumanoidBone(HumanBodyBones.LeftFoot);
        Transform footR = GetHumanoidBone(HumanBodyBones.RightFoot);
        Transform toesL = GetHumanoidBone(HumanBodyBones.LeftToes);
        Transform toesR = GetHumanoidBone(HumanBodyBones.RightToes);
        bool generateToes = _generateToeBlendshape && toesL != null && toesR != null;

        var newNames = new HashSet<string> { _blendshapeNameFoot };
        if (generateToes) newNames.Add(_blendshapeNameToe);

        if (newNames.Count != (generateToes ? 2 : 1))
        {
            EditorUtility.DisplayDialog(
                "エラー / Error",
                "Blendshape 名が重複しています。\nBlendshape names must be unique.", "OK");
            return;
        }

        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            string name = mesh.GetBlendShapeName(i);
            if (newNames.Contains(name))
            {
                if (!EditorUtility.DisplayDialog(
                    "上書き確認 / Overwrite Confirmation",
                    $"'{name}' はすでに存在します。\n同名の Blendshape を削除して再生成しますか？\n\n" +
                    $"'{name}' already exists.\nDelete and regenerate the blendshape with the same name?",
                    "上書き / Overwrite", "キャンセル / Cancel"))
                    return;
                break;
            }
        }

        var originalRotations = new Dictionary<Transform, Quaternion>
        {
            { footL, footL.localRotation },
            { footR, footR.localRotation }
        };
        if (generateToes)
        {
            originalRotations.Add(toesL, toesL.localRotation);
            originalRotations.Add(toesR, toesR.localRotation);
        }

        var tempMesh = new Mesh();
        Mesh newMesh = null;
        float[] savedWeights = null;
        bool createdAsset = false;

        try
        {
            // ---- 既存 Blendshape weight を退避・ゼロ化 ----
            int bsCount = mesh.blendShapeCount;
            savedWeights = new float[bsCount];
            for (int i = 0; i < bsCount; i++)
            {
                savedWeights[i] = smr.GetBlendShapeWeight(i);
                smr.SetBlendShapeWeight(i, 0f);
            }

            smr.BakeMesh(tempMesh);
            var baseVerts = GetBakedVertices(tempMesh);

            // ---- Foot 左右同時回転 → Bake ----
            footL.localRotation = originalRotations[footL] * Quaternion.Euler(_footRotation);
            footR.localRotation = originalRotations[footR] * Quaternion.Euler(_footRotation);
            smr.BakeMesh(tempMesh);
            var bakedFoot = GetBakedVertices(tempMesh);
            footL.localRotation = originalRotations[footL];
            footR.localRotation = originalRotations[footR];

            Vector3[] bakedToe = null;
            if (generateToes)
            {
                // ---- Toes 左右同時回転 → Bake ----
                toesL.localRotation = originalRotations[toesL] * Quaternion.Euler(_toeRotation);
                toesR.localRotation = originalRotations[toesR] * Quaternion.Euler(_toeRotation);
                smr.BakeMesh(tempMesh);
                bakedToe = GetBakedVertices(tempMesh);
                toesL.localRotation = originalRotations[toesL];
                toesR.localRotation = originalRotations[toesR];
            }

            var deltaFoot = CalculateDelta(baseVerts, bakedFoot);
            var zeroN = new Vector3[mesh.vertexCount];
            var zeroT = new Vector3[mesh.vertexCount];

            // ---- 新規 Mesh に Blendshape を追加 ----
            newMesh = DuplicateMeshWithoutBlendshapes(mesh, newNames);
            newMesh.AddBlendShapeFrame(_blendshapeNameFoot, 100f, deltaFoot, zeroN, zeroT);
            if (generateToes)
                newMesh.AddBlendShapeFrame(_blendshapeNameToe, 100f,
                    CalculateDelta(baseVerts, bakedToe), zeroN, zeroT);

            // ---- 保存 ----
            string defaultName = mesh.name + "_HighHeel";
            string defaultDir = "Assets";
            string srcPath = AssetDatabase.GetAssetPath(mesh);
            if (!string.IsNullOrEmpty(srcPath))
                defaultDir = System.IO.Path.GetDirectoryName(srcPath);

            string assetPath = EditorUtility.SaveFilePanelInProject(
                "新規 Mesh を保存 / Save New Mesh", defaultName, "asset",
                "保存先を選択してください / Choose save location", defaultDir);
            if (string.IsNullOrEmpty(assetPath)) return;

            newMesh.name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(newMesh, assetPath);
            createdAsset = true;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            smr.sharedMesh = newMesh;
            EditorUtility.SetDirty(smr);

            string toesResult = generateToes
                ? $"\n  {_blendshapeNameToe}  (Toes / both {_toeRotation})"
                : string.Empty;
            EditorUtility.DisplayDialog(
                "完了 / Done",
                "新規 Mesh を生成し Blendshape を追加しました。\n" +
                "New mesh generated with blendshapes added.\n\n" +
                $"  {_blendshapeNameFoot}  (Foot / both {_footRotation})" + toesResult +
                $"\n\n保存先 / Saved to: {assetPath}", "OK");
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("エラー / Error", e.Message, "OK");
            throw;
        }
        finally
        {
            // ---- 回転と weight を必ず復元 ----
            foreach (var pair in originalRotations)
                pair.Key.localRotation = pair.Value;
            if (savedWeights != null)
            {
                for (int i = 0; i < savedWeights.Length; i++)
                    smr.SetBlendShapeWeight(i, savedWeights[i]);
            }
            if (tempMesh != null) DestroyImmediate(tempMesh);
            if (newMesh != null && !createdAsset) DestroyImmediate(newMesh);
        }

        Repaint();
    }

    // =========================================================
    //  ユーティリティ
    // =========================================================
    private bool CanGenerate()
    {
        if (_detectedAnimator == null || !_detectedAnimator.isHuman || _targetSMR == null || _targetSMR.sharedMesh == null)
            return false;
        if (string.IsNullOrEmpty(_blendshapeNameFoot)) return false;
        if (_generateToeBlendshape && HasBothToes() && string.IsNullOrEmpty(_blendshapeNameToe)) return false;

        Transform footL = GetHumanoidBone(HumanBodyBones.LeftFoot);
        Transform footR = GetHumanoidBone(HumanBodyBones.RightFoot);
        if (!RendererUsesBone(_targetSMR, footL) || !RendererUsesBone(_targetSMR, footR)) return false;
        return !_generateToeBlendshape || !HasBothToes() || TargetUsesBothToes();
    }

    private bool HasBothToes()
    {
        if (_detectedAnimator == null || !_detectedAnimator.isHuman) return false;
        return GetHumanoidBone(HumanBodyBones.LeftToes) != null
               && GetHumanoidBone(HumanBodyBones.RightToes) != null;
    }

    private bool TargetUsesBothToes()
    {
        return RendererUsesBone(_targetSMR, GetHumanoidBone(HumanBodyBones.LeftToes))
               && RendererUsesBone(_targetSMR, GetHumanoidBone(HumanBodyBones.RightToes));
    }

    private Transform GetHumanoidBone(HumanBodyBones bone)
    {
        return _detectedAnimator != null && _detectedAnimator.isHuman
            ? _detectedAnimator.GetBoneTransform(bone) : null;
    }

    /// <summary>
    /// 指定 Transform が SMR の bones 配列に含まれるかを返す
    /// </summary>
    private static bool RendererUsesBone(SkinnedMeshRenderer smr, Transform bone)
    {
        if (smr == null || bone == null) return false;
        foreach (var rendererBone in smr.bones)
            if (rendererBone == bone) return true;
        return false;
    }

    /// <summary>
    /// BakeMesh で得た Renderer ローカル座標の頂点配列をコピーして返す
    /// </summary>
    private static Vector3[] GetBakedVertices(Mesh bakedMesh)
    {
        var vertices = bakedMesh.vertices;
        var copy = new Vector3[vertices.Length];
        System.Array.Copy(vertices, copy, vertices.Length);
        return copy;
    }

    private static Vector3[] CalculateDelta(Vector3[] baseVerts, Vector3[] bakedVerts)
    {
        if (baseVerts == null || bakedVerts == null || baseVerts.Length != bakedVerts.Length)
            throw new System.InvalidOperationException("BakeMesh vertex counts do not match.");

        var delta = new Vector3[baseVerts.Length];
        for (int i = 0; i < delta.Length; i++)
            delta[i] = bakedVerts[i] - baseVerts[i];
        return delta;
    }

    /// <summary>
    /// 既存 Blendshape を全コピーしつつ、指定名の Blendshape だけ除いた新しい Mesh を返す
    /// </summary>
    private static Mesh DuplicateMeshWithoutBlendshapes(Mesh src, HashSet<string> excludeNames)
    {
        var dst = Object.Instantiate(src);
        dst.name = src.name;
        dst.ClearBlendShapes();

        int count = src.blendShapeCount;
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

    /// <summary>
    /// アバタールートからの相対パスを返す。ルート外の Transform にも対応する
    /// </summary>
    private static string GetPath(Transform root, Transform target)
    {
        if (target == null) return "—";
        if (root == null) return target.name;
        if (target == root) return root.name;

        var parts = new Stack<string>();
        var current = target;
        while (current != null && current != root)
        {
            parts.Push(current.name);
            current = current.parent;
        }
        return current == root ? string.Join("/", parts) : target.name;
    }

    /// <summary>
    /// 赤文字用スタイル
    /// </summary>
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
}
