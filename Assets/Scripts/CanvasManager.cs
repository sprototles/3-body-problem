using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class CanvasManager : MonoBehaviour
{
    [Header("Managers")]
    public GameManager gameManager;

    [Header("UI")]
    public TextMeshProUGUI simulationTimer;

    public TMP_InputField inputField;
    public TMP_Dropdown dropdownTimer;
    public TMP_Dropdown dropdownProcess;

    public Button buttonInstatiate;
    public Button buttonStartProcess;

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

        // buttons
        buttonInstatiate.onClick.AddListener(OnButtonInstantiateClicked);
        buttonStartProcess.onClick.AddListener(OnButtonStartProcessClicked);


        buttonStartProcess.interactable = false;
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

    private void OnButtonInstantiateClicked()
    {
        gameManager.InstantiateClicked();
    }

    private void OnButtonStartProcessClicked()
    {
        gameManager.StartProcessClicked();
    }

    private void OnDropdownTimerValueChanged(int value)
    {
        gameManager.DropdownTimer(value);
    }

    private void OnDropdownProcessValueChanged(int value)
    {
        gameManager.DropdownProcess(value);
    }

    #endregion

    // ####################
    // Canvas visuals
    // ####################

    public void UpdateButtonInstantiateText(string text)
    {
        buttonInstatiate.GetComponentInChildren<TextMeshProUGUI>().text = text;
    }

    public void UpdateButtonStartProcessText(string text)
    {
        buttonStartProcess.GetComponentInChildren<TextMeshProUGUI>().text = text;
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
