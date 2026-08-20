using System.Collections;
using UnityEngine;

public sealed class HitFlash : MonoBehaviour
{
    private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
    private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");

    [SerializeField] private Color flashColor = Color.white;
    [SerializeField] private float flashDuration = 0.08f;

    private SpriteRenderer[] renderers;
    private Material[] originalMaterials;
    private Material[] flashMaterials;
    private Color[] originalColors;
    private Coroutine flashRoutine;

    public void Flash()
    {
        Flash(flashColor, flashDuration);
    }

    public void Flash(Color color, float duration = 0.08f)
    {
        CacheRenderers();
        if (renderers == null || renderers.Length == 0) return;

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            RestoreRenderers();
        }

        flashRoutine = StartCoroutine(FlashRoutine(color, Mathf.Max(0.01f, duration)));
    }

    private IEnumerator FlashRoutine(Color color, float duration)
    {
        Shader shader = Shader.Find("GamesJam/SpriteFlash");
        bool usingShader = shader != null;

        if (usingShader)
        {
            PrepareFlashMaterials(shader, color);
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float amount = 1f - Mathf.Clamp01(elapsed / duration);

            if (usingShader)
            {
                SetFlashAmount(amount);
            }
            else
            {
                TintRenderers(color, amount);
            }

            yield return null;
        }

        RestoreRenderers();
        flashRoutine = null;
    }

    private void CacheRenderers()
    {
        renderers = GetComponentsInChildren<SpriteRenderer>();
        originalMaterials = new Material[renderers.Length];
        originalColors = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            originalMaterials[i] = renderers[i].sharedMaterial;
            originalColors[i] = renderers[i].color;
        }
    }

    private void PrepareFlashMaterials(Shader shader, Color color)
    {
        flashMaterials = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            Material material = new(shader)
            {
                name = "Runtime Hit Flash",
            };
            material.SetColor(FlashColorId, color);
            material.SetFloat(FlashAmountId, 1f);
            renderers[i].material = material;
            flashMaterials[i] = material;
        }
    }

    private void SetFlashAmount(float amount)
    {
        if (flashMaterials == null) return;

        for (int i = 0; i < flashMaterials.Length; i++)
        {
            if (flashMaterials[i] != null)
            {
                flashMaterials[i].SetFloat(FlashAmountId, amount);
            }
        }
    }

    private void TintRenderers(Color color, float amount)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].color = Color.Lerp(originalColors[i], color, amount);
            }
        }
    }

    private void RestoreRenderers()
    {
        if (renderers == null) return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;

            if (originalMaterials != null && i < originalMaterials.Length)
            {
                renderers[i].sharedMaterial = originalMaterials[i];
            }

            if (originalColors != null && i < originalColors.Length)
            {
                renderers[i].color = originalColors[i];
            }
        }

        if (flashMaterials != null)
        {
            for (int i = 0; i < flashMaterials.Length; i++)
            {
                if (flashMaterials[i] != null)
                {
                    Destroy(flashMaterials[i]);
                }
            }
        }

        flashMaterials = null;
    }
}
