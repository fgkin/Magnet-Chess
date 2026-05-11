using UnityEngine;

public class MagnetPiece : MonoBehaviour
{
    public enum Owner
    {
        Player1,
        Player2
    }

    public enum State
    {
        Reserve,
        Dragging,
        Placed
    }

    [SerializeField] private Owner owner;
    [SerializeField] private State state = State.Reserve;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Renderer pieceRenderer;

    [Header("Colors")]
    [SerializeField] private Color defaultColor = Color.black;
    [SerializeField] private Color validDragColor = Color.green;
    [SerializeField] private Color invalidDragColor = Color.red;

    public Owner PieceOwner => owner;
    public State PieceState => state;

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (pieceRenderer == null)
            pieceRenderer = GetComponentInChildren<Renderer>();
    }

    public void Initialize(Owner newOwner)
    {
        owner = newOwner;
        state = State.Reserve;

        gameObject.layer = LayerMask.NameToLayer("DraggableMagnet");
        SnapUpright();
        SetPhysicsEnabled(false);
        ResetVisual();
    }

    public void SetState(State newState)
    {
        state = newState;
    }

    public void SetOwner(Owner newOwner)
    {
        owner = newOwner;
    }

    public void SetPhysicsEnabled(bool enabled)
    {
        if (rb == null)
            return;

        if (enabled)
        {
            rb.isKinematic = false;
        }
        else
        {
            // Only reset velocity if NOT already kinematic
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            rb.isKinematic = true;
        }
    }

    public void SnapUpright()
    {
        transform.rotation = Quaternion.identity;
    }

    public void ShowValidPlacementVisual()
    {
        if (pieceRenderer != null)
            pieceRenderer.material.color = validDragColor;
    }

    public void ShowInvalidPlacementVisual()
    {
        if (pieceRenderer != null)
            pieceRenderer.material.color = invalidDragColor;
    }

    public void ResetVisual()
    {
        if (pieceRenderer == null)
            return;

         pieceRenderer.material.color = defaultColor;
    }
}