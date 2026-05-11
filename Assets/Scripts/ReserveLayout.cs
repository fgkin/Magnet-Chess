using System.Collections.Generic;
using UnityEngine;

public class ReserveLayout : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] private Vector3 localStartOffset = Vector3.zero;
    [SerializeField] private Vector3 localStepOffset = new Vector3(0f, 0f, 0.8f);

    [Header("Wrapping")]
    [SerializeField] private int maxPiecesPerLine = 8;
    [SerializeField] private float wrapSpacing = 0.8f;
    [SerializeField] private bool wrapAwayFromArena = true;
    [SerializeField] private Transform arenaCenter;

    [Header("Piece")]
    [SerializeField] private float pieceY = 0.15f;

    private readonly List<MagnetPiece> reservePieces = new();

    public void RegisterPiece(MagnetPiece piece)
    {
        if (piece == null)
            return;

        if (!reservePieces.Contains(piece))
            reservePieces.Add(piece);

        RefreshLayout();
    }

    public void UnregisterPiece(MagnetPiece piece)
    {
        if (piece == null)
            return;

        if (reservePieces.Contains(piece))
            reservePieces.Remove(piece);

        RefreshLayout();
    }

    public void RefreshLayout()
    {
        int visibleIndex = 0;
        int safeMaxPiecesPerLine = Mathf.Max(1, maxPiecesPerLine);

        Vector3 wrapDirection = GetWrapDirection();

        for (int i = 0; i < reservePieces.Count; i++)
        {
            MagnetPiece piece = reservePieces[i];

            if (piece == null)
                continue;

            if (piece.PieceState != MagnetPiece.State.Reserve)
                continue;

            int lineIndex = visibleIndex / safeMaxPiecesPerLine;
            int indexInLine = visibleIndex % safeMaxPiecesPerLine;

            Vector3 baseWorldPos = transform.TransformPoint(
                localStartOffset + localStepOffset * indexInLine
            );

            Vector3 worldPos = baseWorldPos + wrapDirection * wrapSpacing * lineIndex;
            worldPos.y = pieceY;

            piece.transform.position = worldPos;
            piece.SnapUpright();
            piece.SetPhysicsEnabled(false);
            piece.gameObject.layer = LayerMask.NameToLayer("DraggableMagnet");

            visibleIndex++;
        }
    }

    private Vector3 GetWrapDirection()
    {
        if (!wrapAwayFromArena || arenaCenter == null)
            return transform.right;

        Vector3 direction = transform.position - arenaCenter.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return transform.right;

        // Make wrapping cleanly horizontal or vertical, not diagonal.
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.z))
        {
            return direction.x >= 0f ? Vector3.right : Vector3.left;
        }
        else
        {
            return direction.z >= 0f ? Vector3.forward : Vector3.back;
        }
    }

    public int GetReserveCount()
    {
        int count = 0;

        for (int i = 0; i < reservePieces.Count; i++)
        {
            MagnetPiece piece = reservePieces[i];

            if (piece != null && piece.PieceState == MagnetPiece.State.Reserve)
                count++;
        }

        return count;
    }
}