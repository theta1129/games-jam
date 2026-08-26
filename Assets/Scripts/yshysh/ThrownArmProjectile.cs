using UnityEngine;

public sealed class ThrownArmProjectile : MonoBehaviour
{
    private static readonly Vector2 VisualSize =
        new(0.45f, 1.25f);

    private static Sprite fallbackProjectileSprite;


    private Vector2 direction;

    private float speed;

    private float stunDuration;

    private Sprite weaponSprite;


    // =========================================================
    // Create
    // =========================================================

    public static void Create(
        Vector2 position,
        Vector2 direction,
        float speed,
        float stunDuration,
        Sprite weaponSprite
    )
    {
        GameObject projectile =
            new(
                "Yellow Thrown Arm"
            );


        projectile.transform.position =
            position;


        ThrownArmProjectile attack =
            projectile.AddComponent<ThrownArmProjectile>();


        attack.Initialize(
            direction,
            speed,
            stunDuration,
            weaponSprite
        );
    }


    // =========================================================
    // Initialize
    // =========================================================

    private void Initialize(
        Vector2 startDirection,
        float startSpeed,
        float startStunDuration,
        Sprite startWeaponSprite
    )
    {
        direction =
            startDirection.sqrMagnitude >
            0.0001f

                ? startDirection.normalized
                : Vector2.right;


        speed =
            startSpeed;


        stunDuration =
            startStunDuration;


        weaponSprite =
            startWeaponSprite;


        ConfigureVisuals();
    }


    // =========================================================
    // Visual
    // =========================================================

    private void ConfigureVisuals()
    {
        Rigidbody2D rb =
            gameObject.AddComponent<Rigidbody2D>();


        rb.bodyType =
            RigidbodyType2D.Kinematic;


        rb.gravityScale =
            0f;


        rb.freezeRotation =
            true;


        BoxCollider2D collider =
            gameObject.AddComponent<BoxCollider2D>();


        collider.isTrigger =
            true;


        collider.size =
            VisualSize;


        if (weaponSprite != null)
        {
            SpriteRenderer renderer =
                CreateWeaponRenderer();


            renderer.sprite =
                weaponSprite;


            renderer.color =
                Color.white;


            renderer.sortingOrder =
                10;


            FitWeaponVisual(
                renderer
            );
        }
        else
        {
            SpriteRenderer renderer =
                gameObject.AddComponent<SpriteRenderer>();


            renderer.sprite =
                GetFallbackProjectileSprite();


            renderer.drawMode =
                SpriteDrawMode.Sliced;


            renderer.size =
                collider.size;


            renderer.color =
                new Color(
                    1f,
                    0.85f,
                    0.1f,
                    0.75f
                );


            renderer.sortingOrder =
                10;
        }


        transform.up =
            direction;
    }


    private SpriteRenderer CreateWeaponRenderer()
    {
        GameObject visual =
            new(
                "WeaponVisual"
            );


        visual.transform.SetParent(
            transform,
            false
        );


        visual.layer =
            gameObject.layer;


        return
            visual.AddComponent<SpriteRenderer>();
    }


    private void FitWeaponVisual(
        SpriteRenderer renderer
    )
    {
        float sourceLength =
            Mathf.Max(
                weaponSprite.bounds.size.y,
                0.0001f
            );


        float visualScale =
            VisualSize.y /
            sourceLength;


        Transform visualTransform =
            renderer.transform;


        visualTransform.localRotation =
            Quaternion.identity;


        visualTransform.localScale =
            Vector3.one *
            visualScale;


        visualTransform.localPosition =
            -(Vector3)(
                weaponSprite.bounds.center *
                visualScale
            );
    }


    // =========================================================
    // Fallback
    // =========================================================

    private static Sprite GetFallbackProjectileSprite()
    {
        if (
            fallbackProjectileSprite ==
            null
        )
        {
            Texture2D texture =
                Texture2D.whiteTexture;


            fallbackProjectileSprite =
                Sprite.Create(

                    texture,

                    new Rect(
                        0f,
                        0f,
                        texture.width,
                        texture.height
                    ),

                    new Vector2(
                        0.5f,
                        0.5f
                    ),

                    texture.width,

                    0,

                    SpriteMeshType.FullRect

                );
        }


        return
            fallbackProjectileSprite;
    }


    // =========================================================
    // Update
    // =========================================================

    private void Update()
    {
        transform.position +=
            (Vector3)(
                direction *
                speed *
                Time.deltaTime
            );


        transform.up =
            direction;
    }


    // =========================================================
    // Hit
    // =========================================================

    private void OnTriggerEnter2D(
        Collider2D other
    )
    {
        Enemy enemy =
            other.GetComponentInParent<Enemy>();


        if (enemy == null)
        {
            return;
        }


        // 노란색 공격이 실제로 성공했을 때만
        if (
            enemy.OnHit(
                ColorType.Yellow,
                transform.position,
                0f
            )
        )
        {
            // ==========================================
            // 우클릭 투척 추가 스턴
            // ==========================================

            enemy.ApplyYellowThrowStun(
                stunDuration
            );
        }


        Destroy(gameObject);
    }


    private void OnBecameInvisible()
    {
        Destroy(gameObject);
    }
}