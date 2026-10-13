using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Arrow_Controller : MonoBehaviour
{
    [SerializeField] private int damage;
    [SerializeField] private string targetLayerName = "Player";

    [SerializeField] private float xVelocity;
    [SerializeField] private Rigidbody2D rb;

    [SerializeField] private bool canMove;
    [SerializeField] private bool flipped;

    private CharacterStats myStats;

    private void Update()
    {
        if (canMove)
            rb.velocity = new Vector2(xVelocity,rb.velocity.y);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if(collision.gameObject.layer == LayerMask.NameToLayer(targetLayerName))
        {
            // 箭命中玩家（42）
            if (AudioManager.instance != null)
                AudioManager.instance.PlaySFX(42, transform);

            //collision.GetComponent<CharacterStats>()?.TakeDamage(damage);
            myStats.DoDamage(collision.GetComponent<CharacterStats>());
            StuckInto(collision);
        }
        else if(collision.gameObject.layer == LayerMask.NameToLayer("Ground"))
        {
            // 箭命中地面/墙壁（42）
            if (AudioManager.instance != null)
                AudioManager.instance.PlaySFX(42, transform);

            StuckInto(collision);
        }
    }

    public void SetupArrow(float _speed, CharacterStats _myStats, int _facingDir = 1)
    {
        // 朝左生成的箭矢飞行速度取负，同时把整个箭矢（贴图 + 拖尾）也转到左边
        xVelocity = Mathf.Abs(_speed) * (_facingDir < 0 ? -1f : 1f);
        myStats= _myStats;

        transform.rotation = Quaternion.Euler(0, _facingDir < 0 ? 180f : 0f, 0);
    }

    private void StuckInto(Collider2D collision)
    {
        GetComponentInChildren<ParticleSystem>().Stop();
        GetComponent<CapsuleCollider2D>().enabled = false;
        canMove = false;
        rb.isKinematic = true;
        rb.constraints = RigidbodyConstraints2D.FreezeAll;
        transform.parent = collision.transform;

        Destroy(gameObject, Random.Range(5, 7));
    }

    public void FlipArrow()
    {
        if (flipped) return;

        xVelocity *= -1;
        flipped = true;
        transform.Rotate(0, 180, 0);
        targetLayerName = "Enemy";
    }
}
