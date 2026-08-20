using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Persistent player child used as the visible arm and its damage hitbox.
[RequireComponent(typeof(BoxCollider2D))]
public class PlayerAttackHitBox : MonoBehaviour
{
    [SerializeField] private Vector2 size = new(1.25f, 0.45f);
    [SerializeField] private float armReach = 0.85f;
    [SerializeField] private float afterimageLifetime = 0.12f;
    [SerializeField] private Color afterimageColor = new(1f, 1f, 1f, 0.32f);
    [Header("Weapon sprites (vertical artwork)")]
    [SerializeField] private Sprite redWeaponSprite;
    [SerializeField] private Sprite blueWeaponSprite;
    [SerializeField] private Sprite yellowWeaponSprite;

    private readonly HashSet<Enemy> hitEnemies = new();
    private BoxCollider2D hitCollider;
    private SpriteRenderer spriteRenderer;
    private SpriteRenderer weaponRenderer;
    private Coroutine swingRoutine;
    private Coroutine throwRoutine;
    private ColorType activeColor;
    private float activeKnockbackForce;
    private float activeReach;
    private Sprite activeWeaponSprite;
    private bool activePierce;
    private bool holdingComboPose;
    private Coroutine recoveryRoutine;

    public bool IsSwinging => swingRoutine != null;
    public Sprite WeaponSprite => activeWeaponSprite;
    private bool HasWeaponSprite => activeWeaponSprite != null;

    private void Awake()
    {
        CacheComponents();
        activeReach = armReach;
        hitCollider.enabled = false;
    }

    public void Aim(Vector2 direction)
    {
        if (!IsSwinging && throwRoutine == null && !holdingComboPose && direction.sqrMagnitude > 0.0001f) SetArmPose(direction.normalized);
    }

    public void SetColor(ColorType colorType)
    {
        activeColor = colorType;
        activeWeaponSprite = GetWeaponSprite(colorType);
        CacheComponents();

        if (spriteRenderer != null)
        {
            spriteRenderer.color = HasWeaponSprite ? Color.white : GetDisplayColor(colorType);
        }
    }

    public void Swing(float windup, float duration, ColorType colorType, Vector2 direction, float arc, float knockbackForce, bool pierce, bool swingBackToRest)
    {
        CacheComponents();
        if (swingRoutine != null) StopCoroutine(swingRoutine);
        if (recoveryRoutine != null) StopCoroutine(recoveryRoutine);

        SetColor(colorType);
        activeKnockbackForce = knockbackForce;
        activePierce = pierce;
        activeReach = pierce ? armReach * 1.7f : armReach;
        holdingComboPose = false;
        hitEnemies.Clear();
        hitCollider.enabled = false;
        if (spriteRenderer != null) spriteRenderer.enabled = !HasWeaponSprite;
        if (weaponRenderer != null) weaponRenderer.enabled = HasWeaponSprite;

        swingRoutine = StartCoroutine(SwingRoutine(windup, duration, direction.normalized, arc, knockbackForce, pierce, swingBackToRest));
    }

    public void Thrust(float windup, float duration, ColorType colorType, Vector2 direction, float knockbackForce, bool pierce, float thrustReach, float pullbackDistance)
    {
        CacheComponents();
        if (swingRoutine != null) StopCoroutine(swingRoutine);
        if (recoveryRoutine != null) StopCoroutine(recoveryRoutine);

        SetColor(colorType);
        activeKnockbackForce = knockbackForce;
        activePierce = pierce;
        activeReach = Mathf.Max(armReach, thrustReach);
        holdingComboPose = false;
        hitEnemies.Clear();
        hitCollider.enabled = false;
        if (spriteRenderer != null) spriteRenderer.enabled = !HasWeaponSprite;
        if (weaponRenderer != null) weaponRenderer.enabled = HasWeaponSprite;

        swingRoutine = StartCoroutine(ThrustRoutine(windup, duration, direction.normalized, activeReach, pullbackDistance));
    }

    public void ReleaseComboPose(Vector2 direction)
    {
        if (recoveryRoutine != null) StopCoroutine(recoveryRoutine);
        holdingComboPose = false;
        activeReach = armReach;
        if (!IsSwinging && throwRoutine == null && direction.sqrMagnitude > 0.0001f) SetArmPose(direction.normalized);
    }

    public void RecoverToRestPose(Vector2 direction, float duration)
    {
        if (IsSwinging || throwRoutine != null || direction.sqrMagnitude <= 0.0001f) return;
        if (recoveryRoutine != null) StopCoroutine(recoveryRoutine);
        recoveryRoutine = StartCoroutine(RecoverRoutine(direction.normalized, Mathf.Max(0.01f, duration)));
    }

    // A non-damaging follow-through used when the yellow weapon is thrown.
    public void ThrowMotion(Vector2 direction, float duration = 0.22f)
    {
        if (recoveryRoutine != null) StopCoroutine(recoveryRoutine);
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

    private IEnumerator SwingRoutine(float windup, float duration, Vector2 direction, float arc, float knockbackForce, bool pierce, bool swingBackToRest)
    {
        // Draw the arm back behind the player during the ready phase, then release it into the swing.
        float centerAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float halfArc = arc * 0.5f;
        float backSwingAngle = centerAngle - halfArc;
        float followThroughAngle = centerAngle + halfArc;
        float startAngle = swingBackToRest ? followThroughAngle : backSwingAngle;
        float endAngle = swingBackToRest ? backSwingAngle : followThroughAngle;
        float windupElapsed = 0f;
        while (windupElapsed < windup)
        {
            windupElapsed += Time.deltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(windupElapsed / windup));
            float angle = Mathf.Lerp(endAngle, startAngle, progress);
            SetArmPose(AngleToDirection(angle));
            yield return null;
        }

        float elapsed = 0f;
        SpawnSwingAfterimages(startAngle, centerAngle, endAngle, duration);
        hitCollider.enabled = true;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            float angle = Mathf.Lerp(startAngle, endAngle, progress);
            SetArmPose(AngleToDirection(angle));
            yield return null;
        }

        hitCollider.enabled = false;
        swingRoutine = null;
        holdingComboPose = !swingBackToRest;
        if (!holdingComboPose)
        {
            activeReach = armReach;
            SetArmPose(direction);
        }
    }

    private void CacheComponents()
    {
        hitCollider ??= GetComponent<BoxCollider2D>();
        hitCollider.isTrigger = true;
        hitCollider.size = HasWeaponSprite ? new Vector2(size.y, size.x) : size;
        hitCollider.offset = Vector2.zero;
        spriteRenderer ??= GetComponent<SpriteRenderer>();

        if (HasWeaponSprite)
        {
            if (spriteRenderer != null) spriteRenderer.enabled = false;
            EnsureWeaponRenderer();
            weaponRenderer.sprite = activeWeaponSprite;
            weaponRenderer.color = Color.white;
            weaponRenderer.sortingOrder = 10;
            weaponRenderer.enabled = true;
            FitWeaponVisual();
            return;
        }

        if (weaponRenderer != null) weaponRenderer.enabled = false;
        if (spriteRenderer != null)
        {
            EnsureSprite();
            spriteRenderer.drawMode = SpriteDrawMode.Sliced;
            spriteRenderer.size = size;
            spriteRenderer.color = GetDisplayColor(activeColor);
            spriteRenderer.sortingOrder = 10;
            spriteRenderer.enabled = true;
        }
    }

    private IEnumerator ThrustRoutine(float windup, float duration, Vector2 direction, float thrustReach, float pullbackDistance)
    {
        SetArmPose(direction, armReach);

        float pulledReach = Mathf.Max(0.15f, armReach - pullbackDistance);
        float windupElapsed = 0f;
        while (windupElapsed < windup)
        {
            windupElapsed += Time.deltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(windupElapsed / windup));
            SetArmPose(direction, Mathf.Lerp(armReach, pulledReach, progress));
            yield return null;
        }

        float elapsed = 0f;
        SpawnThrustAfterimage(direction, thrustReach, duration);
        hitCollider.enabled = true;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            SetArmPose(direction, Mathf.Lerp(pulledReach, thrustReach, progress));
            yield return null;
        }

        hitCollider.enabled = false;
        swingRoutine = null;
        activeReach = thrustReach;
        holdingComboPose = true;
        SetArmPose(direction, thrustReach);
    }

    private IEnumerator RecoverRoutine(Vector2 direction, float duration)
    {
        hitCollider.enabled = false;
        holdingComboPose = false;

        Vector3 startPosition = transform.localPosition;
        Quaternion startRotation = transform.localRotation;
        Vector3 tuckedPosition = (Vector3)(direction * (armReach * 0.45f));
        Vector3 endPosition = (Vector3)(direction * armReach);
        Quaternion endRotation = GetPoseRotation(direction);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            if (progress < 0.45f)
            {
                float tuckProgress = Mathf.SmoothStep(0f, 1f, progress / 0.45f);
                transform.localPosition = Vector3.Lerp(startPosition, tuckedPosition, tuckProgress);
            }
            else
            {
                float extendProgress = Mathf.SmoothStep(0f, 1f, (progress - 0.45f) / 0.55f);
                transform.localPosition = Vector3.Lerp(tuckedPosition, endPosition, extendProgress);
            }
            transform.localRotation = Quaternion.Slerp(startRotation, endRotation, eased);
            yield return null;
        }

        activeReach = armReach;
        SetArmPose(direction);
        recoveryRoutine = null;
    }

    private void EnsureWeaponRenderer()
    {
        if (weaponRenderer != null) return;

        GameObject visual = new("WeaponVisual");
        visual.transform.SetParent(transform, false);
        visual.layer = gameObject.layer;
        weaponRenderer = visual.AddComponent<SpriteRenderer>();
    }

    private void FitWeaponVisual()
    {
        if (weaponRenderer == null || activeWeaponSprite == null) return;

        float sourceLength = Mathf.Max(activeWeaponSprite.bounds.size.y, 0.0001f);
        float visualScale = size.x / sourceLength;
        FitSpriteVisual(weaponRenderer.transform, activeWeaponSprite, visualScale);
    }

    private void SpawnSwingAfterimages(float startAngle, float centerAngle, float endAngle, float duration)
    {
        if (!HasWeaponSprite) return;

        float lifetime = Mathf.Max(afterimageLifetime, duration + 0.06f);
        SpawnWeaponAfterimage(AngleToDirection(startAngle), afterimageColor.a * 0.45f, lifetime);
        SpawnWeaponAfterimage(AngleToDirection(centerAngle), afterimageColor.a, lifetime);

        if (duration <= 0.05f)
        {
            SpawnWeaponAfterimage(AngleToDirection(endAngle), afterimageColor.a * 0.65f, lifetime);
        }
    }

    private void SpawnThrustAfterimage(Vector2 direction, float thrustReach, float duration)
    {
        if (!HasWeaponSprite) return;

        float lifetime = Mathf.Max(afterimageLifetime, duration + 0.05f);
        SpawnWeaponAfterimage(direction.normalized * Mathf.Max(0.2f, thrustReach / Mathf.Max(activeReach, 0.0001f)), afterimageColor.a * 0.75f, lifetime);
    }

    private void SpawnWeaponAfterimage(Vector2 poseDirection, float alpha, float lifetime)
    {
        Vector2 direction = poseDirection.sqrMagnitude > 0.0001f ? poseDirection.normalized : Vector2.right;
        GameObject ghost = new("WeaponAfterimage");
        ghost.transform.SetParent(transform.parent, false);
        ghost.layer = gameObject.layer;
        ghost.transform.localPosition = direction * (activeReach > 0f ? activeReach : armReach);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        ghost.transform.localRotation = GetPoseRotation(direction);

        GameObject visual = new("Visual");
        visual.transform.SetParent(ghost.transform, false);
        visual.layer = gameObject.layer;

        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = activeWeaponSprite;
        renderer.color = new Color(afterimageColor.r, afterimageColor.g, afterimageColor.b, alpha);
        renderer.sortingOrder = 9;

        float sourceLength = Mathf.Max(activeWeaponSprite.bounds.size.y, 0.0001f);
        FitSpriteVisual(visual.transform, activeWeaponSprite, size.x / sourceLength);
        StartCoroutine(FadeAfterimage(ghost, renderer, lifetime));
    }

    private IEnumerator FadeAfterimage(GameObject ghost, SpriteRenderer renderer, float lifetime)
    {
        float elapsed = 0f;
        Color startColor = renderer.color;
        while (elapsed < lifetime && renderer != null)
        {
            elapsed += Time.deltaTime;
            Color color = startColor;
            color.a = Mathf.Lerp(startColor.a, 0f, Mathf.Clamp01(elapsed / lifetime));
            renderer.color = color;
            yield return null;
        }

        if (ghost != null)
        {
            Destroy(ghost);
        }
    }

    private static void FitSpriteVisual(Transform visualTransform, Sprite sprite, float visualScale)
    {
        visualTransform.localRotation = Quaternion.identity;
        visualTransform.localScale = Vector3.one * visualScale;
        visualTransform.localPosition = -(Vector3)(sprite.bounds.center * visualScale);
    }

    private void SetArmPose(Vector2 direction, float reach = -1f)
    {
        float targetReach = reach >= 0f ? reach : (activeReach > 0f ? activeReach : armReach);
        transform.localPosition = direction * targetReach;
        transform.localRotation = GetPoseRotation(direction);
    }

    private Quaternion GetPoseRotation(Vector2 direction)
    {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        // Weapon art points up in the source texture, so its local up axis should face the attack direction.
        return Quaternion.Euler(0f, 0f, HasWeaponSprite ? angle - 90f : angle);
    }

    private void OnTriggerEnter2D(Collider2D other) => TryHit(other);
    private void OnTriggerStay2D(Collider2D other) => TryHit(other);

    private void TryHit(Collider2D other)
    {
        if (!IsSwinging) return;
        Enemy enemy = other.GetComponentInParent<Enemy>();
        if (enemy == null || enemy.pattern == null || enemy.pattern.Count == 0 || hitEnemies.Contains(enemy)) return;
        if (!enemy.OnHit(activeColor, transform.position, activeKnockbackForce)) return;

        hitEnemies.Add(enemy);
        if (!activePierce)
        {
            hitCollider.enabled = false;
        }
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
