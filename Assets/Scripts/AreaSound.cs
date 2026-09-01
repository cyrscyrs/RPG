using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AreaSound : MonoBehaviour
{
    [SerializeField] private int areaSoundIndex;
    [SerializeField] private AudioSource areaSound;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.GetComponent<Player>() != null)
        {
            AudioManager.instance.PlaySFX(areaSoundIndex, null);

            /*
            StopCoroutine("StopSoundSlowly");
            areaSound.volume = 1;
            areaSound.Play();
            */
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.GetComponent<Player>() != null)
        {
            AudioManager.instance.StopSFXWithTime(areaSoundIndex);
            //StartCoroutine(StopSoundSlowly());
        }
    }

    private IEnumerator StopSoundSlowly()
    {
        float defaultVolume = areaSound.volume;

        while (areaSound.volume > .1f)
        {
            areaSound.volume -= areaSound.volume * .2f;
            yield return new WaitForSeconds(.6f);

            if (areaSound.volume <= .1f)
            {
                areaSound.Stop();
                areaSound.volume = defaultVolume;
                break;
            }
        }
    }

}
