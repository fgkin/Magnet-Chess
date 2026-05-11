using System.Collections.Generic;
using UnityEngine;

public class ReserveLayout : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] private Vector3 localStartOffset = Vector3.zero;
    [SerializeField] private Vector3 localStepOffset = new Vector3(0f, 0f, 0.8f);
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

        for (int i = 0; i < reservePieces.Count; i++)
        {
            MagnetPiece piece = reservePieces[i];

            if (piece == null)
                continue;

            if (piece.PieceState != MagnetPiece.State.Reserve)
                continue;

            Vector3 localPos = localStartOffset + localStepOffset * visibleIndex;
            Vector3 worldPos = transform.TransformPoint(localPos);
            worldPos.y = pieceY;

            piece.transform.position = worldPos;
            piece.SnapUpright();
            piece.SetPhysicsEnabled(false);
            piece.gameObject.layer = LayerMask.NameToLayer("DraggableMagnet");

            visibleIndex++;
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