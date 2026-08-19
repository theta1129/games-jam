using System.Collections;
using UnityEngine;

public class CameraShake : MonoBehaviour
{
    public bool started = false;
    public AnimationCurve animationCurve;
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
        Vector2 startPos = transform.position;
        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float strength = intensity * animationCurve.Evaluate(elapsedTime / duration);
            transform.position = startPos + Random.insideUnitCircle * strength;
            yield return null;
        }
        transform.position = startPos;
    }

    // Use this instead of starting Shake directly so a new impact replaces an older shake cleanly.
    public void ShakeScreen(float duration = 0.18f, float intensity = 0.25f)
    {
        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
        shakeRoutine = StartCoroutine(ShakeScreenRoutine(duration, intensity));
    }

    private IEnumerator ShakeScreenRoutine(float duration, float intensity)
    {
        yield return Shake(duration, intensity);
        shakeRoutine = null;
    }
}
