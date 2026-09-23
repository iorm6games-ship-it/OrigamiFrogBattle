using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Analytics;

public sealed class CrystalPieceLayoutLogger : MonoBehaviour
{
    [Header("Optional")]
    [Tooltip("未設定ならこのGameObject自身をRootとして使用する")]
    [SerializeField]
    private Transform crystalRoot;

    [ContextMenu("Log Crystal Piece Layout")]
    private void LogCrystalPieceLayout()
    {
        Transform root = crystalRoot != null
            ? crystalRoot
            : transform;
        
        List<Transform> pieces = new ();

        // CrystalAssemblyRoot 直下の16片だけを対象にする
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);

            if (!child.name.StartsWith("Crystal_"))
                continue;
            if (child.name == "Cryslta_Final")
                continue;
            
            pieces.Add(child);
        }

        StringBuilder sb = new();

        sb.AppendLine("===== Crysltal Piece Layout =====");
        sb.AppendLine($"Root : {root.name}");
        sb.AppendLine($"Count : {pieces.Count}");
        sb.AppendLine();

        foreach (Transform piece in pieces)
        {
            Vector3 pivot = piece.localPosition;

            if (TryGetMeshBoundsInRootSpace(piece, root, out Bounds bounds))
            {
                sb.AppendLine(
                    $"{piece.name}" +
                    $" | Pivot {Format(pivot)}" +
                    $" | Center {Format(bounds.center)}" +
                    $" | Min {Format(bounds.min)}" +
                    $" | Max {Format(bounds.max)}");
            }
            else
            {
                sb.AppendLine(
                    $"{piece.name}" +
                    $" | Pivot {Format(pivot)}" +
                    " | MeshBounds: NONE");
            }
        }
        sb.AppendLine();
        sb.AppendLine("==============================");
        Debug.Log(sb.ToString(), root);
    }

    private static bool TryGetMeshBoundsInRootSpace(
        Transform piece,
        Transform root,
        out Bounds result)
    {
        MeshFilter meshFilter = piece.GetComponent<MeshFilter>();

        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            result = default;
            return false;
        }
        Bounds meshBounds = meshFilter.sharedMesh.bounds;

        Vector3 min = meshBounds.min;
        Vector3 max = meshBounds.max;

        Vector3[] corners =
        {
            new(min.x, min.y, min.z),
            new(max.x, min.y, min.z),
            new(min.x, max.y, min.z),
            new(max.x, max.y, min.z),
            new(min.x, min.y, max.z),
            new(max.x, min.y, max.z),
            new(min.x, max.y, max.z),
            new(max.x, max.y, max.z)
        };

        Vector3 first =
            root.InverseTransformPoint(
                piece.TransformPoint(corners[0]));
        
        result = new Bounds(first, Vector3.zero);

        for (int i = 1; i < corners.Length; i++)
        {
            Vector3 rootLocalPoint =
                root.InverseTransformPoint(
                    piece.TransformPoint(corners[i]));
            
            result.Encapsulate(rootLocalPoint);
        }
        return true;
    }

    private static string Format(Vector3 value)
    {
        return
            $"({value.x:F6}, {value.y:F6}, {value.z:F6})";
    }
}