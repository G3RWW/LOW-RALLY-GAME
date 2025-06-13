using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseManager : MonoBehaviour
{
    public GameObject pauseMenuUI;
    public GameObject settingsUI; // Optional: Reference to settings UI if you have one
    public GameObject controlsPanel;

    private bool isPaused = false;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused) Resume();
            else Pause();
        }
    }

    public void Resume()
    {
        pauseMenuUI.SetActive(false);
        Time.timeScale = 1f;
        isPaused = false;
        FMODAudioManager.Instance.SetEngineMuted(false);
    }

    public void Pause()
    {
        settingsUI.SetActive(false);
        pauseMenuUI.SetActive(true);
        Time.timeScale = 0f;
        isPaused = true;
        FMODAudioManager.Instance.SetEngineMuted(true);
    }

    public void Settings()
    {
        pauseMenuUI.SetActive(false);
        settingsUI.SetActive(true);
        controlsPanel.SetActive(false);
        Time.timeScale = 0f;
        isPaused = true;
        FMODAudioManager.Instance.SetEngineMuted(true);
    }

    public void ShowControlsPanel()
    {
        settingsUI.SetActive(false);
        controlsPanel.SetActive(true);
    }

    public void BackToSettings()
    {
        controlsPanel.SetActive(false);
        settingsUI.SetActive(true);
    }


    public void RestartScene()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void QuitToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu"); // Replace with your actual main menu scene name
    }
}
