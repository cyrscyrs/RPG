using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerFX : EntityFX
{
    [Header("Screen shake FX")]
    [SerializeField] private float shakeMultipler;
    private CinemachineImpulseSource screenShake;
    public Vector3 shake_catchSword;
    public Vector3 shake_getHighDamage;

    [Header("After image fx")]
    [SerializeField] private float afterImageCooldown;
    [SerializeField] private GameObject afterImagePrefab;
    [SerializeField] private float colorLooseRate;
    private float afterImageCooldownTimer;

    [Space]
    [SerializeField] private ParticleSystem dustFx;

    protected override void Start()
    {
        base.Start();
        screenShake = GetComponent<CinemachineImpulseSource>();
    }
    private void Update()
    {
        afterImageCooldownTimer -= Time.deltaTime;
    }

    
    public void CreateAfterImage()
    {
        if (afterImageCooldownTimer < 0)
        {
            afterImageCooldownTimer = afterImageCooldown;
            GameObject newAfterImage = Instantiate(afterImagePrefab, transform.position, transform.rotation);
            newAfterImage.GetComponent<AfterImageFX>().SetupAfterImage(colorLooseRate, sr.sprite);
        }
    }
    public void ScreenShake(Vector3 _shakePower)
    {
        if (screenShake == null)
            return;

        screenShake.m_DefaultVelocity = new Vector3(_shakePower.x * player.facingDir, _shakePower.y) * shakeMultipler;
        screenShake.GenerateImpulse();
    }
    public void PlayDustFx()
    {
        if (dustFx != null)
            dustFx.Play();
    }
}
