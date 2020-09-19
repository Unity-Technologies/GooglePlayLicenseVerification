using System;
using UnityEngine;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine.Networking;
using UnityEngine.UI;
using Random = System.Random;

[RequireComponent(typeof(Button))]
public class CheckLVLButton : MonoBehaviour
{
	/*
	 * Use the public LVL key from the Android Market publishing section here.
	 */
	[SerializeField] [Tooltip("Insert LVL public key here")]
	private string m_PublicKey_Base64 = string.Empty;

	/*
	 * Consider storing the public key as RSAParameters.Modulus/.Exponent rather than Base64 to prevent the ASN1 parsing..
	 * These are printed to the logcat below.
	 */
	[SerializeField] [Tooltip("Filled automatically when you input a valid LVL public key above")]
	private string m_PublicKey_Modulus_Base64 = string.Empty;
	
	[SerializeField] [Tooltip("Filled automatically when you input a valid LVL public key above")]
	private string m_PublicKey_Exponent_Base64 = string.Empty;

	[Header("UI")]
	[SerializeField]
	private Button button;

	[SerializeField]
	private Text resultsTextArea = default;

	private RSAParameters m_PublicKey;
	private Random _random;
	private AndroidJavaObject m_Activity;
	private AndroidJavaObject m_LVLCheck;

	private void Start()
	{
		if (string.IsNullOrEmpty(m_PublicKey_Modulus_Base64) || string.IsNullOrEmpty(m_PublicKey_Exponent_Base64))
		{
			DisplayError("Please input a valid LVL public key in the inspector to generate its modulus and exponent");
			return;
		}
		
		bool isRunningInAndroid = new AndroidJavaClass("android.os.Build").GetRawClass() != IntPtr.Zero;
		if (isRunningInAndroid == false)
		{
			DisplayError("Please run this on an Android device!");
			return;
		}

		_random = new Random();
		
		m_PublicKey.Modulus = Convert.FromBase64String(m_PublicKey_Modulus_Base64);
		m_PublicKey.Exponent = Convert.FromBase64String(m_PublicKey_Exponent_Base64);	

		m_Activity = new AndroidJavaClass("com.unity3d.player.UnityPlayer").GetStatic<AndroidJavaObject>("currentActivity");
		m_PackageName = m_Activity.Call<string>("getPackageName");
	}

	private void OnValidate()
	{
		if (string.IsNullOrEmpty(m_PublicKey_Base64) == false)
		{
			try
			{
				RSA.SimpleParseASN1(m_PublicKey_Base64, ref m_PublicKey.Modulus, ref m_PublicKey.Exponent);
			}
			catch (Exception e)
			{
				Debug.LogError($"Please input a valid LVL public key in the inspector to generate its modulus and exponent\n{e.Message}");
				return;
			}
			
			// The reason we keep the modulus and exponent is to avoid a costly call to SimpleParseASN1 at runtime
			m_PublicKey_Modulus_Base64 = Convert.ToBase64String(m_PublicKey.Modulus);
			m_PublicKey_Exponent_Base64 = Convert.ToBase64String(m_PublicKey.Exponent);
			m_PublicKey_Base64 = string.Empty;
		}
		
		button = GetComponent<Button>();
	}

	public void VerifyLicense()
	{
		button.interactable = false;
		
		m_Nonce = _random.Next();

		string results = "<b>Requesting LVL response...</b>\n" +
		                 $"Package name: {m_PackageName}\n" +
		                 $"Request nonce: 0x{m_Nonce:X}";
		DisplayResults(results);
		
		m_LVLCheck = new AndroidJavaObject("com.unity3d.plugin.lvl.ServiceBinder", m_Activity);
		m_LVLCheck.Call("create", m_Nonce, new AndroidJavaRunnable(Process));
	}
	
	private string m_PackageName;
	private int m_Nonce;

	private string m_ResponseCode_Received;
	private string m_PackageName_Received;
	private int m_Nonce_Received;
	private int m_VersionCode_Received;
	private string m_UserID_Received;
	private string m_Timestamp_Received;
	private int m_MaxRetry_Received;
	private string m_LicenceValidityTimestamp_Received;
	private string m_GracePeriodTimestamp_Received;
	private string m_UpdateTimestamp_Received;
	private string m_FileURL1_Received = string.Empty;
	private string m_FileURL2_Received = string.Empty;
	private string m_FileName1_Received;
	private string m_FileName2_Received;
	private int m_FileSize1_Received;
	private int m_FileSize2_Received;
	private string m_LicensingURL_Received = string.Empty;

	private static Dictionary<string, string> DecodeExtras(string query)
	{
		Dictionary<string, string> result = new Dictionary<string, string>();

		if (query.Length == 0)
			return result;

		string decoded = query;
		int decodedLength = decoded.Length;
		int namePos = 0;
		bool first = true;

		while (namePos <= decodedLength)
		{
			int valuePos = -1, valueEnd = -1;
			for (int q = namePos; q < decodedLength; q++)
			{
				if (valuePos == -1 && decoded[q] == '=')
				{
					valuePos = q + 1;
				}
				else if (decoded[q] == '&')
				{
					valueEnd = q;
					break;
				}
			}

			if (first)
			{
				first = false;
				if (decoded[namePos] == '?')
					namePos++;
			}

			string name;

			if (valuePos == -1)
			{
				name = string.Empty;
				valuePos = namePos;
			}
			else
			{
				name = UnityWebRequest.UnEscapeURL(decoded.Substring(namePos, valuePos - namePos - 1));
			}

			if (valueEnd < 0)
			{
				namePos = -1;
				valueEnd = decoded.Length;
			}
			else
			{
				namePos = valueEnd + 1;
			}

			string value = UnityWebRequest.UnEscapeURL(decoded.Substring(valuePos, valueEnd - valuePos));

			result.Add(name, value);
			if (namePos == -1)
				break;
		}
		return result;
	}

	private Int64 ConvertEpochSecondsToTicks(Int64 secs)
	{
		DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		Int64 seconds_to_100ns_ticks	=  10 * 1000;
		Int64 max_seconds_allowed =  (DateTime.MaxValue.Ticks - epoch.Ticks)
												/ seconds_to_100ns_ticks;
		if (secs < 0)
			secs = 0;
		if (secs > max_seconds_allowed)
			secs = max_seconds_allowed;
		return epoch.Ticks + secs * seconds_to_100ns_ticks;
	}

	private void Process()
	{
		button.interactable = true;

		string results = "<b>Requested LVL response</b>\n" +
		                 $"Package name: {m_PackageName}\n" +
		                 $"Request nonce: 0x{m_Nonce:X}\n" +
		                 "------------------------------------------\n" +
		                 "<b>Received LVL response</b>\n";

		if (m_LVLCheck == null)
		{
			results += "m_LVLCheck is null!";
			DisplayResults(results);
			return;
		}

		int responseCode	= m_LVLCheck.Get<int>("_arg0");
		string message		= m_LVLCheck.Get<string>("_arg1");
		string signature	= m_LVLCheck.Get<string>("_arg2");

		m_LVLCheck.Dispose();
		m_LVLCheck = null;

		m_ResponseCode_Received = responseCode.ToString();
		if (responseCode < 0 || string.IsNullOrEmpty(message) || string.IsNullOrEmpty(signature))
		{
			results += "Package name: <Failed>";
			DisplayResults(results);
			return;
		}

		byte[] message_bytes = System.Text.Encoding.UTF8.GetBytes(message);
		byte[] signature_bytes = Convert.FromBase64String(signature);
		RSACryptoServiceProvider csp = new RSACryptoServiceProvider();
		csp.ImportParameters(m_PublicKey);
		SHA1Managed sha1 = new SHA1Managed();
		bool match = csp.VerifyHash(sha1.ComputeHash(message_bytes), CryptoConfig.MapNameToOID("SHA1"), signature_bytes);

		if (!match)
		{
			results += "Response code: <Failed>" +
			           "Package name: <Invalid Signature>";
			DisplayResults(results);
			return;
		}

		int index = message.IndexOf(':');
		string mainData, extraData;
		if (-1 == index)
		{
			mainData = message;
			extraData = string.Empty;
		}
		else
		{
			mainData = message.Substring(0, index);
			extraData = index >= message.Length ? string.Empty : message.Substring(index + 1);
		}

		string[] vars = mainData.Split('|');		// response | nonce | package | version | userid | timestamp

		if (String.Compare(vars[0], responseCode.ToString(), StringComparison.Ordinal) != 0)
		{
			results += "Response code: <Failed>" +
			           "Package name: <Invalid Mismatch>";
			DisplayResults(results);
			return;
		}

		m_ResponseCode_Received		= vars[0];
		m_Nonce_Received			= Convert.ToInt32(vars[1]);
		m_PackageName_Received		= vars[2];
		m_VersionCode_Received		= Convert.ToInt32(vars[3]);
		m_UserID_Received			= vars[4];
		Int64 ticks			= ConvertEpochSecondsToTicks(Convert.ToInt64(vars[5]));
		m_Timestamp_Received		= new DateTime(ticks).ToLocalTime().ToString();

		if (!string.IsNullOrEmpty(extraData))
		{
			Dictionary<string, string> extrasDecoded = DecodeExtras(extraData);

			if (extrasDecoded.ContainsKey("GR"))
			{
				m_MaxRetry_Received = Convert.ToInt32(extrasDecoded["GR"]);
			}
			else
			{
				m_MaxRetry_Received = 0;
			}

			if (extrasDecoded.ContainsKey("VT"))
			{
				ticks = ConvertEpochSecondsToTicks(Convert.ToInt64(extrasDecoded["VT"]));
				m_LicenceValidityTimestamp_Received = new DateTime(ticks).ToLocalTime().ToString();
			}
			else
			{
				m_LicenceValidityTimestamp_Received = null;
			}

			if (extrasDecoded.ContainsKey("GT"))
			{
				ticks = ConvertEpochSecondsToTicks(Convert.ToInt64(extrasDecoded["GT"]));
				m_GracePeriodTimestamp_Received = new DateTime(ticks).ToLocalTime().ToString();
			}
			else
			{
				m_GracePeriodTimestamp_Received = null;
			}

			if (extrasDecoded.ContainsKey("UT"))
			{
				ticks = ConvertEpochSecondsToTicks(Convert.ToInt64(extrasDecoded["UT"]));
				m_UpdateTimestamp_Received = new DateTime(ticks).ToLocalTime().ToString();
			}
			else
			{
				m_UpdateTimestamp_Received = null;
			}

			if (extrasDecoded.ContainsKey("FILE_URL1"))
			{
				m_FileURL1_Received = extrasDecoded["FILE_URL1"];
			}
			else
			{
				m_FileURL1_Received = "";
			}

			if (extrasDecoded.ContainsKey("FILE_URL2"))
			{
				m_FileURL2_Received = extrasDecoded["FILE_URL2"];
			}
			else
			{
				m_FileURL2_Received = "";
			}

			if (extrasDecoded.ContainsKey("FILE_NAME1"))
			{
				m_FileName1_Received = extrasDecoded["FILE_NAME1"];
			}
			else
			{
				m_FileName1_Received = null;
			}

			if (extrasDecoded.ContainsKey("FILE_NAME2"))
			{
				m_FileName2_Received = extrasDecoded["FILE_NAME2"];
			}
			else
			{
				m_FileName2_Received = null;
			}

			if (extrasDecoded.ContainsKey("FILE_SIZE1"))
			{
				m_FileSize1_Received = System.Convert.ToInt32(extrasDecoded["FILE_SIZE1"]);
			}
			else
			{
				m_FileSize1_Received = 0;
			}

			if (extrasDecoded.ContainsKey("FILE_SIZE2"))
			{
				m_FileSize2_Received = System.Convert.ToInt32(extrasDecoded["FILE_SIZE2"]);
			}
			else
			{
				m_FileSize2_Received = 0;
			}
			
			if (extrasDecoded.ContainsKey("LU"))
			{
				m_LicensingURL_Received = extrasDecoded["LU"];
			}
			else
			{
				m_LicensingURL_Received = "";
			}
		}
		
		results += $"Response code: {m_ResponseCode_Received}\n" +
		           $"Package name: {m_PackageName_Received}\n" +
		           $"Received nonce: 0x{m_Nonce_Received:X}\n" +
		           $"Version code: {m_VersionCode_Received}\n" +
		           $"User ID: {m_UserID_Received}\n" +
		           $"Timestamp: {m_Timestamp_Received}\n" +
		           $"Max Retry: {m_MaxRetry_Received}\n" +
		           $"License Validity: {m_LicenceValidityTimestamp_Received}\n" +
		           $"Grace Period: {m_GracePeriodTimestamp_Received}\n" +
		           $"Update Since: {m_UpdateTimestamp_Received}\n" +
		           $"Main OBB URL: {m_FileURL1_Received.Substring(0, Mathf.Min(m_FileURL1_Received.Length,50)) + "..."}\n" +
		           $"Main OBB Name: {m_FileName1_Received}\n" +
		           $"Main OBB Size: {m_FileSize1_Received}\n" +
		           $"Patch OBB URL: {m_FileURL2_Received.Substring(0, Mathf.Min(m_FileURL2_Received.Length,50)) + "..."}\n" +
		           $"Patch OBB Name: {m_FileName2_Received}\n" +
		           $"Patch OBB Size: {m_FileSize2_Received}\n" +
		           $"Licensing URL: {m_LicensingURL_Received.Substring(0, Mathf.Min(m_LicensingURL_Received.Length,50)) + "..."}\n";
		DisplayResults(results);
	}

	private void DisplayResults(string text)
	{
		Debug.Log(text);
		resultsTextArea.text = text;
	}

	private void DisplayError(string text)
	{
		button.interactable = false;
		resultsTextArea.text = text;
		Debug.LogError(text);
	}
}
