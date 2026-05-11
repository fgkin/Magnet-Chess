using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MagnetSystem : MonoBehaviour
{
    [SerializeField] private float connectionRadius = 0.7f;
    [SerializeField] private LayerMask placedMagnetMask;

    private GameManager gameManager;

    public void Initialize(GameManager manager)
    {
        gameManager = manager;
    }

    public void EvaluatePlacement(MagnetPiece placedPiece)
    {
        StartCoroutine(ResolveAfterDelay(placedPiece));
    }

    private IEnumerator ResolveAfterDelay(MagnetPiece placedPiece)
    {
        yield return new WaitForSeconds(0.2f);

        List<MagnetPiece> cluster = GetConnectedCluster(placedPiece);

        if (cluster.Count > 1)
        {
            Debug.Log($"Cluster found: {cluster.Count}");

            yield return StartCoroutine(SnapCluster(cluster));

            if (gameManager != null)
                gameManager.CollectCluster(cluster);
        }
        else
        {
            if (gameManager != null)
                gameManager.FinishTurnAfterMagnetResolution();
        }
    }

    private IEnumerator SnapCluster(List<MagnetPiece> cluster)
    {
        float duration = 0.18f;

        Vector3 center = Vector3.zero;

        foreach (var piece in cluster)
            center += piece.transform.position;

        center /= cluster.Count;

        Dictionary<MagnetPiece, Vector3> startPositions = new();
        Dictionary<MagnetPiece, Vector3> startScales = new();

        foreach (var piece in cluster)
        {
            if (piece == null)
                continue;

            startPositions[piece] = piece.transform.position;
            startScales[piece] = piece.transform.localScale;
        }

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);

            // smooth snap feel
            float moveT = Mathf.SmoothStep(0f, 1f, t);

            // quick pop curve
            float scaleT = Mathf.Sin(t * Mathf.PI);

            foreach (var piece in cluster)
            {
                if (piece == null)
                    continue;

                Vector3 startPos = startPositions[piece];
                Vector3 targetPos = Vector3.Lerp(startPos, center, 0.45f);

                piece.transform.position = Vector3.Lerp(startPos, targetPos, moveT);

                Vector3 startScale = startScales[piece];
                Vector3 popScale = startScale * 1.12f;
                piece.transform.localScale = Vector3.Lerp(startScale, popScale, scaleT);
            }

            yield return null;
        }

        foreach (var piece in cluster)
        {
            if (piece == null)
                continue;

            if (startScales.ContainsKey(piece))
                piece.transform.localScale = startScales[piece];
        }
    }

    private List<MagnetPiece> GetConnectedCluster(MagnetPiece start)
    {
        List<MagnetPiece> result = new();
        Queue<MagnetPiece> queue = new();

        result.Add(start);
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            MagnetPiece current = queue.Dequeue();

            Collider[] hits = Physics.OverlapSphere(
                current.transform.position,
                connectionRadius,
                placedMagnetMask
            );

            foreach (var hit in hits)
            {
                MagnetPiece other = hit.GetComponent<MagnetPiece>();

                if (other == null)
                    continue;

                if (result.Contains(other))
                    continue;

                result.Add(other);
                queue.Enqueue(other);
            }
        }

        return result;
    }
}