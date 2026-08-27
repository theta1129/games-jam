using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Persistent player child used as the visible arm and its damage hitbox.
[RequireComponent(typeof(BoxCollider2D))]
public class PlayerAttackHitBox : MonoBehaviour
{
    private static Sprite fallbackHitboxSprite;

    [SerializeField] private Vector2 size = new(1.25f, 0.45f);
    [SerializeField] private float hitboxLengthMultiplier = 1.25f;
    [SerializeField] private float hitboxThicknessMultiplier = 1.45f;
    [Header("Hitbox size boost")]
    [SerializeField, Min(1f)] private float overallAttackHitboxScale = 1.30f;
    [SerializeField, Min(1f)] private float blueAttackHitboxScale = 1.25f;
    [Header("Reliable area hit detection")]
    [SerializeField] private bool useDirectOverlapChecks = true;
    [SerializeField] private float armReach = 0.85f;
    [SerializeField] private float afterimageLifetime = 0.12f;
    [SerializeField] private Color afterimageColor = new(1f, 1f, 1f, 0.32f);
    [Header("Attack trail")]
    [SerializeField] private float trailTime = 0.2f;
    [SerializeField] private float trailStartWidth = 0.32f;
    [SerializeField] private float trailEndWidth = 0.08f;
    [Header("Weapon sprites (vertical artwork)")]
    [SerializeField] private Sprite redWeaponSprite;
    [SerializeField] private Sprite blueWeaponSprite;
    [SerializeField] private Sprite yellowWeaponSprite;

    private readonly HashSet<Enemy> hitEnemies = new();
    private BoxCollider2D hitCollider;
    private SpriteRenderer spriteRenderer;
    private SpriteRenderer weaponRenderer;
    private TrailRenderer weaponTrail;
    private Material trailMaterial;
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
    public bool IsBusy => swingRoutine != null || throwRoutine != null || recoveryRoutine != null;
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

    public void Swing(float windup, float duration, ColorType colorType, Vector2 direction, float arc, float knockbackForce, bool pierce, bool swingBackToRest, bool recoverAfterSwing = false, float recoverDuration = 0.14f)
    {
        CacheComponents();
        if (swingRoutine != null) StopCoroutine(swingRoutine);
        if (recoveryRoutine != null) StopCoroutine(recoveryRoutine);
        SetTrailActive(false);

        SetColor(colorType);
        activeKnockbackForce = knockbackForce;
        activePierce = pierce;
        activeReach = pierce ? armReach * 1.7f : armReach;
        holdingComboPose = false;
        hitEnemies.Clear();
        hitCollider.enabled = false;
        if (spriteRenderer != null) spriteRenderer.enabled = !HasWeaponSprite;
        if (weaponRenderer != null) weaponRenderer.enabled = HasWeaponSprite;

        swingRoutine = StartCoroutine(SwingRoutine(windup, duration, direction.normalized, arc, knockbackForce, pierce, swingBackToRest, recoverAfterSwing, recoverDuration));
    }

    public void Thrust(float windup, float duration, ColorType colorType, Vector2 direction, float knockbackForce, bool pierce, float thrustReach, float pullbackDistance)
    {
        CacheComponents();
        if (swingRoutine != null) StopCoroutine(swingRoutine);
        if (recoveryRoutine != null) StopCoroutine(recoveryRoutine);
        SetTrailActive(false);

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
        SetTrailActive(false);
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
        CacheComponents();
        if (recoveryRoutine != null) StopCoroutine(recoveryRoutine);
        if (throwRoutine != null) StopCoroutine(throwRoutine);
        SetTrailActive(false);
        throwRoutine = StartCoroutine(ThrowRoutine(direction.normalized, duration));
    }

    private IEnumerator ThrowRoutine(Vector2 direction, float duration)
    {
        if (direction.sqrMagnitude <= 0.0001f) direction = Vector2.right;

        float safeDuration = Mathf.Max(0.01f, duration);
        float pullPhase = 0.58f;
        float pulledReach = -armReach * 0.35f;
        float releaseReach = armReach * 1.85f;
        float elapsed = 0f;
        bool trailStarted = false;
        SetArmPose(direction, armReach);

        while (elapsed < safeDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / safeDuration);
            if (progress < pullPhase)
            {
                float pullProgress = Mathf.SmoothStep(0f, 1f, progress / pullPhase);
                SetArmPose(direction, Mathf.Lerp(armReach, pulledReach, pullProgress));
            }
            else
            {
                if (!trailStarted)
                {
                    SetTrailActive(true);
                    trailStarted = true;
                }

                float throwProgress = Mathf.SmoothStep(0f, 1f, (progress - pullPhase) / (1f - pullPhase));
                SetArmPose(direction, Mathf.Lerp(pulledReach, releaseReach, throwProgress));
            }

            yield return null;
        }

        SetTrailActive(false);
        SetArmPose(direction);
        throwRoutine = null;
    }

    private IEnumerator SwingRoutine(float windup, float duration, Vector2 direction, float arc, float knockbackForce, bool pierce, bool swingBackToRest, bool recoverAfterSwing, float recoverDuration)
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
        SetTrailActive(true);
        hitCollider.enabled = true;
        CheckHitsInCurrentArea();
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            float angle = Mathf.Lerp(startAngle, endAngle, progress);
            SetArmPose(AngleToDirection(angle));
            CheckHitsInCurrentArea();
            yield return null;
        }

        hitCollider.enabled = false;
        SetTrailActive(false);
        if (recoverAfterSwing)
        {
            holdingComboPose = false;
            activeReach = armReach;
            yield return RecoverRoutine(direction, Mathf.Max(0.01f, recoverDuration));
            swingRoutine = null;
            yield break;
        }

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
        hitCollider.size = GetHitColliderSize();
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
            EnsureWeaponTrail();
            UpdateTrailColor();
            PositionTrailAtWeaponTip();
            return;
        }

        if (weaponRenderer != null) weaponRenderer.enabled = false;
        if (spriteRenderer != null)
        {
            EnsureSprite();
            spriteRenderer.drawMode = SpriteDrawMode.Sliced;
            spriteRenderer.size = GetHitColliderSize();
            spriteRenderer.color = GetDisplayColor(activeColor);
            spriteRenderer.sortingOrder = 10;
            spriteRenderer.enabled = true;
        }

        EnsureWeaponTrail();
        UpdateTrailColor();
        PositionTrailAtWeaponTip();
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
        SetTrailActive(true);
        hitCollider.enabled = true;
        CheckHitsInCurrentArea();
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            SetArmPose(direction, Mathf.Lerp(pulledReach, thrustReach, progress));
            CheckHitsInCurrentArea();
            yield return null;
        }

        hitCollider.enabled = false;
        SetTrailActive(false);
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

    private void EnsureWeaponTrail()
    {
        if (weaponTrail != null) return;

        GameObject trail = new("WeaponTrail");
        trail.transform.SetParent(transform, false);
        trail.layer = gameObject.layer;
        weaponTrail = trail.AddComponent<TrailRenderer>();
        weaponTrail.time = trailTime;
        weaponTrail.minVertexDistance = 0.01f;
        weaponTrail.startWidth = trailStartWidth;
        weaponTrail.endWidth = trailEndWidth;
        weaponTrail.numCapVertices = 4;
        weaponTrail.numCornerVertices = 4;
        weaponTrail.autodestruct = false;
        weaponTrail.emitting = false;
        weaponTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        weaponTrail.receiveShadows = false;
        weaponTrail.sortingOrder = 12;

        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Default");
        if (shader != null)
        {
            trailMaterial = new Material(shader) { name = "Runtime Weapon Trail" };
            weaponTrail.material = trailMaterial;
        }
    }

    private void PositionTrailAtWeaponTip()
    {
        if (weaponTrail == null) return;

        weaponTrail.transform.localPosition = HasWeaponSprite
            ? Vector3.up * (size.x * 0.52f)
            : Vector3.right * (size.x * 0.52f);
        weaponTrail.transform.localRotation = Quaternion.identity;
        weaponTrail.transform.localScale = Vector3.one;
    }

    private void SetTrailActive(bool active)
    {
        EnsureWeaponTrail();
        if (weaponTrail == null) return;

        PositionTrailAtWeaponTip();
        UpdateTrailColor();
        if (active)
        {
            weaponTrail.Clear();
        }

        weaponTrail.emitting = active;
    }

    private void UpdateTrailColor()
    {
        if (weaponTrail == null) return;

        Color color = GetDisplayColor(activeColor);
        color.a = 0.88f;
        Color softColor = Color.Lerp(color, Color.white, 0.35f);
        softColor.a = 0.26f;

        Gradient gradient = new();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(color, 0.45f),
                new GradientColorKey(softColor, 1f),
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.68f, 0.55f),
                new GradientAlphaKey(0.03f, 1f),
            });

        weaponTrail.time = trailTime;
        weaponTrail.startWidth = trailStartWidth;
        weaponTrail.endWidth = trailEndWidth;
        weaponTrail.colorGradient = gradient;
    }

    private Vector2 GetHitColliderSize()
    {
        Vector2 baseSize = HasWeaponSprite ? new Vector2(size.y, size.x) : size;
        Vector2 calculatedSize = HasWeaponSprite
            ? new Vector2(baseSize.x * hitboxThicknessMultiplier, baseSize.y * hitboxLengthMultiplier)
            : new Vector2(baseSize.x * hitboxLengthMultiplier, baseSize.y * hitboxThicknessMultiplier);

        // Make every melee attack larger, then give Blue an additional boost.
        // Mathf.Max keeps the requested minimum boost even on already-existing prefabs/scenes.
        float overallScale = Mathf.Max(1.30f, overallAttackHitboxScale);
        float colorScale = activeColor == ColorType.Blue
            ? Mathf.Max(1.25f, blueAttackHitboxScale)
            : 1f;

        return calculatedSize * overallScale * colorScale;
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

    // Directly checks every collider inside the current weapon box.
    // This is intentionally kept in addition to Trigger callbacks so very short/fast
    // attacks can still hit every enemy in the area.
    private void CheckHitsInCurrentArea()
    {
        if (!useDirectOverlapChecks || !IsSwinging || hitCollider == null || !hitCollider.enabled) return;

        Vector2 center = transform.TransformPoint(hitCollider.offset);
        Vector3 lossyScale = transform.lossyScale;
        Vector2 worldSize = new(
            hitCollider.size.x * Mathf.Abs(lossyScale.x),
            hitCollider.size.y * Mathf.Abs(lossyScale.y));
        float angle = transform.eulerAngles.z;

        Collider2D[] overlaps = Physics2D.OverlapBoxAll(center, worldSize, angle);
        for (int i = 0; i < overlaps.Length; i++)
        {
            TryHit(overlaps[i]);
        }
    }

    private void TryHit(Collider2D other)
    {
        if (!IsSwinging || other == null) return;

        Enemy enemy = other.GetComponentInParent<Enemy>();
        if (enemy == null || enemy.pattern == null || enemy.pattern.Count == 0 || hitEnemies.Contains(enemy)) return;

        bool correctColor =
            enemy.pattern[0] == activeColor;

        // Enemy.OnHit returns true for any real contact, even when the color is wrong.
        // Therefore wrong-color hits still consume this enemy once for this attack,
        // while health reduction is handled separately inside Enemy.
        if (!enemy.OnHit(activeColor, transform.position, activeKnockbackForce)) return;

        Player.Instance?.PlayWeaponHitSfx(
            activeColor,
            correctColor
        );

        // Area attack rule: each enemy can be hit once per attack, but hitting one
        // enemy NEVER turns the hitbox off. Every other enemy in the area is processed.
        hitEnemies.Add(enemy);
    }

    private void EnsureSprite()
    {
        if (fallbackHitboxSprite == null)
        {
            Texture2D texture = Texture2D.whiteTexture;
            fallbackHitboxSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                texture.width,
                0,
                SpriteMeshType.FullRect);
        }

        spriteRenderer.sprite = fallbackHitboxSprite;
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
