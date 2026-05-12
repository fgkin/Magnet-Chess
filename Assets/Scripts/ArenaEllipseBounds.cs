using UnityEngine;

public class ArenaEllipseBounds : MonoBehaviour
{
    [SerializeField] private Transform centerPoint;
    [SerializeField] private float radiusX = 4f;
    [SerializeField] private float radiusZ = 3f;

    public float RadiusX => radiusX;
    public float RadiusZ => radiusZ;

    private void Reset()
    {
        centerPoint = transform;
    }

    public void SetRadii(float newRadiusX, float newRadiusZ)
    {
        radiusX = Mathf.Max(0.1f, newRadiusX);
        radiusZ = Mathf.Max(0.1f, newRadiusZ);
    }

    public bool IsInside(Vector3 worldPosition)
    {
        Vector3 center = centerPoint != null ? centerPoint.position : transform.position;

        float dx = worldPosition.x - center.x;
        float dz = worldPosition.z - center.z;

        float value = (dx * dx) / (radiusX * radiusX) + (dz * dz) / (radiusZ * radiusZ);
        return value <= 1f;
    }

    public Vector3 ClampToEllipse(Vector3 worldPosition)
    {
        Vector3 center = centerPoint != null ? centerPoint.position : transform.position;

        float dx = worldPosition.x - center.x;
        float dz = worldPosition.z - center.z;

        float value = (dx * dx) / (radiusX * radiusX) + (dz * dz) / (radiusZ * radiusZ);

        if (value <= 1f)
            return worldPosition;

        float scale = 1f / Mathf.Sqrt(value);

        float clampedX = center.x + dx * scale;
        float clampedZ = center.z + dz * scale;

        return new Vector3(clampedX, worldPosition.y, clampedZ);
    }

}