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

    private GameObject myHealthBar;


    protected virtual void Start()
    {
        sr = GetComponentInChildren<SpriteRenderer>();
        originalMat = sr.material;
        player = PlayerManager.instance.player;

        myHealthBar = GetComponentInChildren<UI_HealthBar>().gameObject;
    }


    public void CreatePopUpText(string _text)
    {
        float randomX = Random.Range(-1, 1);
        float randomY = Random.Range(1.5f, 5);

        Vector3 offset = new Vector3(randomX, randomY, 0);

        GameObject newText = Instantiate(popUpTextPrefabs, transform.position + offset, Quaternion.identity);

        newText.GetComponent<TextMeshPro>().text = _text;
    }



    public void MakeTransparent(bool _transparent)
    {
        if (_transparent)
        {
            myHealthBar.SetActive(false);
            sr.color = Color.clear;
        }
        else
        {
            myHealthBar.SetActive(true);
            sr.color = Color.white;
        }
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

        InvokeRepeating("IgnitedColorFX", 0, .3f);
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

        InvokeRepeating("ShockedColorFX", 0, .3f);
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

        IgniteFX.Stop();
        ChillFX.Stop();
        ShockFX.Stop();
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
