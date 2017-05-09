using UnityEngine;
using System.Collections.Generic;
using com.cooladata.tracking.sdk.unity;
using com.cooladata.tracking.sdk.utils;
using com.cooladata.tracking.sdk;
using UnityEngine.UI;

public class CooladataTester : MonoBehaviour {

	private GameObject mSetupPanel;
    private GameObject mLogPanel;
    private GameObject mEventsPanel;

	private InputField mServerAddressInputField;
	private InputField mAPIKeyInputField;
	private InputField mUserIdInputField;

	private InputField mEventNameInputField;

	private Text mLogsText;

	private InputField mParamNameInputField;
	private InputField mParamValueInputField;

    private Toggle mUseParamsToggle;

    private Button mSetupButton;
    private Button mSendEventButton;

    void Start () {
        //
        //  Get all the buttons reference
        //
        Button[] buttons = GetComponentsInChildren<Button>();

        for (int index = 0; index < buttons.Length; index++)
        {
            if (buttons[index].name == "SetupButton")
            {
                mSetupButton = buttons[index];
            }
            else if (buttons[index].name == "SendEventButton")
            {
                mSendEventButton = buttons[index];
            }
        }

        //
        //  Get all the input fields reference
        //
        InputField[] inputFields = GetComponentsInChildren<InputField>();

        for (int index = 0; index < inputFields.Length; index++)
        {
            if (inputFields[index].name == "ServerAddressInputField")
            {
                mServerAddressInputField = inputFields[index];
            }
            else if (inputFields[index].name == "APIKeyInputField")
            {
                mAPIKeyInputField = inputFields[index];
            }
            else if (inputFields[index].name == "UserIdInputField")
            {
                mUserIdInputField = inputFields[index];
            }
            else if (inputFields[index].name == "EventNameInputField")
            {
                mEventNameInputField = inputFields[index];
            }
            else if (inputFields[index].name == "ParamNameInputField")
            {
                mParamNameInputField = inputFields[index];
            }
            else if (inputFields[index].name == "ParamValueInputField")
            {
                mParamValueInputField = inputFields[index];
            }
        }

        //
        //  Get all the images reference
        //
        Image[] images = GetComponentsInChildren<Image>();

        for (int index = 0; index < images.Length; index++)
        {
            if (images[index].name == "SetupPanel")
            {
                mSetupPanel = images[index].gameObject;
            }
            else if (images[index].name == "LogsPanel")
            {
                mLogPanel = images[index].gameObject;
            }
            else if (images[index].name == "EventsPanel")
            {
                mEventsPanel = images[index].gameObject;
            }
            
        }

        //
        //  Get all the text reference
        //
        Text[] texts = GetComponentsInChildren<Text>();

        for (int index = 0; index < texts.Length; index++)
        {
            if (texts[index].name == "LogsText")
            {
                mLogsText = texts[index];
            }
        }

        //
        //  Get all the text reference
        //
        Toggle[] toggles = GetComponentsInChildren<Toggle>();

        for (int index = 0; index < toggles.Length; index++)
        {
            if (toggles[index].name == "ParamSelectionToggle")
            {
                mUseParamsToggle = toggles[index];
            }
        }

        // Register to the operation complete (for tester logs)
        CoolaDataTracker.getInstance().operationComplete += operationCompleteCallback;

        //
        // Try to load local data
        //
        mServerAddressInputField.text = CooladataLocalStorage.LoadParam(Constants.SERVER_ADDRESS_LOCAL_STORAGE_STR);
        mAPIKeyInputField.text = CooladataLocalStorage.LoadParam(Constants.API_KEY_LOCAL_STORAGE_STR);
        mUserIdInputField.text = CooladataLocalStorage.LoadParam(Constants.USER_ID_LOCAL_STORAGE_STR);

        //
        // put default values if no loaded from local storage
        //
        if (string.IsNullOrEmpty(mServerAddressInputField.text))
        {
            mServerAddressInputField.text = "https://api.cooladata.com";
        }

        if (string.IsNullOrEmpty(mUserIdInputField.text))
        {
            mUserIdInputField.text = "Unity user";
        }

        // Set the toggle params
        OnParamsToggleChange(false);

        // Show only the setup panel
        mLogPanel.SetActive(false);
        mEventsPanel.SetActive(false);

        //	eventNameInputField.text = "Test Unity Event";

        CheckSetupButton();
        CheckSendEventButton();
    }

    /// <summary>
    /// Check if the setup button should be enabled (All the mandatory fields are filled)
    /// </summary>
    public void CheckSetupButton()
    {
        if (string.IsNullOrEmpty(mServerAddressInputField.text) ||
            string.IsNullOrEmpty(mAPIKeyInputField.text) ||
            string.IsNullOrEmpty(mUserIdInputField.text))
        {
            mSetupButton.enabled = false;
        }
        else
        {
            mSetupButton.enabled = true;
        }
    }

    public void CheckSendEventButton()
    {
        mSendEventButton.enabled = !string.IsNullOrEmpty(mEventNameInputField.text);
    }

    public void OnInputFieldValueChanged(string newText)
    {
        CheckSetupButton();
    }

    public void OnEventNameInputFieldValueChanged(string newText)
    {
        CheckSendEventButton();
    }

    public void OnUISetup() {
		Debug.Log("doSetup. serverAddress: " + mServerAddressInputField.text + ", apiKey: " + mAPIKeyInputField.text + ", userId: " + mUserIdInputField.text);

        mSetupPanel.SetActive(false);
        mLogPanel.SetActive(true);
        mEventsPanel.SetActive(true);   

        // Setup cooladata
        CoolaDataTracker.getInstance().setup(mAPIKeyInputField.text, mServerAddressInputField.text, mUserIdInputField.text);

        AddToLog("UTM data abailable: " + CoolaDataTracker.coolaDataUTM.DataAvailable);

        //
        // Save the local data
        //
        CooladataLocalStorage.SaveParam(Constants.SERVER_ADDRESS_LOCAL_STORAGE_STR, mServerAddressInputField.text);
        CooladataLocalStorage.SaveParam(Constants.API_KEY_LOCAL_STORAGE_STR, mAPIKeyInputField.text);
        CooladataLocalStorage.SaveParam(Constants.USER_ID_LOCAL_STORAGE_STR, mUserIdInputField.text);
    }

	public void OnUISendEvent() {
		Debug.Log("Sending event");

        Dictionary<string, Boomlagoon.JSON.JSONValue> paramsMap = new Dictionary<string, Boomlagoon.JSON.JSONValue>();

        if (mUseParamsToggle.isOn)
        {
            // Prepare the parameters
            paramsMap.Add(mParamNameInputField.text, mParamValueInputField.text);
        }

        // Add the event to the queue
        CoolaDataTracker.getInstance().trackEvent(mEventNameInputField.text, paramsMap);
	}

    private void operationCompleteCallback(string name) {
        AddToLog(name);
	}

    public void OnUIClearLogs() {
		mLogsText.text = "";
	}

    private void AddToLog(string txtToAdd)
    {
        mLogsText.text = (System.DateTime.Now.ToString() + ": " + txtToAdd + "\n" + mLogsText.text);
    }

    public void OnParamsToggleChange(bool isSelected) {
        mParamNameInputField.interactable = mUseParamsToggle.isOn;
		mParamValueInputField.interactable = mUseParamsToggle.isOn;
	}
}
