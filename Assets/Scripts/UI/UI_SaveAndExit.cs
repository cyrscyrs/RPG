using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UI_SaveAndExit : MonoBehaviour
{
    public void SaveAndExit()
    {
        SaveManager.instance.SaveGame();
        Invoke("turn",.5f);
    }

    public void Turn()
    {
        SceneManager.LoadScene("MainMenu");
    }
}
