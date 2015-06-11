using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using com.cooladata.tracking.sdk.unity;
using UnityEngine.UI;

public class CooladataTester : MonoBehaviour {

	private CanvasGroup setupCanvasGroup;
	private CanvasGroup sendEventCanvasGroup;
	private InputField serverAddressInputField;
	private InputField APIKeyInputField;
	private InputField userIdInputField;
	private InputField sessionIdInputField;
	private InputField eventNameInputField;
	private Text logsText;
	private InputField paramNameInputField;
	private InputField paramValueInputField;
	private Toggle paramSelectionToggle;
	
	void Start () {
		// Get reference to all relevant GUI elements
		setupCanvasGroup = (CanvasGroup)GameObject.Find("SetupPanel").GetComponent<CanvasGroup>();
		sendEventCanvasGroup = (CanvasGroup)GameObject.Find("EventsPanel").GetComponent<CanvasGroup>();
		serverAddressInputField = (InputField)GameObject.Find("ServerAddressInputField").GetComponent<InputField>();
		APIKeyInputField = (InputField)GameObject.Find("APIKeyInputField").GetComponent<InputField>();
		userIdInputField = (InputField)GameObject.Find("UserIdInputField").GetComponent<InputField>();
		eventNameInputField = (InputField)GameObject.Find("EventNameInputField").GetComponent<InputField>();
		sessionIdInputField = (InputField)GameObject.Find("SessionIdInputField").GetComponent<InputField>();
		logsText = (Text)GameObject.Find("LogsText").GetComponent<Text>();
		paramNameInputField = (InputField)GameObject.Find("ParamNameInputField").GetComponent<InputField>();
		paramValueInputField = (InputField)GameObject.Find("ParamValueInputField").GetComponent<InputField>();
		paramSelectionToggle = (Toggle)GameObject.Find("ParamSelectionToggle").GetComponent<Toggle>();

		// Register to the operation complete (for tester logs)
		CoolaDataTracker.getInstance().operationComplete += operationCompleteCallback;

		// The events panelshould be disabled until the user setup
		sendEventCanvasGroup.interactable = false;

		// Parameters are disabled when starting
		paramNameInputField.interactable = false;
		paramValueInputField.interactable = false;

		// put default values
		serverAddressInputField.text = "https://api.cooladata.com";
		APIKeyInputField.text = "";
		userIdInputField.text = "Unity user";
		sessionIdInputField.text = "Unity Session";
		eventNameInputField.text = "Test Unity Event";
	}

	public void doSetup() {
		string serverAddress = serverAddressInputField.text;
		string apiKey = APIKeyInputField.text;
		string userId = userIdInputField.text;
		string sessionId = sessionIdInputField.text;

		if (apiKey == "") {
			operationCompleteCallback("Error: The API key can not be empty!");
		}
		else {
			Debug.Log("doSetup. serverAddress: " + serverAddress + ", apiKey: " + apiKey + ", userId: " + userId + ", sessionId: " + sessionId);

			setupCanvasGroup.interactable  = false;
			sendEventCanvasGroup.interactable = true;

			// Setup cooladata
			CoolaDataTracker.getInstance().setup(apiKey, serverAddress, userId, sessionId);
		}
	}

	public void sendEvent() {
		Debug.Log("Sending event");

		Dictionary<string,string> paramsMap = new Dictionary<string, string>();;

		if (paramSelectionToggle.isOn) {
			// Prepare the parameters
			paramsMap.Add(paramNameInputField.text, paramValueInputField.text);
		}

		// Add the event to the queue
		CoolaDataTracker.getInstance().trackEvent(eventNameInputField.text, paramsMap);
	}

	public void operationCompleteCallback(string name) {
		logsText.text = (System.DateTime.Now.ToString() + ": " + name + "\n" + logsText.text);
	}

	public void clearLogs() {
		logsText.text = "";
	}

	public void onParamsToggleChange(bool isSelected) {
		paramNameInputField.interactable = isSelected;
		paramValueInputField.interactable = isSelected;
	}
}
