using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace com.cooladata.tracking.sdk.unity{
	/// <summary>
	/// The result given back from the CoolaData server.
	/// </summary>
	public class CoolaDataDeliveryResult {
		/// <summary> The event identifier for the callback </summary>
		public string eventId;
		/// <summary> The status of the server request, will be false if something went wrong </summary>
		public bool status;
		/// <summary> The delivery status description, only relevant if status is not 200 </summary>
		public string deliveryStatusDescription;
		/// <summary> The delivery status code returned with the request to the server, is 200 when request worked </summary>
		public int deliveryStatusCode;
		/// <summary> The response properties the server sent for the callback </summary>
		public Dictionary<string, string> responseProperties;

		public override string ToString ()
		{
			string result = "EventId: " + eventId;
			result += "\n" + "Status: " + (status?"Success":"Failure");
			result += "\n" + "Delivery Status Description: " + (deliveryStatusDescription ?? "NULL");
			result += "\n" + "Delivery Status Code: " + deliveryStatusCode.ToString();
			result += "\n" + "Response Properties: ";
			foreach(var pair in responseProperties) {
				result += "\n\r" + pair.Key + " : " + pair.Value;
			}
			return result;
		}
	}
}