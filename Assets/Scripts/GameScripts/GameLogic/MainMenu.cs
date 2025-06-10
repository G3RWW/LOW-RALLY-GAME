using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    public GameObject mainMenu;
    public GameObject settingsMenu;
    public GameObject selectMode;
    public GameObject selectCar;
    public GameObject selectMap;
    public GameObject controlsPanel;


    public void ShowMainMenu()
    {
        HideAll();
        mainMenu.SetActive(true);
    }

     public void ShowSettingsMenu()
    {
        HideAll();
        settingsMenu.SetActive(true);
    }

    public void ShowSelectMode()
    {
        HideAll();
        selectMode.SetActive(true);
    }

    public void ShowSelectCar()
    {
        HideAll();
        selectCar.SetActive(true);
    }

    public void ShowSelectMap()
    {
        HideAll();
        selectMap.SetActive(true);
    }

        public void ShowControlsPanel()
    {
        HideAll();
        controlsPanel.SetActive(true);
    }


    public void SelectCar(string carName)
    {
        PlayerSelection.selectedCarName = carName;
        ShowSelectMap(); // cleaner than repeating SetActive
    }

    public void StartGame(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    public void QuitGame()
    {
        Application.Quit();
        Debug.Log("Game is quitting...");
    }

    void HideAll()
    {
        mainMenu.SetActive(false);
        selectMode.SetActive(false);
        selectCar.SetActive(false);
        selectMap.SetActive(false);
        settingsMenu.SetActive(false);
        controlsPanel.SetActive(false);
    }
}
