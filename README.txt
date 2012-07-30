Google Play Application License Verification
----------------------------------------------

Author: Erik Hemming / Unity Technologies (twitter: _eriq_)

About the plugin
----------------

This is an example of how Google Play Application Licensing [1] can be integrated into any Unity application, with a minimum of additional Java code (everything except the Service Binder is written in C#). The plugin also loads the Java code loaded at runtime [2] (i.e. the classes.dex in the .apk does no include any LVL code).

The code can also be used in conjunction with an online verification mechanism [3], where the result of the LVL check is sent to a server for proper inspection.

For detailed explanation on Google Play License Verification mechanism, please have at the very informative 'Evading Pirates and Stopping Vampires' [4] presentation from Google I/O 2011.

[1] http://developer.android.com/guide/market/licensing/index.html
[2] http://android-developers.blogspot.com/2011/07/custom-class-loading-in-dalvik.html
[3] http://code.google.com/p/android-market-license-verification
[4] http://www.google.com/events/io/2011/sessions/evading-pirates-and-stopping-vampires-using-license-verification-library-in-app-billing-and-app-engine.html


How to use
----------

The logic of the plugin is centered around a C# script file, CheckLVLButton.cs, which serves as an example how the LVL check could be integrated in an existing application. This single C# file is responsible for loading the Java code, setting up and calling the LVL backend, and finally deciphering the result.

Please note that the plugin cannot work without the public LVL key, which can be obtained from the publishing account at Google Play. Have a look in CheckLVLButton.cs for the line
	m_PublicKey_Base64 = "<Insert LVL public key here>";
and replace the string with your personal LVL key.


How to (Re-)Compile the Java source
-----------------------------------

First, make sure you have the JDK [1] and ANT [2] installed - while OSX usually comes with these pre-installed, Windows users need to download and install them manually. Then, make sure you have a fairly recent Android SDK [3] - at least API-15.

Before compiling the code the project must be initialized to have the local.properties file updated with path to the (local) Android SDK:

	$ cd <project_root>/Assets/LicenseVerification/JavaSource
	$ cp ../../Plugins/Android/AndroidManifest.xml .
	$ android update project -p .

Now you can use
	$ ant help		to display the help message.
	$ ant clean		to remove output files created by the build target.
	$ ant build		to build the classes.jar (classes.txt) library for use with Unity Android.

[1] http://www.oracle.com/technetwork/java/javase/downloads/index.html
[2] http://ant.apache.org/
[3] http://developer.android.com/sdk/index.html


Detailed instructions how the plugin was created
------------------------------------------------

1) Add the patched manifest

1.1) Create the Android plugins directory.

	$ mkdir <project_root>/Assets/Plugins/Android
	$ cd <project_root>/Assets/Plugins/Android

1.2) Copy the default AndroidManifest.xml used by Unity. This is the normal path used on OSX - the Windows path starts with C:\Program Files\Unity\Data\

	$ cp /Applications/Unity/Unity.app/Contents/PlaybackEngines/AndroidPlayer/AndroidManifest.xml .

1.3) Edit the AndroidManifest.xml, and add permissions related to LVL.

	$ diff -rupN /Applications/Unity/Unity.app/Contents/PlaybackEngines/AndroidPlayer/AndroidManifest.xml AndroidManifest.xml
	--- /Applications/Unity/Unity.app/Contents/PlaybackEngines/AndroidPlayer/AndroidManifest.xml	2012-03-27 22:25:58.000000000 +0200
	+++ AndroidManifest.xml	2012-03-30 18:41:32.000000000 +0200
	@@ -39,4 +39,9 @@
	                   android:configChanges="fontScale|keyboard|keyboardHidden|locale|mnc|mcc|navigation|orientation|screenLayout|screenSize|smallestScreenSize|uiMode|touchscreen">
	         </activity>
	     </application>
	+
	+<!-- patched manifest starts here -->
	+    <uses-permission android:name="android.permission.INTERNET" />
	+    <uses-permission android:name="android.permission.WRITE_EXTERNAL_STORAGE" />
	+    <uses-permission android:name="com.android.vending.CHECK_LICENSE" />
	 </manifest>


2) Create JavaSource project

2.1) Create ANT project

	$ mkdir <project_root>/Assets/LicenseVerification/JavaSource
	$ cd <project_root>/Assets/LicenseVerification/JavaSource
	$ android create project -t android-15 -p . -k com.unity3d.plugin.lvl -a dummy_activity

2.2) Remove generated files which we don't use.

	$ rm -rf res/
	$ rm src/com/unity3d/plugin/lvl/dummy_activity.java


2.3) Edit build.xml to add support for obfuscated .jar generation

	$ diff -rupN build_dist.xml build.xml
--- build_dist.xml	2012-03-30 18:36:07.000000000 +0200
+++ build.xml	2012-03-31 11:38:00.000000000 +0200
@@ -80,4 +80,60 @@
     <!-- version-tag: 1 -->
     <import file="${sdk.dir}/tools/ant/build.xml" />
 
	+    <target name="-code-gen">
	+    </target>
	+
	+    <target name="-create-jar" depends="-compile,-post-compile">
	+        <property name="jar-name" value="lic_check_plain.jar" />
	+        <property name="plugin.jar" value="${out.absolute.dir}/${jar-name}" />
	+
	+        <zip basedir="${out.classes.absolute.dir}"
	+             destfile="${plugin.jar}"
	+             filesonly="true"
	+             excludes="**/*.meta"
	+             />
	+    </target>
	+
	+    <target name="-obfuscate" depends="-create-jar">
	+        <property name="obfuscated-name" value="lic_check_obfuscated.jar" />
	+        <property name="obfuscated.jar" value="${out.absolute.dir}/${obfuscated-name}" />
	+
	+        <property name="proguard.jar" location="${android.tools.dir}/proguard/lib/proguard.jar" />
	+        <taskdef name="proguard" classname="proguard.ant.ProGuardTask" classpath="${proguard.jar}" />
	+
	+        <pathconvert property="android.libraryjars" refid="android.target.classpath">
	+            <firstmatchmapper>
	+                <regexpmapper from='^([^ ]*)( .*)$$' to='"\1\2"'/>
	+                <identitymapper/>
	+            </firstmatchmapper>
	+        </pathconvert>
	+                
	+        <proguard>
	+            -include      "${proguard.config}"
	+            -injars       ${plugin.jar}
	+            -outjars      "${obfuscated.jar}"
	+            -libraryjars  "${android.libraryjars}"
	+        </proguard>
	+    </target>
	+
	+    <target name="build" depends="-dex">
	+        <property name="zip.file" location="${out.absolute.dir}/classes.zip" />
	+        <property name="out.name" value="classes_jar.txt" />
	+        <property name="out.path" location="../" />
	+        <property name="out.file" location="${out.path}/${out.name}" />
	+        <zip basedir="${out.absolute.dir}"
	+             includes="classes.dex"
	+             destfile="${zip.file}"
	+             filesonly="true"
	+             excludes="**/*.meta"
	+             />
	+        <copy file="${zip.file}" tofile="${out.file}"/>
	+    </target>
	+
	+    <target name="help">
	+        <echo>Android Ant Build. Available targets:</echo>
	+        <echo>   help:      Displays this help.</echo>
	+        <echo>   clean:     Removes output files created by the build target.</echo>
	+        <echo>   build:     Builds the classes.jar (classes.txt) library for use with Unity Android.</echo>
	+    </target>
	 </project>

2.4) Enable ProGuard obfuscation by adding a line to project.properties :

	proguard.config=proguard-project.txt

2.5) Edit the proguard-project.txt file, and add :

	-target 1.6
	-optimizationpasses 2
	-dontusemixedcaseclassnames
	-dontskipnonpubliclibraryclasses
	-dontpreverify
	-keepattributes InnerClasses,EnclosingMethod
	
	-optimizations !code/simplification/arithmetic
	
	-keep class * { public <methods>; !private *; }

3) Add some C# code load the Java classes_jar.txt

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
