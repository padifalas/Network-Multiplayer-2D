using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [Header("Canvas's")]
    [SerializeField] private GameObject mainCanvasGO;
    [SerializeField] private GameObject howToPlayCanvasGO;
    [SerializeField] private GameObject settingsCanvasGO;
    [SerializeField] private GameObject trapsCanvasGO;
    [SerializeField] private GameObject creditsCanvasGO;

    [Header("First Selected Options")]
    [SerializeField] private GameObject startGameFirst;
    [SerializeField] private GameObject howToPlayFirst;
    [SerializeField] private GameObject settingsFirst;
    [SerializeField] private GameObject trapsFirst;
    [SerializeField] private GameObject creditsFirst;


    private void Start()
    {

        mainCanvasGO.SetActive(true);
        howToPlayCanvasGO.SetActive(false);
        settingsCanvasGO.SetActive(false);
        trapsCanvasGO.SetActive(false);
        creditsCanvasGO.SetActive(false);

        EventSystem.current.SetSelectedGameObject(startGameFirst);

        AudioManager.Singleton?.PlayMenuMusic();
    }

    #region Scene Loader
    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
        Debug.Log($"Loading scene: {sceneName}");
    }
    #endregion

    #region Canvas Activations/Deactivations

    private void OpenMainMenu()
    {
        mainCanvasGO.SetActive(true);
        howToPlayCanvasGO.SetActive(false);
        settingsCanvasGO.SetActive(false);
        trapsCanvasGO.SetActive(false);
        creditsCanvasGO.SetActive(false);

        EventSystem.current.SetSelectedGameObject(startGameFirst);

    }

    private void OpenHowToPlay()
    {
        mainCanvasGO.SetActive(false);
        howToPlayCanvasGO.SetActive(true);
        settingsCanvasGO.SetActive(false);
        trapsCanvasGO.SetActive(false);
        creditsCanvasGO.SetActive(false);

        EventSystem.current.SetSelectedGameObject(howToPlayFirst);
    }

    private void OpenSettings()
    {
        mainCanvasGO.SetActive(false);
        howToPlayCanvasGO.SetActive(false);
        settingsCanvasGO.SetActive(true);
        trapsCanvasGO.SetActive(false);
        creditsCanvasGO.SetActive(false);

        EventSystem.current.SetSelectedGameObject(settingsFirst);
    }

    private void OpenTraps()
    {
        mainCanvasGO.SetActive(false);
        howToPlayCanvasGO.SetActive(false);
        settingsCanvasGO.SetActive(false);
        trapsCanvasGO.SetActive(true);
        creditsCanvasGO.SetActive(false);

        EventSystem.current.SetSelectedGameObject(trapsFirst);
    }

    private void OpenCredits()
    {
        mainCanvasGO.SetActive(false);
        howToPlayCanvasGO.SetActive(false);
        settingsCanvasGO.SetActive(false);
        trapsCanvasGO.SetActive(false);
        creditsCanvasGO.SetActive(true);

        EventSystem.current.SetSelectedGameObject(creditsFirst);
    }

    #endregion

    #region Main Menu Actions

    public void OnHowToPlayPress()
    {
        OpenHowToPlay();
    }

    public void OnSettingsPress()
    {
        OpenSettings();
    }

    public void OnTrapsPress()
    {
        OpenTraps();
    }

    public void OnCreditsPress()
    {
        OpenCredits();
    }

    public void BackToMain()
    {
        OpenMainMenu();
    }

    public void OnQuitPress()
    {
        Application.Quit();



#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    #endregion

}