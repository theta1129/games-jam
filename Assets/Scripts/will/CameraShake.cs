using System.Collections;
using UnityEngine;

public class CameraShake : MonoBehaviour
{
    public bool started = false;
    public AnimationCurve animationCurve;
    public Vector3 CurrentOffset { get; private set; }

    private Coroutine shakeRoutine;

    private void Awake()
    {
        animationCurve ??= AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (started)
        {
            started = false;
            StartCoroutine(Shake());
        }
    }

    public IEnumerator Shake(float duration = 0.5f, float intensity = 1f)
    {
        Vector3 startPos = transform.position;
        bool useAdditiveOffset = GetComponent<CombatCameraController>() != null;
        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float strength = intensity * animationCurve.Evaluate(elapsedTime / duration);
            Vector2 offset = Random.insideUnitCircle * strength;
            CurrentOffset = new Vector3(offset.x, offset.y, 0f);
            if (!useAdditiveOffset)
            {
                transform.position = startPos + CurrentOffset;
            }
            yield return null;
        }

        if (!useAdditiveOffset)
        {
            transform.position = startPos;
        }
        CurrentOffset = Vector3.zero;
    }

    // Use this instead of starting Shake directly so a new impact replaces an older shake cleanly.
    public void ShakeScreen(float duration = 0.18f, float intensity = 0.25f)
    {
        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
        if (CurrentOffset != Vector3.zero && GetComponent<CombatCameraController>() == null)
        {
            transform.position -= CurrentOffset;
        }
        CurrentOffset = Vector3.zero;
        shakeRoutine = StartCoroutine(ShakeScreenRoutine(duration, intensity));
    }

    private IEnumerator ShakeScreenRoutine(float duration, float intensity)
    {
        yield return Shake(duration, intensity);
        shakeRoutine = null;
    }
}
