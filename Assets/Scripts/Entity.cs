using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Entity : MonoBehaviour
{

    [Header("Collison info")]

    public Transform attackCheck;
    public float attackCheckRadius = 1.2f;
    [SerializeField] protected Transform groundCheck;
    [SerializeField] protected float groundCheckDistance = 1;
    [SerializeField] protected Transform wallCheck;
    [SerializeField] protected float wallCheckDistance = .8f;
    [SerializeField] protected LayerMask whatIsGround;

    [Header("Knockback info")]
    [SerializeField] protected Vector2 knockbackPower = new Vector2(7, 12);
    [SerializeField] protected Vector2 knockbackOffset = new Vector2(.5f, 2);
    [SerializeField] protected float knockbackDuration = .07f;

    protected bool isKnocked;
    public int knockbackDir { get; private set; }

    #region Components
    public SpriteRenderer sr { get; private set; }
    public Animator anim { get; private set; }
    public Rigidbody2D rb { get; private set; }
    public CharacterStats stats { get; private set; }
    public CapsuleCollider2D cd { get; private set; }

    #endregion

    public int facingDir { get; private set; } = 1;
    protected bool facingRight = true;

    public System.Action onFlipped;

    protected virtual void Awake()
    {

    }
    protected virtual void Start()
    {
        sr = GetComponentInChildren<SpriteRenderer>();
        anim = GetComponentInChildren<Animator>();
        rb = GetComponent<Rigidbody2D>();
        stats = GetComponentInChildren<CharacterStats>();
        cd = GetComponent<CapsuleCollider2D>();
    }

    // Update is called once per frame
    protected virtual void Update()
    {
        
    }

    public virtual void SetupKnockbackDir(Transform _damager)
    {
        if (_damager.transform.position.x > transform.position.x)
            knockbackDir = -1;
        else if (_damager.transform.position.x < transform.position.x)
            knockbackDir = 1;
    }

    protected virtual void SetupZeroKnockbackPower()
    {

    }

    public void SetupKncokbackPower(Vector2 _knockbackPower) => knockbackPower = _knockbackPower;

    public virtual void SlowEntityBy(float _slowPercentage, float _slowDuration)
    {

    }

    public virtual void ReturnDefaultSpeed()
    {
        anim.speed = 1;
    }

    public virtual void DamegedEffect()
    {
        StartCoroutine("HitKnockback");
    }

    protected virtual IEnumerator HitKnockback()
    {
        isKnocked = true;

        float xOffset = Random.Range(knockbackOffset.x, knockbackOffset.y);

        //if(knockbackPower.x > 0 || knockbackPower.y > 0) //不加if语句可以增加人物受击硬直效果
        rb.velocity = new Vector2((knockbackPower.x + xOffset) * knockbackDir, knockbackPower.y);

        yield return new WaitForSeconds(knockbackDuration);

        isKnocked = false;
    }

    #region Velocity

    public void SetZeroVelocity() => SetVelocity(0,0);

    public void SetVelocity(float _xVelocity, float _yVelocity)
    {
        if (isKnocked)
            return;

        rb.velocity = new Vector2(_xVelocity, _yVelocity);
        FlipController(_xVelocity);
    }

    #endregion

    #region Collision

    public virtual bool isGroundDetected() => Physics2D.Raycast(groundCheck.position,
        Vector2.down, groundCheckDistance, whatIsGround);
    public virtual bool isWallDectected() => Physics2D.Raycast(wallCheck.position,
        Vector2.right * facingDir, wallCheckDistance, whatIsGround);

    protected virtual void OnDrawGizmos()
    {
        Gizmos.DrawLine(groundCheck.position, new Vector3(groundCheck.position.x,
            groundCheck.position.y - groundCheckDistance));
        Gizmos.DrawLine(wallCheck.position, new Vector3(wallCheck.position.x + wallCheckDistance * facingDir,
            wallCheck.position.y));
        Gizmos.DrawWireSphere(attackCheck.position, attackCheckRadius);
    }

    #endregion

    #region Flip

    public virtual void Flip()
    {
        facingDir = facingDir * -1;
        facingRight = !facingRight;
        transform.Rotate(0, 180, 0);

        onFlipped?.Invoke();
    }

    public virtual void FlipController(float _x)
    {
        if (_x > 0 && !facingRight)
            Flip();
        else if (_x < 0 && facingRight)
            Flip();
    }

    public virtual void SetupDefaultFacingDir(int _direction)
    {
        facingDir = _direction;
        if (facingDir == -1)//朝左
            facingRight = false;
    }

    #endregion



    public virtual void Die()
    {

    }
}
