using UnityEngine;

public sealed class ThrownArmProjectile : MonoBehaviour
{
    private Vector2 direction;
    private float speed;
    private float stunDuration;
    private Sprite weaponSprite;

    public static void Create(Vector2 position, Vector2 direction, float speed, float stunDuration, Sprite weaponSprite)
    {
        GameObject projectile = new("Yellow Thrown Arm");
        projectile.transform.position = position;
        ThrownArmProjectile attack = projectile.AddComponent<ThrownArmProjectile>();
        attack.direction = direction;
        attack.speed = speed;
        attack.stunDuration = stunDuration;
        attack.weaponSprite = weaponSprite;
    }

    private void Awake()
    {
        BoxCollider2D collider = gameObject.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(1.25f, 0.45f);

        SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
        if (weaponSprite != null)
        {
            renderer.sprite = Sprite.Create(weaponSprite.texture, weaponSprite.textureRect, new Vector2(0.5f, 0.5f), weaponSprite.pixelsPerUnit);
            collider.size = new Vector2(0.45f, 1.25f);
        }
        else
        {
            Texture2D texture = Texture2D.whiteTexture;
            renderer.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = collider.size;
        }
        renderer.color = new Color(1f, 0.85f, 0.1f, 0.75f);
        renderer.sortingOrder = 10;
    }

    private void Update()
    {
        transform.position += (Vector3)(direction * speed * Time.deltaTime);
        transform.right = weaponSprite != null ? Vector2.Perpendicular(direction) : direction;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Enemy enemy = other.GetComponentInParent<Enemy>();
        if (enemy == null) return;

        if (enemy.OnHit(ColorType.Yellow, transform.position, 0f))
        {
            enemy.Stun(stunDuration);
        }
        Destroy(gameObject);
    }

    private void OnBecameInvisible() => Destroy(gameObject);
}
