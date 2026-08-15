using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class PlayerAttackHitBox : MonoBehaviour
{
    [SerializeField] private Vector2 size = new(1f, 1f);

    private readonly HashSet<Enemy> hitEnemies = new();
    private BoxCollider2D hitCollider;
    private SpriteRenderer spriteRenderer;
    private Coroutine hideRoutine;
    private ColorType activeColor;

    private void Awake()
    {
        CacheComponents();
    }

    public void Show(float duration, ColorType colorType)
    {
        gameObject.SetActive(true);
        CacheComponents();

        activeColor = colorType;
        hitEnemies.Clear();

        hitCollider.enabled = true;

        if (spriteRenderer != null)
        {
            spriteRenderer.color = GetDisplayColor(colorType);
            spriteRenderer.enabled = true;
        }

        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
        }

        hideRoutine = StartCoroutine(HideAfter(duration));
    }

    private void CacheComponents()
    {
        hitCollider = GetComponent<BoxCollider2D>();
        hitCollider.isTrigger = true;
        hitCollider.size = size;

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            EnsureSprite();
            spriteRenderer.enabled = false;
        }
    }

    private IEnumerator HideAfter(float duration)
    {
        yield return new WaitForSeconds(duration);
        Hide();
    }

    private void Hide()
    {
        hitCollider.enabled = false;

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }

        hideRoutine = null;
        gameObject.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryHit(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryHit(other);
    }

    private void TryHit(Collider2D other)
    {
        Enemy enemy = other.GetComponentInParent<Enemy>();
        if (enemy == null || enemy.pattern == null || enemy.pattern.Count == 0 || hitEnemies.Contains(enemy))
        {
            return;
        }

        hitEnemies.Add(enemy);
        enemy.OnHit(activeColor);
    }

    private void EnsureSprite()
    {
        if (spriteRenderer.sprite != null)
        {
            return;
        }

        Texture2D texture = Texture2D.whiteTexture;
        Rect rect = new(0f, 0f, texture.width, texture.height);
        spriteRenderer.sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), texture.width);
        spriteRenderer.sortingOrder = 10;
    }

    private static Color GetDisplayColor(ColorType colorType)
    {
        return colorType switch
        {
            ColorType.Blue => new Color(0.2f, 0.45f, 1f, 0.45f),
            ColorType.Yellow => new Color(1f, 0.85f, 0.1f, 0.45f),
            _ => new Color(1f, 0.2f, 0.2f, 0.45f),
        };
    }
}
