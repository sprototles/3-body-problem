using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class CanvasManager : MonoBehaviour
{
    [Header("Managers")]
    public GameManager gameManager;

    [Header("UI")]
    public TextMeshProUGUI simulationTimer;
    [Space]
    public TMP_InputField inputField;
    [Space]
    public TMP_Dropdown dropdownTimer;
    public TMP_Dropdown dropdownProcess;
    public TMP_Dropdown dropdownNumthreadX;
    public TMP_Dropdown dropdownNumthreadY;
    [Space]
    public Button buttonSpawnObjects;
    public Button buttonDestroyObjects;

    public Button buttonStartSimulation;
    public Button buttonStopSimulation;

    public Button buttonOkWarning;
    public Button buttonDisableWarning;
    [Space]
    public GameObject panelWarning;

    [Header("Misc")]
    public TextMeshProUGUI versionText;
    public int avgFrameRate;
    public TextMeshProUGUI display_Text;

    // Start is called before the first frame update
    void Start()
    {
        versionText.text = "Version: " + Application.version; 

        // input fields
        inputField.onValueChanged.AddListener(delegate {
            OnInputFieldValueChanged();
        });

        // dropdowns
        dropdownTimer.onValueChanged.AddListener(delegate { OnDropdownTimerValueChanged(dropdownTimer.value); });
        dropdownProcess.onValueChanged.AddListener(delegate { OnDropdownProcessValueChanged(dropdownProcess.value); });
        dropdownNumthreadX.onValueChanged.AddListener(delegate { OnDropdownNumthreadXValueChanged(dropdownNumthreadX.value); });
        dropdownNumthreadY.onValueChanged.AddListener(delegate { OnDropdownNumthreadYValueChanged(dropdownNumthreadY.value); });

        // buttons
        buttonSpawnObjects.onClick.AddListener(OnButtonSpawnObjectsClicked);
        buttonDestroyObjects.onClick.AddListener(OnButtonDestroyObjectsClicked);

        buttonStartSimulation.onClick.AddListener(OnButtonStartSimulationClicked);
        buttonStopSimulation.onClick.AddListener(OnButtonStopSimulationClicked);

        buttonOkWarning.onClick.AddListener(OnButtonOkWarningPressed);
        buttonDisableWarning.onClick.AddListener(OnButtonDisableWarningPressed);

        // set button status
        buttonSpawnObjects.gameObject.SetActive(true);
        buttonDestroyObjects.gameObject.SetActive(false);

        buttonStartSimulation.gameObject.SetActive(true);
        buttonStopSimulation.gameObject.SetActive(false);


        buttonStartSimulation.interactable = false;

        dropdownNumthreadX.interactable = false;
        dropdownNumthreadY.interactable = false;

        panelWarning.SetActive(false);
    }

    public void Update()
    {
        // calculate frame rate
        float current = 1f / Time.unscaledDeltaTime;
        display_Text.text = current.ToString("F2") + "FPS";
    }

    // ####################
    // Events
    // ####################

    #region Events

    private void OnInputFieldValueChanged()
    {
        int inputFieldNumber = int.Parse(inputField.text);
        int realNumber = Mathf.Clamp(inputFieldNumber, 3, 100000);
        inputField.text = realNumber.ToString();

        gameManager.SetMaxArray(realNumber);
    }

    /// <summary>
    /// button for Spawn objects / Destroy objects
    /// </summary>
    private void OnButtonSpawnObjectsClicked()
    {
        gameManager.SpawnObjectsClicked();
    }

    /// <summary>
    /// button for Spawn objects / Destroy objects
    /// </summary>
    private void OnButtonDestroyObjectsClicked()
    {
        gameManager.DestroyObjectsClicked();
    }

    /// <summary>
    /// button for Start simulation / Stop simulation
    /// </summary>
    private void OnButtonStartSimulationClicked()
    {
        gameManager.StartSimulationClicked();
    }

    /// <summary>
    /// button for Stop simulation
    /// </summary>
    private void OnButtonStopSimulationClicked()
    {
        gameManager.StopSimulationClicked();
    }

    /// <summary>
    /// 
    /// </summary>
    private void OnButtonOkWarningPressed()
    {
        gameManager.OkWarningClicked();
    }

    private void OnButtonDisableWarningPressed()
    {
        gameManager.DisableWarningClicked();
    }

    private void OnDropdownTimerValueChanged(int value)
    {
        gameManager.DropdownTimer(value);
    }

    private void OnDropdownProcessValueChanged(int value)
    {
        gameManager.DropdownProcess(value);
    }

    private void OnDropdownNumthreadXValueChanged(int value)
    {
        gameManager.DropdownNumthreadX(value);
    }

    private void OnDropdownNumthreadYValueChanged(int value)
    {
        gameManager.DropdownNumthreadY(value);
    }

    #endregion

    // ####################
    // Canvas visuals
    // ####################

    public void UpdateButtonInstantiateText(string text)
    {
        buttonSpawnObjects.GetComponentInChildren<TextMeshProUGUI>().text = text;
    }

    public void UpdateButtonStartProcessText(string text)
    {
        buttonStartSimulation.GetComponentInChildren<TextMeshProUGUI>().text = text;
    }

    public void UpdateInputFieldText(string text)
    {
        inputField.text = text;
    }

    public void UpdateSimulationTimerText(string _text)
    {
        simulationTimer.text = _text;
    }

}
