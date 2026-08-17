using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Persistent player child used as the visible arm and its damage hitbox.
[RequireComponent(typeof(BoxCollider2D))]
public class PlayerAttackHitBox : MonoBehaviour
{
    [SerializeField] private Vector2 size = new(1.25f, 0.45f);
    [SerializeField] private float armReach = 0.85f;
    [Header("Weapon sprites (vertical, centred artwork)")]
    [SerializeField] private Sprite redWeaponSprite;
    [SerializeField] private Sprite blueWeaponSprite;
    [SerializeField] private Sprite yellowWeaponSprite;

    private readonly HashSet<Enemy> hitEnemies = new();
    private BoxCollider2D hitCollider;
    private SpriteRenderer spriteRenderer;
    private Coroutine swingRoutine;
    private Coroutine throwRoutine;
    private ColorType activeColor;
    private float activeKnockbackForce;
    private float activeReach;
    private Sprite activeWeaponSprite;
    private Sprite centeredWeaponSprite;

    public bool IsSwinging => swingRoutine != null;
    public Sprite WeaponSprite => activeWeaponSprite;

    private void Awake()
    {
        CacheComponents();
        activeReach = armReach;
        hitCollider.enabled = false;
        if (spriteRenderer != null) spriteRenderer.enabled = true;
    }

    public void Aim(Vector2 direction)
    {
        if (!IsSwinging && throwRoutine == null && direction.sqrMagnitude > 0.0001f) SetArmPose(direction.normalized);
    }

    public void SetColor(ColorType colorType)
    {
        activeColor = colorType;
        activeWeaponSprite = GetWeaponSprite(colorType);
        centeredWeaponSprite = null;
        CacheComponents();

        if (spriteRenderer != null)
        {
            spriteRenderer.color = GetDisplayColor(colorType);
        }
    }

    public void Swing(float windup, float duration, ColorType colorType, Vector2 direction, float arc, float knockbackForce, bool pierce)
    {
        CacheComponents();
        if (swingRoutine != null) StopCoroutine(swingRoutine);

        SetColor(colorType);
        activeKnockbackForce = knockbackForce;
        activeReach = pierce ? armReach * 1.7f : armReach;
        hitEnemies.Clear();
        hitCollider.enabled = false;
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
        }

        swingRoutine = StartCoroutine(SwingRoutine(windup, duration, direction.normalized, arc, knockbackForce, pierce));
    }

    // A non-damaging follow-through used when the yellow weapon is thrown.
    public void ThrowMotion(Vector2 direction, float duration = 0.22f)
    {
        if (throwRoutine != null) StopCoroutine(throwRoutine);
        throwRoutine = StartCoroutine(ThrowRoutine(direction.normalized, duration));
    }

    private IEnumerator ThrowRoutine(Vector2 direction, float duration)
    {
        float centerAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            // Pull the held weapon around one side, then whip it forward as the separate weapon leaves.
            float angle = progress < 0.4f
                ? Mathf.Lerp(centerAngle, centerAngle + 75f, progress / 0.4f)
                : Mathf.Lerp(centerAngle + 75f, centerAngle - 25f, (progress - 0.4f) / 0.6f);
            SetArmPose(AngleToDirection(angle));
            yield return null;
        }

        SetArmPose(direction);
        throwRoutine = null;
    }

    private IEnumerator SwingRoutine(float windup, float duration, Vector2 direction, float arc, float knockbackForce, bool pierce)
    {
        // Draw the arm back behind the player during the ready phase, then release it into the swing.
        float centerAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float startAngle = centerAngle - arc * 0.5f;
        float windupElapsed = 0f;
        while (windupElapsed < windup)
        {
            windupElapsed += Time.deltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(windupElapsed / windup));
            // Wind up along the same circular path, but in the opposite direction to the release swing.
            float angle = Mathf.Lerp(centerAngle + arc * 0.5f, startAngle, progress);
            SetArmPose(AngleToDirection(angle));
            yield return null;
        }

        float elapsed = 0f;
        hitCollider.enabled = true;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            float angle = Mathf.Lerp(startAngle, startAngle + arc, progress);
            SetArmPose(AngleToDirection(angle));
            yield return null;
        }

        hitCollider.enabled = false;
        swingRoutine = null;
        activeReach = armReach;
        SetArmPose(direction);
    }

    private void CacheComponents()
    {
        hitCollider ??= GetComponent<BoxCollider2D>();
        hitCollider.isTrigger = true;
        hitCollider.size = activeWeaponSprite != null ? new Vector2(size.y, size.x) : size;
        spriteRenderer ??= GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            if (activeWeaponSprite != null)
            {
                // Recreate the assigned sprite with a centred pivot so every weapon sits on the arm correctly.
                if (centeredWeaponSprite == null)
                {
                    centeredWeaponSprite = Sprite.Create(activeWeaponSprite.texture, activeWeaponSprite.textureRect, new Vector2(0.5f, 0.5f), activeWeaponSprite.pixelsPerUnit);
                }
                spriteRenderer.sprite = centeredWeaponSprite;
                spriteRenderer.drawMode = SpriteDrawMode.Simple;
            }
            else
            {
                EnsureSprite();
                spriteRenderer.drawMode = SpriteDrawMode.Sliced;
                spriteRenderer.size = size;
            }
            spriteRenderer.sortingOrder = 10;
        }
    }

    private void SetArmPose(Vector2 direction, float reach = -1f)
    {
        float targetReach = reach >= 0f ? reach : (activeReach > 0f ? activeReach : armReach);
        transform.localPosition = direction * targetReach;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        // Weapon art is authored upright/vertical, while the hitbox needs to face the attack direction.
        transform.localRotation = Quaternion.Euler(0f, 0f, angle + (activeWeaponSprite != null ? -90f : 0f));
    }

    private void OnTriggerEnter2D(Collider2D other) => TryHit(other);
    private void OnTriggerStay2D(Collider2D other) => TryHit(other);

    private void TryHit(Collider2D other)
    {
        if (!IsSwinging) return;
        Enemy enemy = other.GetComponentInParent<Enemy>();
        if (enemy == null || enemy.pattern == null || enemy.pattern.Count == 0 || hitEnemies.Contains(enemy)) return;
        hitEnemies.Add(enemy);
        enemy.OnHit(activeColor, transform.position, activeKnockbackForce);
    }

    private void EnsureSprite()
    {
        if (spriteRenderer.sprite != null) return;
        Texture2D texture = Texture2D.whiteTexture;
        spriteRenderer.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
    }

    private static Vector2 AngleToDirection(float angle)
    {
        float radians = angle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }

    private Sprite GetWeaponSprite(ColorType colorType) => colorType switch
    {
        ColorType.Blue => blueWeaponSprite,
        ColorType.Yellow => yellowWeaponSprite,
        _ => redWeaponSprite,
    };

    private static Color GetDisplayColor(ColorType colorType) => colorType switch
    {
        ColorType.Blue => new Color(0.2f, 0.45f, 1f, 0.75f),
        ColorType.Yellow => new Color(1f, 0.85f, 0.1f, 0.75f),
        _ => new Color(1f, 0.2f, 0.2f, 0.75f),
    };
}
