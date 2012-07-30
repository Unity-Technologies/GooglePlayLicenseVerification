using UnityEngine;
using System.Collections;
using System.Security.Cryptography;

public class CheckLVLButton : MonoBehaviour
{
	/*
	 * This is the Java service binder classes.jar
	 */
	public TextAsset ServiceBinder;

	/*
	 * Use the public LVL key from the Android Market publishing section here.
	 */
	private string m_PublicKey_Base64 = "<Insert LVL public key here>";

	/*
	 * Consider storing the public key as RSAParameters.Modulus/.Exponent rather than Base64 to prevent the ASN1 parsing..
	 * These are printed to the logcat below.
	 */
	private string m_PublicKey_Modulus_Base64 = "<Set to output from SimpleParseASN1>";
	private string m_PublicKey_Exponent_Base64 = "< .. and here >";
	
	void Start()
	{
		// Either parse the ASN1-formatted public LVL key at runtime (only available when stripping is disabled)..
		RSA.SimpleParseASN1(m_PublicKey_Base64, ref m_PublicKey.Modulus, ref m_PublicKey.Exponent);
		m_PublicKey_Modulus_Base64 = System.Convert.ToBase64String(m_PublicKey.Modulus);
		m_PublicKey_Exponent_Base64 = System.Convert.ToBase64String(m_PublicKey.Exponent);
		// .. and check the logcat for these values ...
		Debug.Log("private string m_PublicKey_Modulus_Base64 = \"" + m_PublicKey_Modulus_Base64 + "\";");
		Debug.Log("private string m_PublicKey_Exponent_Base64 = \"" + m_PublicKey_Exponent_Base64 + "\";");

		// .. or use pre-parsed keys (and remove the code above).
		m_PublicKey.Modulus = System.Convert.FromBase64String(m_PublicKey_Modulus_Base64);
		m_PublicKey.Exponent = System.Convert.FromBase64String(m_PublicKey_Exponent_Base64);
		
		m_RunningOnAndroid = new AndroidJavaClass("android.os.Build").GetRawClass() != System.IntPtr.Zero;
		if (!m_RunningOnAndroid)
			return;

		LoadServiceBinder();

		new SHA1CryptoServiceProvider();	// keep a dummy reference to prevent too aggressive stripping

		m_ButtonMessage = "Check LVL";
	}

	private RSAParameters m_PublicKey = new RSAParameters();

	/*
	 *
	 */
	private void LoadServiceBinder()
	{
		byte[] classes_jar = ServiceBinder.bytes;

		System.IO.File.WriteAllBytes(Application.temporaryCachePath + "/classes.jar", classes_jar);
		System.IO.Directory.CreateDirectory(Application.temporaryCachePath + "/odex");

		m_Activity = new AndroidJavaClass("com.unity3d.player.UnityPlayer").GetStatic<AndroidJavaObject>("currentActivity");
		AndroidJavaObject dcl = new AndroidJavaObject("dalvik.system.DexClassLoader",
		                                              Application.temporaryCachePath + "/classes.jar",
		                                              Application.temporaryCachePath + "/odex",
		                                              null,
		                                              m_Activity.Call<AndroidJavaObject>("getClassLoader"));
		m_LVLCheckType = dcl.Call<AndroidJavaObject>("findClass", "com.unity3d.plugin.lvl.ServiceBinder");

		System.IO.Directory.Delete(Application.temporaryCachePath, true);

		m_PackageName = m_Activity.Call<string>("getPackageName");
	}
	
	private bool m_RunningOnAndroid = false;

	private AndroidJavaObject m_Activity;
	private AndroidJavaObject m_LVLCheckType;

	private AndroidJavaObject m_LVLCheck = null;

	private string m_ButtonMessage = "Invalid LVL key!\nCheck the source...";
	private bool m_ButtonEnabled = true;

	private string m_PackageName;
	private int m_Nonce;

	private bool m_LVL_Received = false;
	private string m_ResponseCode_Received;
	private string m_PackageName_Received;
	private int m_Nonce_Received;
	private int	m_VersionCode_Received;
	private string m_UserID_Received;
	private string m_Timestamp_Received;

	void OnGUI()
	{
		if (!m_RunningOnAndroid)
		{
			GUI.Label(new Rect(10, 10, Screen.width-10, 20), "Use LVL checks only on the Android device!");
			return;
		}
		GUI.enabled = m_ButtonEnabled;
		if (GUI.Button(new Rect(10,10, 450, 300), m_ButtonMessage))
		{
			m_Nonce = new System.Random().Next();

			object[] param = new object[] { new AndroidJavaObject[]{ m_Activity } };
			AndroidJavaObject[] ctors = m_LVLCheckType.Call<AndroidJavaObject[]>("getConstructors");
			m_LVLCheck = ctors[0].Call<AndroidJavaObject>("newInstance", param);
			m_LVLCheck.Call("create", m_Nonce, new AndroidJavaRunnable(Process) );

			m_ButtonMessage = "Checking...";
			m_ButtonEnabled = false;
		}
		GUI.enabled = true;

		if (m_LVLCheck != null || m_LVL_Received)
		{
			GUI.Label(new Rect(10, 320, 450, 20), "Requesting LVL response:");
			GUI.Label(new Rect(20, 340, 450, 20), "Package name  = " + m_PackageName);
			GUI.Label(new Rect(20, 360, 450, 20), "Request nonce = 0x" + m_Nonce.ToString("X"));
		}

		if (m_LVLCheck == null && m_LVL_Received)
		{
			GUI.Label(new Rect(10, 420, 450, 20), "Received LVL response:");
			GUI.Label(new Rect(20, 440, 450, 20), "Response code  = " + m_ResponseCode_Received);
			GUI.Label(new Rect(20, 460, 450, 20), "Package name   = " + m_PackageName_Received);
			GUI.Label(new Rect(20, 480, 450, 20), "Received nonce = 0x" + m_Nonce_Received.ToString("X"));
			GUI.Label(new Rect(20, 500, 450, 20), "Version code = " + m_VersionCode_Received);
			GUI.Label(new Rect(20, 520, 450, 20), "User ID   = " + m_UserID_Received);
			GUI.Label(new Rect(20, 540, 450, 20), "Timestamp = " + m_Timestamp_Received);
		}
	}

	private void Process()
	{
		m_LVL_Received = true;
		m_ButtonMessage = "Check LVL";
		m_ButtonEnabled = true;

		if (m_LVLCheck == null)
			return;

		int responseCode	= m_LVLCheck.Get<int>("_arg0");
		string message		= m_LVLCheck.Get<string>("_arg1");
		string signature	= m_LVLCheck.Get<string>("_arg2");

		m_LVLCheck = null;

		m_ResponseCode_Received = responseCode.ToString();
		if (responseCode < 0 || message == null || signature == null)
		{
			m_PackageName_Received = "<Failed>";
			return;
		}

		byte[] message_bytes = System.Text.Encoding.UTF8.GetBytes(message);
		byte[] signature_bytes = System.Convert.FromBase64String(signature);
		RSACryptoServiceProvider csp = new RSACryptoServiceProvider();
		csp.ImportParameters(m_PublicKey);
		SHA1Managed sha1 = new SHA1Managed();
		bool match = csp.VerifyHash(sha1.ComputeHash(message_bytes), CryptoConfig.MapNameToOID("SHA1"), signature_bytes);

		if (!match)
		{
			m_ResponseCode_Received = "<Failed>";
			m_PackageName_Received = "<Invalid Signature>";
			return;
		}

		string[] vars = message.Split('|');		// response | nonce | package | version | userid | timestamp

		if (vars[0].CompareTo(responseCode.ToString()) != 0)
		{
			m_ResponseCode_Received = "<Failed>";
			m_PackageName_Received = "<Response Mismatch>";
			return;
		}

		m_ResponseCode_Received		= vars[0];
		m_Nonce_Received			= System.Convert.ToInt32(vars[1]);
		m_PackageName_Received		= vars[2];
		m_VersionCode_Received		= System.Convert.ToInt32(vars[3]);
		m_UserID_Received			= vars[4];
		System.Int64 ticks			= System.Convert.ToInt64(vars[5]) * 10 * 1000;
		System.DateTime epoch		= new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);
		m_Timestamp_Received		= epoch.AddTicks(ticks).ToLocalTime().ToString();
	}
}
