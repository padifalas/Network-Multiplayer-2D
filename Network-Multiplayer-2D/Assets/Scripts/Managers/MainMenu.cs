using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject creditsPanel;

    [Header("Scene Names")]
    [SerializeField] private string lobbySceneName = "Lobby";


    private void Start()
    {
       
        ShowMain();
        AudioManager.Singleton?.PlayMenuMusic();
    }




    public void OnStartPressed()
    {
        SceneManager.LoadScene(lobbySceneName);
    }

    public void OnSettingsPressed()
    {
        mainPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    public void OnCreditsPressed()
    {
        mainPanel.SetActive(false);
        creditsPanel.SetActive(true);
    }

    public void OnBackPressed()
    {
        ShowMain();
    }

    public void OnQuitPressed()
    {
        Application.Quit();

       
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }


 

    private void ShowMain()
    {
        if (mainPanel)mainPanel.SetActive(true);
        if (settingsPanel) settingsPanel.SetActive(false);
        if (creditsPanel)  creditsPanel.SetActive(false);
    }
}