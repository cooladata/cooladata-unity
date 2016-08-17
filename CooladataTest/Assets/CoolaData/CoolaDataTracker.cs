using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using Boomlagoon.JSON;
using com.cooladata.tracking.sdk.utils;

namespace com.cooladata.tracking.sdk.unity{
	/// <summary>
	/// Coola data tracker - implements the cooladata SDK
	/// </summary>
	public class CoolaDataTracker : MonoBehaviour {

		const string TrackerVersion = "v1.0.7";

        public static CoolaDataUTM coolaDataUTM;

        // OperationCompleteCallback signature
        public delegate void OperationCompleteCallback (string message);
		
		// OperationCompleteCallback Event declaration
		public event OperationCompleteCallback operationComplete;

		/// <summary> The maximum number of batches waiting to be sent to server (bigger amount means more memory consumed in case of connection loss)</summary>
		public int maxBatchesWaitingForServer = 1000;

		/// <summary>
		/// Returns the singleton instance of the CoolaData tracker.
		/// </summary>
		/// <returns>The instance.</returns>
		public static CoolaDataTracker getInstance(){
			return instance;

		}

		/// <summary> Sets the CooladataTraker to use the specified apiToken when talking to server </summary>
		/// <param name="apiToken">API token. Cannot be null.</param>
		public void setup (string apiToken) { setup(apiToken, null, null); }

		/// <summary>  Sets the CooladataTraker to use the specified apiToken when talking to server at endpointUrl. Sets userId to be defualt values in case they are not provided in subsequent trackEvent function calls.</summary>
		/// <param name="apiToken">API token. Cannot be null.</param>
		/// <param name="endpointUrl">Endpoint URL. Can be null.</param>
		/// <param name="userId">User identifier. If null, must send with trackEvent.</param>
		public void setup (string apiToken, string endpointUrl, string userId) {
			setupState = SetupState.Called;

			// inititlaize the queue
			queue = queue ?? new CoolaDataQueue(long.Parse(defaults["queueMaxSize"]), int.Parse(defaults["maxQueueSizeTriggerPercent"]));

			//initialize the server communication
			CoolaDataTracker.endpointUrl = ( ( ! string.IsNullOrEmpty(endpointUrl)) ?  endpointUrl : defaults["serviceEndPoint"] );
			CoolaDataTracker.apiToken = apiToken;

			// setup batch queue and batch sending 
			batchQueue = batchQueue ?? new CoolaDataBatchQueue();
			batchQueue.maxNumberOfBatchs = maxBatchesWaitingForServer;	
			if(isManageSendAttachedToBatchQueue == false){
				queue.TriggersBatchSending += ManageBatchSending;
				isManageSendAttachedToBatchQueue = true;
			}

			if(string.IsNullOrEmpty(userId) )
            {
                // Try to load the user if from the player prefs
                string savedUserId = CooladataLocalStorage.LoadUserId();

                if (string.IsNullOrEmpty(savedUserId))
                {
                    // Generate a random user id
                    System.Guid uid = System.Guid.NewGuid();
                    instance.userId = uid.ToString();

                    // Save the user id
                    CooladataLocalStorage.SaveUserId(instance.userId);
                }
                else
                {
                    // We use the saved user id
                    instance.userId = savedUserId;
                }                
            }
            else
            {
                // We use the user id defined by the user
                instance.userId = userId;
            }

            // Initialize the UTM data
            coolaDataUTM = new CoolaDataUTM();

            setupState = SetupState.Finished;
			if(isSendEveryIntervalCoroutineRunning == false){
				isSendEveryIntervalCoroutineRunning = true;
				StartCoroutine(TrySendBatchEveryInterval());
			}

            StartCoroutine(GetCalibrationTimeFromServer(apiToken));
		} 

		/// <summary> Tracks the event - The trackEvent method will store the reported event in a queue and will return back immediately and will throw/return error if proper conditions for sending the event are not met </summary>
		public void trackEvent( string eventName, Dictionary<string, JSONValue> eventProperties){ trackEvent(eventName, eventProperties, null, null); }

		/// <summary> Tracks the event - The trackEvent method will store the reported event in a queue and will return back immediately and will throw/return error if proper conditions for sending the event are not met </summary>
		public void trackEvent( string eventName, Dictionary<string, JSONValue> eventProperties, string eventId, Action<CoolaDataDeliveryResult> callback){ trackEvent(eventName, null, eventProperties, eventId, callback); }

		/// <summary> Tracks the event - The trackEvent method will store the reported event in a queue and will return back immediately and will throw/return error if proper conditions for sending the event are not met </summary>
		public void trackEvent( string eventName, string userID, Dictionary<string, JSONValue> eventProperties){ trackEvent(eventName, userID, eventProperties, null, null); }

		/// <summary> Tracks the event - The trackEvent method will store the reported event in a queue and will return back immediately and will throw/return error if proper conditions for sending the event are not met </summary>
		public void trackEvent( string eventName, string userID, Dictionary<string, JSONValue> eventProperties, string eventId, Action<CoolaDataDeliveryResult> callback){
			if(setupState == SetupState.NotCalled) {
				SetupRequired();
			} else if (setupState == SetupState.Failed) {
				return;
			}
			queue.Add(eventName, userID, eventProperties, eventId, callback);
		}

		#region Internal

		#region Internal Component
		static CoolaDataTracker instance {
			get {
				if (_instance == null) {
					CoolaDataTracker cooladata = GameObject.FindObjectOfType<CoolaDataTracker>();
					if (cooladata == null)
					{
						GameObject go = new GameObject();
						go.name = "Cooladata";
						cooladata = go.AddComponent<CoolaDataTracker>();
					}
					_instance = cooladata;
				}
				return _instance;
			}
		}
		static CoolaDataTracker _instance;
		
		CoolaDataQueue queue;
		CoolaDataBatchQueue batchQueue;

		enum SetupState { NotCalled, Called, Finished, Failed };

		SetupState setupState = SetupState.NotCalled;
		
		void Awake() {
			DontDestroyOnLoad(gameObject);
		}
		#endregion

		#region Internal Batch Sending
		/// <summary> Is ManageSend attached to batchQueue trigger event </summary>
		bool isManageSendAttachedToBatchQueue = false;
		/// <summary> is already trying to send batch </summary>
		bool alreadyTryingToSendBatch = false;
		/// <summary> The time when we last tried to send a batch </summary>
		float lastBatchSendingCompletedTime;
		/// <summary> The batch send start time. </summary>
		float batchSendStartTime;
		/// <summary> Is send every interval coroutine running. </summary>
		bool isSendEveryIntervalCoroutineRunning = false;
        /// <summary> Calibration time got from the server. if value is 0 it means we did not succeeed to get that number. </summary>
		static double calibrationTimeMS = 0;

        void OnEnable(){
			// reactivate batch sending when we are enabled if needed
			if(setupState == SetupState.Finished){
				if(isManageSendAttachedToBatchQueue == true)	queue.TriggersBatchSending += ManageBatchSending;
				if(isSendEveryIntervalCoroutineRunning == false)	StartCoroutine(TrySendBatchEveryInterval());
			}
		}
		
		void OnDisable(){
			// deactivate batche sending if we are disabled
			if(setupState == SetupState.Finished){
				if(isManageSendAttachedToBatchQueue == true)	queue.TriggersBatchSending -= ManageBatchSending;
			}
		}
		
		/// <summary> Tries to send a batch every interval. </summary>
		IEnumerator TrySendBatchEveryInterval(){
			// while we are active
			//Debug.Log ("Trying1");
			while(enabled && setupState == SetupState.Finished){
				//Debug.Log ("Trying2");
				// wait for events publich intervall then try to send a batch
				float publishInterval = float.Parse(defaults["eventsPublishIntervalMillis"])/1000f;
				yield return new WaitForSeconds(publishInterval);
				if(enabled && setupState == SetupState.Finished) ManageBatchSending();
			}
			isSendEveryIntervalCoroutineRunning = false;
			yield break;
		}

        /// <summary> Get the calibration time from the server. </summary>
		IEnumerator GetCalibrationTimeFromServer(string apiToken)
        {
            // Add slash in the end if needed
            if (CoolaDataTracker.endpointUrl[CoolaDataTracker.endpointUrl.Length - 1] != '/') CoolaDataTracker.endpointUrl += '/';

            string finalAddress = CoolaDataTracker.endpointUrl + "egw/2/" + apiToken + "/config";

            WWW w = new WWW(finalAddress);

            // Wait for response
            while (!w.isDone)
            {
                yield return new WaitForSeconds(0.1f);
            }

            // Check timeout
            if (!w.isDone)
            {
                Debug.Log("GetCalibrationTimeFromServer 408 (timeout).");

                w.Dispose();
                yield break;
            }
   
            if (!String.IsNullOrEmpty(w.error))
            {
                Exception e = new Exception(w.error);
                Debug.Log("Error in the connection for " + finalAddress + ": " + e);
                yield break;
            }
            else
            {
                JSONObject responseDictionary = JSONObject.Parse(w.text);

                // Get the configuration
                JSONObject configurationJSON = responseDictionary.GetObject("configuration");

                // Get the calibration time
                calibrationTimeMS = configurationJSON.GetNumber("calibrationTimestampMillis");

                if (CoolaDataTracker.getInstance().operationComplete != null)
                {
                    CoolaDataTracker.getInstance().operationComplete("Calibration time: " + calibrationTimeMS);
                }
            }
        }            

        /// <summary> Manages the batch sending: tryes to add a new batch from queue to batchQueue, and then send it </summary>
        void ManageBatchSending() {
			// check if error with api token
			if(setupState == SetupState.NotCalled){
				SetupRequired();
			}
			
			// Add a new batch to the batchQueue to await sending, if possible
			int maxBatchSize = (int)((int.Parse(defaults["queueMaxSize"]) * int.Parse(defaults["maxQueueSizeTriggerPercent"])) / 100f);
			List<TrackEventParamaters> batch = queue.GetFirst(maxBatchSize);
			if( batchQueue.Add(batch) ) queue.DeleteFirst(maxBatchSize);
			
			// try to send a batch from batch queue
			if( alreadyTryingToSendBatch == false  &&  batchQueue.Count() > 0 ) {
				alreadyTryingToSendBatch = true;
			StartCoroutine(TrySendBacthFromQueue(0));
			}
		}
		
		/// <summary> Tries to send the first bacth from batch queue to the server </summary>
		///  NOTE: should only be called from ManageBatchSending or BatchSendCallbackDelegate to prevent multiple competing calls that break the backoff interval times
		IEnumerator TrySendBacthFromQueue(int attemptsMadeSoFar){
			// Make sure we comply with publishing backoff interval
			float publishBackoffInterval = float.Parse(defaults["eventsPublishBackoffIntervalMillis"])/1000f;
			
			if( lastBatchSendingCompletedTime < publishBackoffInterval )
				yield return publishBackoffInterval - lastBatchSendingCompletedTime;
			
			// get batch and turn it into usable post data
			var batch = batchQueue.GetFirstBatch();
			
			if(batch == null) yield break;
			
			// create the post data we need to send to the server
			string postDataString = "";
			foreach(TrackEventParamaters item in batch){
				postDataString += item.ToString();
				postDataString += "\n,";
			}
			postDataString = postDataString.Remove(postDataString.Length-2); // removes the last NEWLINE and ',' we added

			string postDataStrNotEnc = "{\"events\":[" + postDataString + "]}";

			string postDataStrEnc = WWW.EscapeURL(postDataStrNotEnc);
			string finalPostDataStr = "data=" + postDataStrEnc;

			byte[] postData = System.Text.Encoding.ASCII.GetBytes(finalPostDataStr); // System.Text.Encoding.UTF8.GetBytes(postDataString)
			
			// mark when we started the attempt so we can calculate how long we need to wait before a retry
			batchSendStartTime = Time.realtimeSinceStartup;

			if (CoolaDataTracker.getInstance().operationComplete != null) {
				CoolaDataTracker.getInstance().operationComplete("Trying to send " + batch.Count + " events to server");
			}

			// try to send batch
			Send(string.Format("v1/{0}/track?r={1}", apiToken, UnityEngine.Random.Range(0,int.MaxValue)), postData
			     , delegate(string arg1, Exception arg2) { StartCoroutine(BatchSendCallbackDelegate(arg1, arg2, attemptsMadeSoFar));} , false, true);
			yield break;
		}
		
		/// <summary> The callback for the batch we tried to send </summary>
		IEnumerator BatchSendCallbackDelegate(string response, Exception e, int attemptsMadeSoFar) {
			// note when we finished sending a batch so we can calculate how long to wait before trying to automatically publish again
			lastBatchSendingCompletedTime = Time.realtimeSinceStartup;
			// update number of attempts made
			attemptsMadeSoFar++;	

			// check for errors and handle accordingly
			if(e != null) {
				
				// handel the error by type
				if(e.Message.StartsWith("403")){ // "403" error code means the api token is incorrect
					Debug.Log("BatchSendCallbackDelegate failed. 403");
					setupState = SetupState.NotCalled;
					alreadyTryingToSendBatch = false;
					yield break;
				}
				// on all other error codes we reattempt to send unless limit reached
				else { 

					Debug.Log("BatchSendCallbackDelegate failed. " + e.Message);

					// check if we have reached the retry limit
					if(attemptsMadeSoFar >= int.Parse(defaults["maxTotalRequestRetries"])){
						// Let all callbacks in batch know that attempt failed and what error codes we got
						foreach(var item in batchQueue.GetFirstBatch()){
							if( ( ! string.IsNullOrEmpty(item.eventId)) && item.callback != null) {
								// set the proper error paramaters to each CoolaData delivery result we need to return to each callback
								CoolaDataDeliveryResult coolaDataDeliveryResult = new CoolaDataDeliveryResult();
								coolaDataDeliveryResult.eventId = item.eventId;
								coolaDataDeliveryResult.status = false;
								int indexOfFirstSpace = e.Message.IndexOf(" ");
								coolaDataDeliveryResult.deliveryStatusCode = int.Parse(e.Message.Substring(0, indexOfFirstSpace));
								coolaDataDeliveryResult.deliveryStatusDescription = e.Message.Substring(indexOfFirstSpace + 1, e.Message.Length - indexOfFirstSpace - 1);
								// call the callback with the CoolaData delivery result 
								item.callback(coolaDataDeliveryResult);
							}
						}
						// remove the first batch, and let the batch manager try again
						batchQueue.RemoveFirstBatch();
						alreadyTryingToSendBatch = false;
						ManageBatchSending();
						yield break;
					}		
					
					// comply with failed attempt backoff interval
					float backoffTimeBeforeReattempt = float.Parse(defaults["eventsOutageBackoffIntervalMillis"])/1000f;
					if( Time.realtimeSinceStartup - batchSendStartTime < backoffTimeBeforeReattempt )
						yield return new WaitForSeconds(backoffTimeBeforeReattempt - Time.realtimeSinceStartup + batchSendStartTime);
					// try to send the batch again
					StartCoroutine(TrySendBacthFromQueue(attemptsMadeSoFar));
					yield break;
				}
			}
			else {
				if (CoolaDataTracker.getInstance().operationComplete != null) {
					CoolaDataTracker.getInstance().operationComplete(response);
				}

				Debug.Log("BatchSendCallbackDelegate soccess. response: " + response);
			}
			// send was sucessful 
			
			// collect all callback trackEvents we have
			Dictionary<string, Action<CoolaDataDeliveryResult>> callbacks = new Dictionary<string, Action<CoolaDataDeliveryResult>>();
			foreach(var item in batchQueue.GetFirstBatch()){
				if( ( ! string.IsNullOrEmpty(item.eventId)) && item.callback != null) callbacks.Add(item.eventId, item.callback);
			}
			// parse response into usable format
			JSONObject responseDictionary = JSONObject.Parse(response);
			// give the callbacks their answers
			foreach(var result in responseDictionary.GetObject("results")){
				// Extract the specific CoolaData delivery result from the server response
				CoolaDataDeliveryResult coolaDataDeliveryResult = new CoolaDataDeliveryResult();
				coolaDataDeliveryResult.eventId = result.Key;
				coolaDataDeliveryResult.status = responseDictionary.GetBoolean("status");
				coolaDataDeliveryResult.deliveryStatusCode = 200; // succeffuly communication with server
				coolaDataDeliveryResult.deliveryStatusDescription = null; // The description of the status state. e.g. “Failed to send event due to network issues”
				coolaDataDeliveryResult.responseProperties = new Dictionary<string, string>();
				foreach(var pair in result.Value.Obj){ coolaDataDeliveryResult.responseProperties.Add(pair.Key, pair.Value.ToString()); }
				// call the appropriate callback eith the Cooladata deliviery result
				callbacks[result.Key](coolaDataDeliveryResult);
			}
			// remove first batch which we have sucessfully sent
			batchQueue.RemoveFirstBatch();
			// unlock batch sending
			alreadyTryingToSendBatch = false;
		}
		#endregion

		#region Internal Communication

		static string endpointUrl;
		static string apiToken;
		string userId;

		private void Send (string command, byte[] postData, Action<string,Exception> callback){ Send(command, postData, callback, true, false);}
		private void Send (string command, byte[] postData, Action<string,Exception> callback, bool logErrors, bool sendingABatch){
			instance.StartCoroutine(Send_Coroutine(command, postData, callback, logErrors, sendingABatch));
		}
		
		private IEnumerator Send_Coroutine(string command, byte[] postData, Action<string, Exception> callback, bool logErrors, bool sendingABatch) {
			string finalAddress = endpointUrl;
			if(finalAddress[finalAddress.Length - 1] != '/') finalAddress += '/';
			finalAddress += command;
			
			WWW w = null;
			if (postData == null || postData.Length == 0) { // is GET
				w = new WWW(finalAddress);
			} else { // is POST
				Dictionary<string,string> headers = new Dictionary<string, string>();
				headers.Add("Content-Type", "application/x-www-form-urlencoded");
				w = new WWW(finalAddress, postData, headers);
			}

			if( ! sendingABatch){
		 		yield return w;
			} else {
				float timeAlreadyWaiting = 0;
				while( ! w.isDone && float.Parse(defaults["eventsPublishIntervalMillis"])/1000f > timeAlreadyWaiting){
					yield return new WaitForSeconds(0.1f);
					timeAlreadyWaiting += 0.1f;
				}

				if( ! w.isDone ) {

					Debug.Log("Send_Coroutine 408 (timeout).");

					w.Dispose(); 
					callback( null, new TimeoutException("408 Request Timeout")); 
					yield break;
				}
			}

			if (!String.IsNullOrEmpty(w.error)) {
				Exception e = new Exception(w.error);
				if(logErrors) Debug.LogError("Error in the connection for " + finalAddress  + ": " +  w.error);
				callback(null, e);
				yield break;
			}

			callback(w.text, null);
		}
		#endregion

		#region Internal Defaults
		/// <summary> default values </summary>
			private static Dictionary<string, string> defaults = new Dictionary<string, string>() {
				{"serviceEndPoint","https://api.cooladata.com"}, // "http://127.0.0.1:1337/"}, //
				{"queueMaxSize", "30000"},
				{"maxEventsPerRequest", "50"},
				{"maxSingleRequestRetries", "3"},
				{"maxTotalRequestRetries", "24"},
				{"eventsPublishBackoffIntervalMillis", "5000"},
				{"eventsOutageBackoffIntervalMillis", "15000"},
				{"eventsPublishIntervalMillis"," 5000"},
				{"maxQueueSizeTriggerPercent", "85"},
				{"tracker_type", "unity"},
				
			};
        #endregion

        ///<summary> Should hold all paramaters that cooladataTracker needs to track an Event</summary>
        private struct TrackEventParamaters{
			private JSONObject info;
			/// <summary> The name of the event. </summary>
			public string eventName{ get { return info.ContainsKey("event_name") ? info["event_name"].Str : null; } }
			/// <summary> The user identifier.</summary>
			public string userId{ get { return info.ContainsKey("user_id") ? info["user_id"].ToString() : null; } }
			/// <summary> The event identifier. </summary>
			public string eventId{ get { return info.ContainsKey("event_id") ? info["event_id"].Str : null; } }
			/// <summary> The callback for the event id's event response</summary>
			public Action<CoolaDataDeliveryResult> callback;
			
			public TrackEventParamaters( string eventName, string userId, Dictionary<string, JSONValue> eventProperties, string eventId, Action<CoolaDataDeliveryResult> callback){
				this.info = new JSONObject();

				if( string.IsNullOrEmpty(eventName) ) throw new ArgumentException("The event name cannot be empty - it's the primary identifier of the event and must be populated correctly");
				info.Add("event_name", eventName);

				if( string.IsNullOrEmpty(userId) &&  string.IsNullOrEmpty(instance.userId) ) throw new ArgumentException("User ID must either be Provided at Setup or provided as a parameter to track event. Doing both is allowed, doing neither is not");
				info.Add("user_id", string.IsNullOrEmpty(userId) ? instance.userId : userId);

		        this.callback = callback;

				if( ! string.IsNullOrEmpty(eventId) )	info.Add("event_id", eventId);
				// add provided paramators, missing mandatory fields, additional optional fields and UTM data
				foreach(KeyValuePair<string, JSONValue> pair in eventProperties) {  if( ! info.ContainsKey(pair.Key) ) info.Add(pair.Key, pair.Value); }
				foreach(var pair in MandatoryFields()) { if( ! info.ContainsKey(pair.Key) ) 	info.Add(pair.Key, pair.Value); }
				foreach(var pair in OptionalFields()) { if( ! info.ContainsKey(pair.Key) ) 	info.Add(pair.Key, pair.Value); }
                foreach (var pair in coolaDataUTM.UTMData) { if (!info.ContainsKey(pair.Key)) info.Add(pair.Key, pair.Value); }
			}

			public override string ToString (){
				return info.ToString();
			}
		}

		#region Internal Collection Parameters
		#region Mandatory Fields
		/// <summary> returns a dictionary of the fields that must be included when sending to server</summary>
		/// <returns>The fields.</returns>
		static Dictionary<string,JSONValue> MandatoryFields(){
			Dictionary<string, JSONValue> data = new Dictionary<string, JSONValue>();
            // Mandatory Fields

            if (calibrationTimeMS == 0)
            {
                // We got not calibration time from the server, use the local machine time
                data["event_timestamp_epoch"] = System.Convert.ToInt64(System.DateTime.UtcNow.Subtract(new System.DateTime(1970, 1, 1)).TotalMilliseconds);  // Time in milliseconds from Jan 1st 1970
            }
            else
            {
                double timeInAppMS = Math.Round(System.Convert.ToDouble(Time.time) * 1000);
                data["event_timestamp_epoch"] = (calibrationTimeMS + timeInAppMS);
            }

            data["event_timezone_offset"] = GetTimeZoneOffset();
			// user_id  added by trackeEvent
			// alternative_user_id  added by trackEvent (in paramter eventProperties)
			data["tracker_type"] = "unity";
			data["tracker_version"] = TrackerVersion;
			data["r"] = UnityEngine.Random.Range(0,int.MaxValue).ToString() ; // A random number added to the URL for each REST API call to prevent caching/proxying on the network.
			// event_id  passed to trackEvent
			return data;
		}
		
		/// <summary> The local timezone in float format, e.g: -3.0 means GMT-3:00 </summary>
		/// <returns>The time zone offset.</returns>
		static int GetTimeZoneOffset(){
            int event_timezone_offset =
                (System.TimeZone.CurrentTimeZone.GetUtcOffset(System.DateTime.Now).Hours // calculate the offset right now
                 - ((System.TimeZone.CurrentTimeZone.IsDaylightSavingTime(System.DateTime.Now)) ? 1 : 0)); // dedcut daylight savings if needed
			return event_timezone_offset;
		}

        #endregion

        #region Optional Fields
        /// <summary> returns a dictionary of the fields that can be included when sending to server</summary>
        static Dictionary<string,JSONValue> OptionalFields(){
			Dictionary<string, JSONValue> data = new Dictionary<string, JSONValue>();
			// screen fields
			data["session_screen_size"] = Screen.currentResolution.width + "x" + Screen.currentResolution.height;
			int hfc = FindHCF(Screen.currentResolution.width, Screen.currentResolution.height);
			data["session_screen_scale"] = (Screen.currentResolution.width/hfc) + "/" + (Screen.currentResolution.height/hfc);

			data["session_device_orientation"] = Screen.orientation.ToString();
			// OS fields
			if( ! string.IsNullOrEmpty(SystemInfo.operatingSystem) ){
				int indexOfFisrtSpace = SystemInfo.operatingSystem.IndexOf(" ");
				data["session_os_version"] = SystemInfo.operatingSystem.Substring(indexOfFisrtSpace + 1, SystemInfo.operatingSystem.Length - indexOfFisrtSpace - 1); 
				data["session_os"] = SystemInfo.operatingSystem.Substring(0, indexOfFisrtSpace); 
			}
			// Device fields
			if( SystemInfo.deviceName != null) data["device_name"] = SystemInfo.deviceName;

            // Time fields
            data["time_in_app"] = Math.Round(System.Convert.ToDouble(Time.time) * 1000);

			// Device extra information
			data["device_name"] = SystemInfo.deviceName;
			data["session_model"] = SystemInfo.deviceModel;
			data["device_type"] = SystemInfo.deviceType.ToString();
			data["device_unique_identifier"] = SystemInfo.deviceUniqueIdentifier;


			return data;
		}

		/// <summary>
		/// Finds the Highest Common Factor (needed to calculate the aspect ratio).
		/// </summary>
		/// <returns>The HC.</returns>
		/// <param name="m">M.</param>
		/// <param name="n">N.</param>
		private static int FindHCF(int m, int n)
		{
			int temp, reminder;
			if (m < n)
			{
				temp = m;
				m = n;
				n = temp;
			}
			while (true)
			{
				reminder = m % n;
				if (reminder == 0)
					return n;
				else
					m = n;
				n = reminder;
			}
		}
		#endregion
		#endregion

		#region Internal Management
		private static string SetupRequired(){
			throw new ArgumentException("You need to preform setup(apiToken).(token provided in the request was invalid or unknown)");
		}
		#endregion

		#region Internal CoolaDataQueue
		/// <summary>
		/// Temporarily holds all tracked events until certain trigger is reached and the queue is flushed
		/// Prioritized callback events over non-callback events.
		/// </summary>
		private class CoolaDataQueue {
			/// <summary> queue items without callbacks </summary>
			List<CoolaDataTracker.TrackEventParamaters> items = new List<CoolaDataTracker.TrackEventParamaters>();	
			/// <summary> queue items with callbacks </summary>
			List<CoolaDataTracker.TrackEventParamaters> callbackItems = new List<CoolaDataTracker.TrackEventParamaters>(); 
			
			public int Count{ get{ return items.Count + callbackItems.Count;} }
			
			/// <summary> Occurs when queue size precentage is exceeded </summary>
			public event Action TriggersBatchSending;
			
			/// <summary> Gets or sets the maximum size of the queue max. </summary>
			public long queueMaxSize{ 
				get{ return _queueMaxSize; } 
				set{ 
					_queueMaxSize = ( (value < 0) ? 0 : value);
					ShrinkQueueDownToSize();
					CheckIfQueueTriggeredBySize();
				}
			}
			private long _queueMaxSize;
			
			/// <summary> Gets or sets the max queue size trigger percentage </summary>
			public int maxQueueSizeTriggerPercent{
				get{ return Mathf.RoundToInt(_maxQueueSizeTriggerPercent*100f); }
				set{
					_maxQueueSizeTriggerPercent = Mathf.Clamp(value,0, 100)/100f;
					CheckIfQueueTriggeredBySize();
				}
			}
			private float _maxQueueSizeTriggerPercent;
			
			public CoolaDataQueue(long queueMaxSize, int maxQueueSizeTriggerPercent){
				this.queueMaxSize = queueMaxSize;
				this.maxQueueSizeTriggerPercent = maxQueueSizeTriggerPercent;
			}
			#region Internal Management Function
			/// <summary> Removes items from items and callbackItems so that their joint size is at most queueMaxSize. </summary>
			private void ShrinkQueueDownToSize(){
				if (items.Count + callbackItems.Count > queueMaxSize){ // remove as many as you can from the lower priority 
					items.RemoveRange(0, Mathf.Min( (int)(items.Count + callbackItems.Count - queueMaxSize), items.Count));
					// at worst we will remove all of items, and then the only way we still exceed capacity is if callbackItems exceeds capacity
					if(callbackItems.Count > queueMaxSize){ 
						callbackItems.RemoveRange(0, (int)(callbackItems.Count - queueMaxSize));
					}
				}
			}
			/// <summary> Checks if the current size of the queue has reached the precentage where we should trigger the event </summary>
			private void CheckIfQueueTriggeredBySize(){
				if((TriggersBatchSending != null 
				    && (items.Count + callbackItems.Count >= queueMaxSize*_maxQueueSizeTriggerPercent)
				    ||
				    callbackItems.Count > 0) // We automatically try to send a batch if a eventTrack with a callback has arrived
				   ) {
					TriggersBatchSending();
				}
			}
			#endregion
			/// <summary> Add the specified eventName, userId, eventProperties, eventId and callback to the Queue </summary>
			/// <param name="eventName">Event name.</param>
			/// <param name="userID">User identifier.</param>
			/// <param name="eventProperties">Event properties.</param>
			/// <param name="eventId">Event identifier.</param>
			/// <param name="callback">Callback.</param>
			public void Add( string eventName, string userId, Dictionary<string, JSONValue> eventProperties, string eventId, Action<CoolaDataDeliveryResult> callback){
				if( ! AreArgumentsOkay(eventName, userId, eventProperties, eventId, callback) ) return;
				CoolaDataTracker.TrackEventParamaters queuedItem = new CoolaDataTracker.TrackEventParamaters(eventName, userId, eventProperties, eventId, callback);
				if( string.IsNullOrEmpty(eventId) && callback == null){ 
					items.Add(queuedItem); 
				}else {	callbackItems.Add(queuedItem);}
				ShrinkQueueDownToSize();

				if (CoolaDataTracker.getInstance().operationComplete != null) {
					CoolaDataTracker.getInstance().operationComplete("Queue size: " + Count);
				}

				CheckIfQueueTriggeredBySize();
			}
			/// <summary>Checks if the arguments are legal  </summary>
			private bool AreArgumentsOkay(string eventName, string userID, Dictionary<string, JSONValue> eventProperties, string eventId, Action<CoolaDataDeliveryResult> callback){
				return (! string.IsNullOrEmpty(eventName)) && eventProperties != null 
					&& ( (string.IsNullOrEmpty(eventId) && callback == null) || ( ( ! string.IsNullOrEmpty(eventId) ) && callback != null) );
			} 
			/// <summary> 
			/// Gets the first "amount" (or all if amount exceedsexting itmes) of CoolaDataTracker.TrackEventParamaterss from the queue, 
			/// returns null if amount is negative number. 
			/// </summary>
			public List<CoolaDataTracker.TrackEventParamaters> GetFirst(int amount){
				if( amount < 0 ) return null;
				// collact the first "amount" of CoolaDataTracker.TrackEventParamaters, callback items are collected first, regular items afterwords
				List<CoolaDataTracker.TrackEventParamaters> res = new List<CoolaDataTracker.TrackEventParamaters>();
				for ( int i = 0; i < Mathf.Min( callbackItems.Count , amount); i ++){ res.Add(callbackItems[i]); }
				for ( int i = 0; i < Mathf.Min( items.Count 			, Mathf.Clamp(amount - items.Count, 0 , int.MaxValue)) ; i++) { res.Add(items[i]); }
				return res;
			}
			/// <summary> Deletes the first "amount" (or all if amount exceedsexting itmes) of CoolaDataTracker.TrackEventParamaterss from the queue. </summary>
			public void DeleteFirst(int amount){
				if( amount <= 0 ) return;
				int amountToRemove =  Mathf.Min(amount, callbackItems.Count);
				callbackItems.RemoveRange(0, amountToRemove);
				if(amount > amountToRemove){ // if callbackItems.Count was smaller then amount
					items.RemoveRange(0, Mathf.Min(amount - amountToRemove, items.Count) );
				}
			}
		}
		#endregion

		#region Internal CoolaDataBatchQueue
		/// <summary>
		/// Cooladata batch queue
		/// Holds batches of events we are tracking and want to send to the server
		/// Queue workfs in a FiFo manner - first batch that goes into the queue is the first batch that leaves the queue
		/// </summary>
		private class CoolaDataBatchQueue {
			/// <summary> The batch queue. </summary>
			List<List<CoolaDataTracker.TrackEventParamaters>> batchQueue = new List<List<CoolaDataTracker.TrackEventParamaters>>();
			
			/// <summary> The maximum number of batchs in the queue. </summary>
			public int maxNumberOfBatchs;
			
			/// <summary> 
			/// Add the specified newBatch, if newBatch is a valid non-empty batch, and there is still space in the queue
			/// Returns true if added the newBatch to the queue, false otherwise
			/// </summary>
			public bool Add(List<CoolaDataTracker.TrackEventParamaters> newBatch){
				if (newBatch == null || newBatch.Count == 0) return false; // if the new batch is invalid returns 
				if (batchQueue.Count >= maxNumberOfBatchs) return false;
				
				batchQueue.Add(newBatch);
				return true;
			}
			
			/// <summary> Removes the first batch in the queue</summary>
			public void RemoveFirstBatch(){ 
				if (batchQueue.Count > 0) batchQueue.RemoveAt(0); 
			}
			
			/// <summary> Gets the first batch in the queue, if the queue is empty returns null </summary>
			/// <returns>The first batch.</returns>
			public List<CoolaDataTracker.TrackEventParamaters> GetFirstBatch(){ 
				if (batchQueue.Count > 0) 	return batchQueue[0]; 
				else 									return null;
			}
			
			/// <summary> Returns the number of batches in the queue </summary>
			public int Count() { return batchQueue.Count; }
		}
		#endregion
		#endregion
	}
}