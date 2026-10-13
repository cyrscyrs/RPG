using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class EntityFX : MonoBehaviour
{
    protected Player player;
    protected SpriteRenderer sr;

    [Header("Pop up text")]
    [SerializeField] private GameObject popUpTextPrefabs;

    [Tooltip("弹出文字左右随机散布的范围（正负这么多格）。数字密的时候才不会叠成一坨")]
    [SerializeField] private float popUpRandomX = 1.8f;

    [Tooltip("弹出文字的高度随机范围（x = 最低，y = 最高）")]
    [SerializeField] private Vector2 popUpRandomY = new Vector2(1.6f, 6f);




    [Header("Flash FX")]
    [SerializeField] private float flashDuration;
    [SerializeField] private Material hitMat;
    private Material originalMat;

    [Header("Ailment colors")]
    [SerializeField] private Color chilledColor;
    [SerializeField] private Color[] ignitedColor;
    [SerializeField] private Color[] shockedColor;

    [Header("Ailment FX")]
    [SerializeField] private ParticleSystem IgniteFX;
    [SerializeField] private ParticleSystem ChillFX;
    [SerializeField] private ParticleSystem ShockFX;

    [Header("Hit fx")]
    [SerializeField] private GameObject hitFx;
    [SerializeField] private GameObject criticalHitFx;

    [Header("点燃音效")]
    [Tooltip("点燃状态循环播放的音效下标（AudioManager.sfx），要和负面效果一起停")]
    [SerializeField] private int igniteSfxIndex = 4;

    // 点燃音效用自己的 AudioSource 播：共用 AudioManager 上那一条的话，
    // 没地方记「是谁在烧」，负面效果结束后就停不下来（会一直循环到下一次被重新播）。
    private AudioSource igniteSfxSource;

    private GameObject myHealthBar;

    [Header("血条")]
    [Tooltip("勾上就不在头顶显示血条（比如 Boss 改用屏幕下方的大血条）")]
    [SerializeField] private bool hideHealthBar;


    protected virtual void Start()
    {
        sr = GetComponentInChildren<SpriteRenderer>();
        originalMat = sr.material;
        player = PlayerManager.instance.player;

        // The status UI stays inactive until it is needed, and GetComponentInChildren
        // skips inactive children unless includeInactive is true.
        UI_HealthBar healthBar = GetComponentInChildren<UI_HealthBar>(true);

        if (healthBar != null)
            myHealthBar = healthBar.gameObject;
        else
            Debug.LogWarning(name + " has no UI_HealthBar in its children.", this);

        if (hideHealthBar && myHealthBar != null)
            myHealthBar.SetActive(false);
    }


    public void CreatePopUpText(string _text)
    {
        CreatePopUpText(_text, Color.white);
    }

    /// <summary>带颜色的弹出文字（伤害数字按属性区分颜色时用）。</summary>
    public void CreatePopUpText(string _text, Color _color)
    {
        float randomX = Random.Range(-popUpRandomX, popUpRandomX);
        float randomY = Random.Range(popUpRandomY.x, Mathf.Max(popUpRandomY.x + 0.01f, popUpRandomY.y));

        Vector3 offset = new Vector3(randomX, randomY, 0);

        GameObject newText = Instantiate(popUpTextPrefabs, transform.position + offset, Quaternion.identity);

        TextMeshPro text = newText.GetComponent<TextMeshPro>();
        text.text = _text;
        text.color = _color;
    }



    public void MakeTransparent(bool _transparent)
    {
        if (myHealthBar != null)
            myHealthBar.SetActive(!_transparent && !hideHealthBar);

        sr.color = _transparent ? Color.clear : Color.white;
    }

    private IEnumerator FlashFX()
    {
        sr.material = hitMat;

        yield return new WaitForSeconds(flashDuration);

        sr.material = originalMat;
    }

    private void RedColorBlink()
    {
        if(sr.color!= Color.white)
            sr.color = Color.white;
        else
            sr.color = Color.red;
    }

    public void IgnitedFxFor(float _seconds)
    {
        IgniteFX.Play();
        PlayIgniteSfx();

        InvokeRepeating("IgnitedColorFX", 0, Tuning.Config.ailmentBlinkInterval);
        Invoke("CancelColorChange", _seconds);
    }
    public void ChilledFxFor(float _seconds)
    {
        ChillFX.Play();
        
        sr.color = chilledColor;
        Invoke("CancelColorChange", _seconds);
    }
    public void ShockedFxFor(float _seconds)
    {
        ShockFX.Play();

        InvokeRepeating("ShockedColorFX", 0, Tuning.Config.ailmentBlinkInterval);
        Invoke("CancelColorChange", _seconds);
    }

    private void IgnitedColorFX()
    {
        if (sr.color != ignitedColor[0])
            sr.color = ignitedColor[0];
        else
            sr.color = ignitedColor[1];
    }
    private void ShockedColorFX()
    {
        if(sr.color != shockedColor[0])
            sr.color = shockedColor[0];
        else
            sr.color = shockedColor[1];
    }

    private void CancelColorChange()
    {
        CancelInvoke();
        sr.color = Color.white;

        // 负面效果结束：点燃音效也跟着停，不要继续循环
        StopIgniteSfx();

        IgniteFX.Stop();
        ChillFX.Stop();
        ShockFX.Stop();
    }

    /// <summary>开始播放点燃音效（用实体自己的 AudioSource，方便单独停）。</summary>
    private void PlayIgniteSfx()
    {
        if (AudioManager.instance == null)
            return;

        AudioClip clip = AudioManager.instance.GetSFXClip(igniteSfxIndex);

        if (clip == null)
            return;

        if (igniteSfxSource == null)
        {
            igniteSfxSource = gameObject.AddComponent<AudioSource>();
            igniteSfxSource.playOnAwake = false;
            igniteSfxSource.spatialBlend = 0f;   // 和 AudioManager 里的 SFX 一样按 2D 播
            igniteSfxSource.loop = true;         // 烧多久循环多久
        }

        igniteSfxSource.pitch = Random.Range(.9f, 1.05f);
        igniteSfxSource.clip = clip;
        igniteSfxSource.Play();
    }

    /// <summary>停掉点燃音效。</summary>
    private void StopIgniteSfx()
    {
        if (igniteSfxSource != null && igniteSfxSource.isPlaying)
            igniteSfxSource.Stop();
    }

    public void CreateHitFX(Transform _target,bool _critical)
    {
        float zRotation = Random.Range(-90, 90);
        float xPosition = Random.Range(-.5f, .5f);
        float yPosition = Random.Range(-.5f, .5f);

        Vector3 hitFxRotation = new Vector3(0, 0, zRotation);

        GameObject hitPrefabs = hitFx;

        if (_critical)
        {
            hitPrefabs = criticalHitFx;
            
            float yRotation = 0;
            zRotation = Random.Range(-45, 45);

            if (GetComponent<Entity>().facingDir == -1)
                yRotation = 180;

            hitFxRotation = new Vector3(0, yRotation, zRotation);
        }

        GameObject newHitFx = Instantiate(hitPrefabs, _target.position + new Vector3(xPosition, yPosition), Quaternion.identity);

        newHitFx.transform.Rotate(hitFxRotation);

        Destroy(newHitFx, .5f);
    }



}
