using Unity.Netcode;
using UnityEngine;

public class MagnetPiece : NetworkBehaviour
{
    public enum Owner
    {
        Player1,
        Player2,
        Player3,
        Player4
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

    private readonly NetworkVariable<int> networkOwner = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<int> networkState = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public Owner PieceOwner => IsNetworkActive() ? (Owner)networkOwner.Value : owner;
    public State PieceState => IsNetworkActive() ? (State)networkState.Value : state;
    

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (pieceRenderer == null)
            pieceRenderer = GetComponentInChildren<Renderer>();
    }

    public override void OnNetworkSpawn()
    {
        networkOwner.OnValueChanged += HandleOwnerChanged;
        networkState.OnValueChanged += HandleStateChanged;

        ApplyNetworkValues();

        if (NetworkManager.Singleton != null &&
        NetworkManager.Singleton.IsListening &&
        !NetworkManager.Singleton.IsServer)
        {
            SetPhysicsEnabled(false);
        }
    }

    public override void OnNetworkDespawn()
    {
        networkOwner.OnValueChanged -= HandleOwnerChanged;
        networkState.OnValueChanged -= HandleStateChanged;
    }

    private void HandleOwnerChanged(int oldValue, int newValue)
    {
        owner = (Owner)newValue;
    }

    private void HandleStateChanged(int oldValue, int newValue)
    {
        state = (State)newValue;
        ApplyStateVisuals();
    }

    private void ApplyNetworkValues()
    {
        owner = (Owner)networkOwner.Value;
        state = (State)networkState.Value;
        ApplyStateVisuals();
    }

    private bool IsNetworkActive()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && IsSpawned;
    }

    public void Initialize(Owner newOwner)
    {
        owner = newOwner;
        state = State.Reserve;

        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening &&
            NetworkManager.Singleton.IsServer)
        {
            networkOwner.Value = (int)newOwner;
            networkState.Value = (int)State.Reserve;
        }

        gameObject.layer = LayerMask.NameToLayer("DraggableMagnet");
        SnapUpright();
        SetPhysicsEnabled(false);
        ResetVisual();
    }

    public void SetState(State newState)
    {
        state = newState;

        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening &&
            NetworkManager.Singleton.IsServer &&
            IsSpawned)
        {
            networkState.Value = (int)newState;
        }

        ApplyStateVisuals();
    }

    public void SetOwner(Owner newOwner)
    {
        owner = newOwner;

        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening &&
            NetworkManager.Singleton.IsServer &&
            IsSpawned)
        {
            networkOwner.Value = (int)newOwner;
        }
    }

    private void ApplyStateVisuals()
    {
        if (PieceState == State.Placed)
            gameObject.layer = LayerMask.NameToLayer("PlacedMagnet");
        else
            gameObject.layer = LayerMask.NameToLayer("DraggableMagnet");

        ResetVisual();
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