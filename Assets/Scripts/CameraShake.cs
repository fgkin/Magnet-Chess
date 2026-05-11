using System.Collections;
using UnityEngine;

public class CameraShake : MonoBehaviour
{
    [SerializeField] private Transform targetCamera;
    [SerializeField] private float defaultDuration = 0.12f;
    [SerializeField] private float defaultStrength = 0.08f;

    private Vector3 originalLocalPosition;
    private Coroutine shakeRoutine;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = transform;

        originalLocalPosition = targetCamera.localPosition;
    }

    public void PlayShake()
    {
        PlayShake(defaultDuration, defaultStrength);
    }

    public void PlayShake(float duration, float strength)
    {
        if (shakeRoutine != null)
            StopCoroutine(shakeRoutine);

        shakeRoutine = StartCoroutine(ShakeRoutine(duration, strength));
    }

    private IEnumerator ShakeRoutine(float duration, float strength)
    {
        float timer = 0f;

        targetCamera.localPosition = originalLocalPosition;

        while (timer < duration)
        {
            timer += Time.deltaTime;

            Vector2 offset2D = Random.insideUnitCircle * strength;
            Vector3 offset = new Vector3(offset2D.x, offset2D.y, 0f);

            targetCamera.localPosition = originalLocalPosition + offset;

            yield return null;
        }

        targetCamera.localPosition = originalLocalPosition;
        shakeRoutine = null;
    }
}