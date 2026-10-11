using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager instance;

    [SerializeField] private float sfxMinimumDistance;
    [SerializeField] private AudioSource[] sfx;
    [SerializeField] private AudioSource[] bgm;

    [SerializeField] private bool playBgm;
    [SerializeField] private int bgmIndex;

    private bool canPlaySFX;

    private void Awake()
    {
        // 出现重复实例时把旧的那个整个销毁，然后立刻把 static 指向自己。
        // 不写那句 instance = this 的话，静态字段会继续指着一个已经销毁的对象，
        // 之后所有 AudioManager.instance.XXX 都会在死对象上执行。
        if (instance != null && instance != this)
            Destroy(instance.gameObject);

        instance = this;

        Invoke("AllowSFX", .1f);
    }

    private void Update()
    {
        if (!playBgm)
            StopAllBGM();
        else
        {
            if (!bgm[bgmIndex].isPlaying)
                PlayBGM(bgmIndex);
        }
    }

    public void PlaySFX(int _sfxIndex, Transform _source)
    {
        if (!canPlaySFX) return;

        // 数组没接、索引越界、或者这一格是空的，都直接跳过，别让一个缺音源把整局打断
        if (sfx == null || _sfxIndex < 0 || _sfxIndex >= sfx.Length) return;
        if (sfx[_sfxIndex] == null) return;

        if (_source != null && Vector2.Distance(PlayerManager.instance.player.transform.position, _source.position) > sfxMinimumDistance)
            return;

        /*
        if (sfx[_sfxIndex].isPlaying)
            return;
        */

        sfx[_sfxIndex].pitch = Random.Range(.85f, 1.1f);
        sfx[_sfxIndex].Play();
    }

    public void StopSFX(int _index) => sfx[_index].Stop();

    /// <summary>
    /// 拿到某个下标的音效片段。给需要自己控制播放/停止的地方用
    /// （比如点燃音效要跟着负面效果一起停，不能占用 AudioManager 上公用的那条 AudioSource）。
    /// </summary>
    public AudioClip GetSFXClip(int _sfxIndex)
    {
        if (sfx == null || _sfxIndex < 0 || _sfxIndex >= sfx.Length || sfx[_sfxIndex] == null)
            return null;

        return sfx[_sfxIndex].clip;
    }


    public void StopSFXCoroutine(int _index) => StopCoroutine(DecreaseVolume(sfx[_index]));
    public void StopSFXWithTime(int _index) => StartCoroutine(DecreaseVolume(sfx[_index]));

    private IEnumerator DecreaseVolume(AudioSource _audio)
    {
        float defaultVolume = _audio.volume;

        while (_audio.volume > .1f)
        {
            _audio.volume -= _audio.volume * .2f;
            yield return new WaitForSeconds(.6f);

            if (_audio.volume <= .1f)
            {
                _audio.Stop();
                _audio.volume = defaultVolume;
                break;
            }
        }

    }

    public void PlayRandomBGM()
    {
        bgmIndex = Random.Range(0, bgm.Length);
        PlayBGM(bgmIndex);
    }

    public void PlayBGM(int _index)
    {
        bgmIndex = _index;

        StopAllBGM();
        bgm[bgmIndex].Play();
    }

    public void StopAllBGM()
    {
        for (int i = 0; i < bgm.Length; i++)
        {
            bgm[i].Stop();
        }
    }

    private void AllowSFX() => canPlaySFX = true;
}
