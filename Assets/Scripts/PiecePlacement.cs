using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode.Components;

public class PiecePlacement : MonoBehaviour
{
    public event Action<MagnetPiece> OnPiecePlacedSuccessfully;

    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private ArenaEllipseBounds arenaBounds;
    [SerializeField] private GameAudio gameAudio;

    [Header("Layers")]
    [SerializeField] private LayerMask boardMask;
    [SerializeField] private LayerMask magnetMask;

    [Header("Placement")]
    [SerializeField] private float pieceHeight = 0.6f;

    private int draggableLayer;
    private int placedLayer;


    private MagnetPiece draggedPiece;
    private bool isDragging;
    private Vector3 dragStartPosition;
    private Vector3 currentDragWorldPosition;
    private bool currentDragIsValid;
    private NetworkTransform draggedNetworkTransform;
    private GameManager gameManager;

    public void SetGameManager(GameManager manager)
    {
        gameManager = manager;
    }

    private void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
        
        draggableLayer = LayerMask.NameToLayer("DraggableMagnet");
        placedLayer = LayerMask.NameToLayer("PlacedMagnet");
    }

    private void Update()
    {
        if (gameManager != null && !gameManager.CanPlayerInteract())
            return;

        // Mobile touch
        if (Touchscreen.current != null)
        {
            var touch = Touchscreen.current.primaryTouch;
            Vector2 pos = touch.position.ReadValue();

            if (touch.press.wasPressedThisFrame)
            {
                TryBeginDrag(pos);
            }

            if (isDragging && touch.press.isPressed)
            {
                UpdateDrag(pos);
            }

            if (isDragging && touch.press.wasReleasedThisFrame)
            {
                EndDrag();
            }

            return;
        }

        // Editor / desktop mouse
        if (Mouse.current != null)
        {
            Vector2 pos = Mouse.current.position.ReadValue();

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                TryBeginDrag(pos);
            }

            if (isDragging && Mouse.current.leftButton.isPressed)
            {
                UpdateDrag(pos);
            }

            if (isDragging && Mouse.current.leftButton.wasReleasedThisFrame)
            {
                EndDrag();
            }
        }
    }

    private void TryBeginDrag(Vector2 screenPosition)
    {
        Ray ray = mainCamera.ScreenPointToRay(screenPosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, magnetMask))
        {
            MagnetPiece piece = hit.collider.GetComponent<MagnetPiece>();

            if (piece == null)
                return;

            if (gameManager == null)
                return;

            if (!gameManager.CanCurrentPlayerDrag(piece))
                return;

            draggedPiece = piece;
            isDragging = true;
            dragStartPosition = draggedPiece.transform.position;
            currentDragWorldPosition = dragStartPosition;
            currentDragIsValid = false;

            draggedNetworkTransform = draggedPiece.GetComponent<NetworkTransform>();

            if (gameManager != null && gameManager.IsOnlineGame() && draggedNetworkTransform != null)
            {
                draggedNetworkTransform.enabled = false;
            }

            draggedPiece.SetPhysicsEnabled(false);
            draggedPiece.SnapUpright();

            if (gameManager == null || !gameManager.IsOnlineGame())
            {
                draggedPiece.SetState(MagnetPiece.State.Dragging);
            }
            // 🚫 Disable collision between dragged and placed magnets
            Physics.IgnoreLayerCollision(draggableLayer, placedLayer, true);

            Debug.Log("Picked up magnet: " + draggedPiece.name);
        }
    }

    private void UpdateDrag(Vector2 screenPosition)
    {
        if (draggedPiece == null)
            return;

        Ray ray = mainCamera.ScreenPointToRay(screenPosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, boardMask))
        {
            Vector3 target = hit.point;
            target.y = pieceHeight;

            currentDragWorldPosition = target;
            currentDragIsValid = arenaBounds.IsInside(target);

            draggedPiece.transform.position = target;

            if (currentDragIsValid)
                draggedPiece.ShowValidPlacementVisual();
            else
                draggedPiece.ShowInvalidPlacementVisual();
        }
    }

   public void CancelCurrentDrag()
    {
        if (!isDragging || draggedPiece == null)
            return;

        draggedPiece.transform.position = dragStartPosition;
        draggedPiece.SnapUpright();
        draggedPiece.SetState(MagnetPiece.State.Reserve);
        draggedPiece.SetPhysicsEnabled(false);
        draggedPiece.gameObject.layer = LayerMask.NameToLayer("DraggableMagnet");
        draggedPiece.ResetVisual();

        Physics.IgnoreLayerCollision(draggableLayer, placedLayer, false);

        if (draggedNetworkTransform != null)
        {
            draggedNetworkTransform.enabled = true;
            draggedNetworkTransform = null;
        }

        draggedPiece = null;
        isDragging = false;

        Debug.Log("Current drag cancelled");
    }

    private void EndDrag()
    {
        if (draggedPiece == null)
        {
            isDragging = false;
            return;
        }

        bool isValidDrop = currentDragIsValid;
        Vector3 finalDropPosition = currentDragWorldPosition;

        if (isValidDrop)
        {
            draggedPiece.ResetVisual();

            Physics.IgnoreLayerCollision(draggableLayer, placedLayer, false);

            if (draggedNetworkTransform != null)
            {
                draggedNetworkTransform.enabled = true;
                draggedNetworkTransform = null;
            }

            if (gameManager != null && gameManager.IsOnlineGame())
            {
                gameManager.RequestPlacePiece(draggedPiece, finalDropPosition);
            }
            else
            {
                draggedPiece.transform.position = finalDropPosition;
                draggedPiece.SetPhysicsEnabled(true);
                draggedPiece.SetState(MagnetPiece.State.Placed);
                draggedPiece.gameObject.layer = LayerMask.NameToLayer("PlacedMagnet");

                if (gameAudio != null)
                    gameAudio.PlayPlace();

                Debug.Log("Released magnet inside arena");
                OnPiecePlacedSuccessfully?.Invoke(draggedPiece);
            }
        }
        else
        {
            draggedPiece.transform.position = dragStartPosition;
            draggedPiece.SnapUpright();
            draggedPiece.SetState(MagnetPiece.State.Reserve);
            draggedPiece.SetPhysicsEnabled(false);
            draggedPiece.gameObject.layer = LayerMask.NameToLayer("DraggableMagnet");
            draggedPiece.ResetVisual();

            if (gameAudio != null)
                gameAudio.PlayInvalidDrop();

            Physics.IgnoreLayerCollision(draggableLayer, placedLayer, false);

            if (draggedNetworkTransform != null)
            {
                draggedNetworkTransform.enabled = true;
                draggedNetworkTransform = null;
            }
            
            Debug.Log("Invalid drop - magnet returned");

            if (gameManager != null)
                gameManager.RefreshReserveLayouts();
        }

        draggedPiece = null;
        isDragging = false;
    }
}