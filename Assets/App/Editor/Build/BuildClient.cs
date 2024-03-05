using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
//using DG.DemiEditor;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions;


#endif


namespace Cocone.ProjectP3
{
	public struct PlayerBuildConfig
	{
		public int buildNumber;
		public string outputDirectory;
		public BuildTarget buildTarget;
		public BuildOptions options;
		public bool useAndroidAppBundle;
		public bool uploadToStore;
		public string keystorePass;
		public string keyaliasPass;
		public string buildKind;
		public string assetKind;
		public HashSet<AndroidArchitecture> androidArchitectures;

		public BuildTargetGroup TargetGroup
		{
			get
			{
				switch (buildTarget)
				{
					case BuildTarget.StandaloneWindows64: return BuildTargetGroup.Standalone;
					case BuildTarget.StandaloneOSX: return BuildTargetGroup.Standalone;
					case BuildTarget.Android: return BuildTargetGroup.Android;
					case BuildTarget.iOS: return BuildTargetGroup.iOS;

					default: return BuildTargetGroup.Unknown;
				}
			}
		}

		public string GetOutputPath(string productName)
		{
			Debug.Log("OutputPath:" + outputDirectory + " / "+ productName);
			var path = Path.Combine(outputDirectory, productName);

			switch (buildTarget)
			{
				case BuildTarget.StandaloneWindows64: return path + ".exe";
				case BuildTarget.StandaloneOSX: return path + ".app";
				case BuildTarget.Android: return useAndroidAppBundle ? path + ".aab" : path + ".apk";

				// iosはXcodeProjectがBuildされるためフォルダを指定
				case BuildTarget.iOS: return outputDirectory;

				default: return path;
			}		
		}

		// ビルド種別として追加。不要であれば、他とマージしてください
		public enum BuildKind
		{
			None,
			Dev,
			QA,
			Beta,
			Billing,
			Release
		}
	}

	public static class Client
	{
		private static bool manual = false;
		private const string AndroidManifestPath = "Assets/Plugins/Android/AndroidManifest.xml";

		/**
		 * ビルド種別の取得
		 */
		public static PlayerBuildConfig.BuildKind GetBuildKind(string kind)
		{
			if (!string.IsNullOrEmpty(kind))
			{
				if (Enum.TryParse<PlayerBuildConfig.BuildKind>(kind, out var buildKind))
				{
					return buildKind;
				}
			}
			return PlayerBuildConfig.BuildKind.None;
		}
		
		// NOTE:OnPostProcessBuild()に引数として渡せないのでプロパティとして宣言
		// ios向けのビルドには必須なので設定必須
		private static PlayerBuildConfig playerBuildConfig { get; set; }

		/// <summary>
		/// BuildConfigurationを取得
		/// </summary>
		private static BuildConfiguration buildConfigurations = null;
		public static BuildConfiguration BuildConfigurations
		{
			get
			{
				if (buildConfigurations == null)
				{
					if (PlayerBuildConfig.BuildKind.None != GetBuildKind(playerBuildConfig.buildKind))
					{
						var path = $"Assets/App/Editor/Build/Configs/{playerBuildConfig.buildKind}BuildSettings.yaml";
						if (File.Exists(path))
						{
							buildConfigurations = BuildConfiguration.Deserialize(path);
						}
						else
						{
							Debug.Log($"{path}ファイルを開けません。");
						}
					}
				}

				return buildConfigurations;
			}
		}

		// Export用のplistパスを返す
		public static string ExportPlistPath
		{
			get
			{
				// export用のplistから、provisioning関連の設定を拝借
				return $"./Assets/App/Editor/Build/Configs/iOS/ExportOptions_{playerBuildConfig.buildKind}.plist";
			}
		}

		// コマンドテストメニュー(xcodeproject生成）
		// NOTE: コマンドでのテストビルドをメニューから行うためのものです
		// 実際にjenkinsでは動かしません
		// あとで消す可能性あるのでtmpなメソッド
		[MenuItem("P3/Build/コマンド(テスト用)/iOS/Dev")]
		public static void BuildCommandTestiOSDev()
		{
			var workspace = ".";
			var unityMethod = "Cocone.ProjectP3.Client.Build";
			var buildTarget = "iOS";
			var build_id = "000";
			var output_path = "build_ios";
			
			string[] args = {
				"-projectPath", workspace,
				"-quit","-batchmode", 
				"-executeMethod", unityMethod,
				"-buildTarget", buildTarget,
				"-BuildNumber", build_id,
				"-OutputPath", output_path,
				"-buildKind", "Dev"
			};

			Build(args);
		}
		
		[MenuItem("P3/Build/コマンド(テスト用)/iOS/Beta")]
		public static void BuildCommandTestiOSBeta()
		{
			var workspace = ".";
			var unityMethod = "Cocone.ProjectP3.Client.Build";
			var buildTarget = "iOS";
			var build_id = "000";
			var output_path = "build_ios";
			
			string[] args = {
				"-projectPath", workspace,
				"-quit","-batchmode", 
				"-executeMethod", unityMethod,
				"-buildTarget", buildTarget,
				"-BuildNumber", build_id,
				"-OutputPath", output_path,
				"-buildKind", "Beta"
			};

			Build(args);
		}
		
		// コマンドテストメニュー(xcodeproject生成）
		// NOTE: コマンドでのテストビルドをメニューから行うためのものです
		// 実際にjenkinsでは動かしません
		// あとで消す可能性あるのでtmpなメソッド
		[MenuItem("P3/Build/コマンド(テスト用)/iOS/Release")]
		public static void BuildCommandTestiOSRelease()
		{
			var workspace = ".";
			var unityMethod = "Cocone.ProjectP3.Client.Build";
			var buildTarget = "iOS";
			var build_id = "120";
			var output_path = "build_ios";
			
			string[] args = {
				"-projectPath", workspace,
				"-quit","-batchmode", 
				"-executeMethod", unityMethod,
				"-buildTarget", buildTarget,
				"-BuildNumber", build_id,
				"-OutputPath", output_path,
				"-buildKind", "Release",
			};

			Build(args);
		}
		
		[MenuItem("P3/Build/コマンド(テスト用)/Android/Dev")]
		public static void BuildCommandTestAndroidDev()
		{
			var workspace = ".";
			var unityMethod = "Cocone.ProjectP3.Client.Build";
			var buildTarget = "Android";
			var build_id = "100"; // 在这个位置执行的话缺乏个递增机制，jenkins里这个数字是来源于jenkins那个项目自带的buildid 
			var output_path = "build_android";
			
			string[] args = {
				"-projectPath", workspace,
				"-quit","-batchmode", 
				"-executeMethod", unityMethod,
				"-buildTarget", buildTarget,
				"-BuildNumber", build_id,
				"-logFile", workspace+"/Logs/build_"+build_id+"_log.txt",
				"-OutputPath", workspace +"/" +output_path,
				"-buildKind", "Dev",//Dev，QA，Beta？
				"-keystorePass", "890710gxy", // TODO:unityから入力させたい
				"-keyaliasPass", "890710gxy", // TODO:unityから入力させたい
				"-androidArchitectures", "ARMv7;Arm64",// or Arm64
				"-developmentBuild", "false"
			};
			manual = true;
			Build(args);
		}
		
		[MenuItem("P3/Build/コマンド(テスト用)/Android/Release(コード書き換え必要）")]
		public static void BuildCommandTestAndroidRelease()
		{
			var workspace = ".";
			var unityMethod = "Cocone.ProjectP3.Client.Build";
			var buildTarget = "Android";
			var build_id = "120";
			var output_path = "build_android";
			
			string[] args = {
				"-projectPath", workspace,
				"-quit","-batchmode", 
				"-useAndroidAppBundle", "-uploadToStore",
				"-executeMethod", unityMethod,
				"-buildTarget", buildTarget,
				"-BuildNumber", build_id,
				"-OutputPath", output_path,
				"-buildKind", "Release",
				"-keystorePass", "890710gxy", // TODO:unityから入力させたい
				"-keyaliasPass", "890710gxy", // TODO:unityから入力させたい
				"-androidArchitectures", "ARMv7;ARM64",
			};
			manual = true;
			Build(args);
		}
		
		public static void Build()
		{
			var command = Environment.CommandLine;
			var args = command.Split(' ');

			Build(args);
		}
		
		public static void Build(string[] args)
		{
			Debug.Log("---------------- Start Batch Build Script ------------------");
			
			var config = new PlayerBuildConfig
			{
				options = BuildOptions.None,
				useAndroidAppBundle = false
			};

			for (int i = 0; i < args.Length; i++)
			{
				switch (args[i])
				{
					case "-developmentBuild":
						if (args[i + 1] == "true")
						{
							config.options = BuildOptions.Development;
						}
						break;
					case "-BuildNumber":
						config.buildNumber = int.Parse(args[i + 1]);
						i++;
						break;

					case "-OutputPath":
						config.outputDirectory = args[i + 1];
						i++;
						break;

					case "-buildTarget":
						config.buildTarget = (BuildTarget) Enum.Parse(typeof(BuildTarget), args[i + 1]);
						i++;
						break;

					case "-useAndroidAppBundle":
						config.useAndroidAppBundle = true;
						break;
					
					case "-uploadToStore":
						config.uploadToStore = true;
						break;

					case "-keystorePass":
						config.keystorePass = args[i + 1];
						i++;
						break;
					
					case "-keyaliasPass":
						config.keyaliasPass = args[i + 1];
						i++;
						break;

					case "-androidArchitectures":
						config.androidArchitectures = new HashSet<AndroidArchitecture>();
						
						var architectures = args[i + 1].Split(';');
						foreach (var architecture in architectures)
						{
							if (Enum.TryParse<AndroidArchitecture>(architecture, out var androidArchitecture))
							{
								config.androidArchitectures.Add(androidArchitecture);
							}
						}
						
						i++;
						break;
					
					case "-buildKind":
						config.buildKind = args[i + 1];
						i++;
						break;
				}
			}
			
			// configの設定
			playerBuildConfig = config;
			var report = Build(config);
			
			if (!manual)
				EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
		}

		/**
		 * ビルド種別に応じたシーンリストを返す
		 */
		private static string[] GetTargetScenes(PlayerBuildConfig.BuildKind kind)
		{
			return EditorBuildSettings.scenes.Select(x => x.path).ToArray();
		}
		
		/// <summary>
		/// BuildConfigurationのyamlファイルからPlayerSettingsへ設定を行う
		/// </summary>
		/// <param name="kind"></param>
		/// <param name="target"></param>
		/// <param name="group"></param>
		/// <returns></returns>
		private static void SetPlayerSettingsByBuildConfiguration(PlayerBuildConfig.BuildKind kind, BuildTarget target, BuildTargetGroup group)
		{
			// PlayerSettingsの設定
			if (BuildConfigurations != null)
			{
				Debug.Log("BuildConfigurations.applicationIdentifier:"+ BuildConfigurations.applicationIdentifier);
				
				if (!string.IsNullOrEmpty(BuildConfigurations.applicationIdentifier))
				{
					PlayerSettings.SetApplicationIdentifier(group, BuildConfigurations.applicationIdentifier);	
				}

				if (!string.IsNullOrEmpty(BuildConfigurations.productName))
				{
					PlayerSettings.productName = BuildConfigurations.productName;
				}
				
				// addDefineSymbolsの設定
				// if (BuildConfigurations.addDefineSymbols != null && BuildConfigurations.addDefineSymbols.Any())
				// {
				// 	// 既存シンボルを取得
				// 	var currentSymbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
				// 	var currentSymbolsList = currentSymbols.Split(';').ToList();
				// 	// シンボルを追加（重複していたら削除）
				// 	foreach (var symbol in BuildConfigurations.addDefineSymbols)
				// 	{
				// 		currentSymbolsList.Add(symbol);
				// 	}
				// 	currentSymbolsList = currentSymbolsList.Distinct().ToList();
				//
				// 	// シンボルを再設定
				// 	PlayerSettings.SetScriptingDefineSymbolsForGroup(group, currentSymbolsList.ToArray());
				// }
			}
		}

		/// <summary>
		/// Android向けのアーキテクチャを取得
		/// </summary>
		/// <param name="architectures"></param>
		/// <returns></returns>
		private static AndroidArchitecture GetAndroidArchitectures(IEnumerable<AndroidArchitecture> architectures)
		{
			var result = AndroidArchitecture.None;
			foreach (var arch in architectures)
			{
				result |= arch;
			}

			return result;
		}
		
		private static BuildReport Build(PlayerBuildConfig config)
		{
			// Yamlの読み込みと設定
			SetPlayerSettingsByBuildConfiguration(GetBuildKind(config.buildKind), config.buildTarget, config.TargetGroup);
			
			// AppCenterの設定を環境で変更する
			ConfigurationSetter.SetAppCenterParam(GetBuildKind(config.buildKind) != PlayerBuildConfig.BuildKind.Release);

			// 既存シンボルを取得
			// var currentSymbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(config.TargetGroup);
			// var currentSymbolsList = currentSymbols.Split(';').ToList();
			// if (GetBuildKind(config.buildKind) == PlayerBuildConfig.BuildKind.Release)
			// {
			// 	// DEBUG関連シンボル削除
			// 	currentSymbolsList.RemoveAll(x => x.StartsWith("DEBUG_"));
			// }
			// // シンボルを再設定
			// PlayerSettings.SetScriptingDefineSymbolsForGroup(config.TargetGroup, currentSymbolsList.ToArray());
			
			if (config.buildTarget == BuildTarget.iOS)
			{
				// CFBundleVersionの指定
				PlayerSettings.iOS.buildNumber = config.buildNumber.ToString();
			}
			else if (config.buildTarget == BuildTarget.Android)
			{
				if (File.Exists(AndroidManifestPath))
				{
					var originManifest = File.ReadAllText(AndroidManifestPath);
					var text = originManifest.Replace("${app_id}", BuildConfigurations.applicationIdentifier);
					File.WriteAllText(AndroidManifestPath, text);
				}
				
				// architecturesの設定
				if (config.androidArchitectures != null && config.androidArchitectures.Any())
				{
					PlayerSettings.Android.targetArchitectures = GetAndroidArchitectures(config.androidArchitectures);
				}

				// 毎回設定する必要がある
				EditorUserBuildSettings.buildAppBundle = config.useAndroidAppBundle;
				
				// ビルド番号を付与
				PlayerSettings.Android.bundleVersionCode = config.buildNumber;
				
				//if (config.uploadToStore)
				{
					PlayerSettings.Android.useCustomKeystore = true;
					if (!string.IsNullOrEmpty(BuildConfigurations.keystoreName))
					{
						PlayerSettings.Android.keystoreName = BuildConfigurations.keystoreName;
					}
						
					if (!string.IsNullOrEmpty(BuildConfigurations.keyaliasName))
					{
						PlayerSettings.Android.keyaliasName = BuildConfigurations.keyaliasName;
					}
					
					Debug.Log("PlayerSettings.Android.keystorePass :" + config.keystorePass);
					Debug.Log("PlayerSettings.Android.keyaliasPass :" + config.keyaliasPass);
					Debug.Log("PlayerSettings.Android.keystoreName :" + BuildConfigurations.keystoreName);
					Debug.Log("PlayerSettings.Android.keyaliasName :" + BuildConfigurations.keyaliasName);
					
					PlayerSettings.Android.keystorePass = config.keystorePass;
					PlayerSettings.Android.keyaliasPass = config.keyaliasPass;
				}
			}
			
			var report = BuildPipeline.BuildPlayer(
				new BuildPlayerOptions
				{
					scenes = GetTargetScenes(GetBuildKind(config.buildKind)),
					locationPathName = config.GetOutputPath(PlayerSettings.productName),
					target = config.buildTarget,
					targetGroup = config.TargetGroup,
					options = config.options,
				}
			);
			
			Debug.Log(
				$"[Result:{report.summary.result}] " +
				$"[Output:{report.summary.outputPath}] " +
				$"[TotalSize:{report.summary.totalSize}] " +
				$"[BuildTime:{report.summary.totalTime}] " +
				$"[Error:{report.summary.totalErrors}] " +
				$"[Warning:{report.summary.totalWarnings}] "
			);

			return report;
		}
		
#if UNITY_IOS
		/**
		 * アプリケーションのデフォルト設定plistを取得
		 * (Unity GUIにて設定する項目がなさそうなのでコード上で指定する）
		 */
		private static PlistDocument GetDefaultPlistDocument(string plistPath)
		{
			// アプリ管理のInfo.plistを設定
			var plist = new PlistDocument();
			if (File.Exists(plistPath))
			{
				plist.ReadFromFile(plistPath);	
			}

			// 日本語に設定
			plist.root.SetString("CFBundleDevelopmentRegion", "Japan");
			var localizations = plist.root.CreateArray("CFBundleLocalizations");
			localizations.AddString ("Japanese");
			
			if (BuildConfigurations != null)
			{
				if (!string.IsNullOrEmpty(BuildConfigurations.cfBundleName))
				{
					// ipaのファイル名を変更（PlayerSettings.productNameが反映されないみたいなので、ここで尚設定）
					plist.root.SetString("CFBundleName", BuildConfigurations.cfBundleName);
				}

				if (!string.IsNullOrEmpty(BuildConfigurations.cfBundleExecutableName))
				{
					// unzipした場合の中身のappパッケージの名称を設定する
					plist.root.SetString("CFBundleExecutable", BuildConfigurations.cfBundleExecutableName);	
				}
				
				// if (!string.IsNullOrEmpty(BuildConfigurations.appDisplayName))
				// {
				// 	// アプリの表示名を変更する
				// 	plist.root.SetString("CFBundleDisplayName", BuildConfigurations.appDisplayName);	
				// }
			}
			
			// LLVMの中間コードBitCodeを再コンパイルするかどうか
			plist.root.SetBoolean("compileBitcode", false);
				
			// libzを使う
			plist.root.SetBoolean("libz.1.2.11.tbd", false);
			
			// 輸出コンプライアンス設定（審査でうるさく聞かれるのを回避）
			plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
			
			// iOS14 AppTrackingポップアップの表示用
			// var attMessage = "許可をした場合、本サービスで収集したお客様の情報をアプリの品質の向上に役立たせていただきます。" +
			//                  "\n今後のサービス改善のため、トラッキングの設定をお願いします。" +
			//                  "\n※トラッキングの設定は端末の設定からいつでも変更可能です。";
			// plist.root.SetString("NSUserTrackingUsageDescription", attMessage);
			
			//URL Schemes
			AddURLSchemes(plist);
			AddAllowURLSchemes(plist);
			
			// 保存
			plist.WriteToFile(plistPath);
			return plist;
		}
		
		/// <summary>
		/// plistの署名設定を適用
		/// NOTE:本来であればpbxproject生成前に、iOSProvisioningProfileIDの値を指定するべきだが、
		/// Export用のplistにも同様の情報の記載があるので、こちらで設定
		/// </summary>
		/// <param name="project"></param>
		/// <param name="plist"></param>
		private static void SetSignByExportProfile(PBXProject project, PlistDocument plist, string guid)
		{
			if (plist.root.values.TryGetValue("Bundle identifier", out var bundleIdentifier))
			{
				project.SetBuildProperty(guid, "PRODUCT_BUNDLE_IDENTIFIER", bundleIdentifier.AsString());

				if (plist.root.values.TryGetValue("provisioningProfiles", out var profiles))
				{
					var provisioningProfiles = profiles as PlistElementDict;
					if (provisioningProfiles != null && provisioningProfiles.values.TryGetValue(bundleIdentifier.AsString(), out var provisioningProfileSpecifier))
					{
						project.SetBuildProperty(guid, "PROVISIONING_PROFILE_SPECIFIER", provisioningProfileSpecifier.AsString());					
					}
				}
			}

			if (plist.root.values.TryGetValue("teamID", out var teamId))
			{
				project.SetBuildProperty(guid, "DEVELOPMENT_TEAM", teamId.AsString());
			}

			if (plist.root.values.TryGetValue("signingCertificate", out var codeSignIdentity))
			{
				project.SetBuildProperty(guid, "CODE_SIGN_IDENTITY", codeSignIdentity.AsString());
			}
			
			project.SetBuildProperty(guid, "CODE_SIGN_STYLE", "Manual");// other option: Automatic
		}
		
		/// <summary>
		/// 署名やTeamIDを設定する
		/// </summary>
		/// <param name="project"></param>
		private static void SetSignByExportProfile(PBXProject project)
		{
			// export用のplistから、provisioning関連の設定を拝借
			var plist = new PlistDocument();
			if (!File.Exists(ExportPlistPath))
			{
				return;
			}

			plist.ReadFromFile(ExportPlistPath);

			// plistの設定流し込む
			var mainTargetGuid = project.GetUnityMainTargetGuid();
			var frameworkTargetGuid = project.GetUnityFrameworkTargetGuid();
			
			SetSignByExportProfile(project, plist, mainTargetGuid);
		}
		
		[PostProcessBuild(100)]
		/**
		 * pbxprojectの生成およびplistファイルの設定を行う
		 * NOTE:provisioningProfileの設定はファイルの指定をplistに対して行っているが、
		 * PlayerSettings.iOS.iOSManualProvisioningProfileID に設定することも可能。
		 * しかし、ビルド時に渡すUUIDなど適宜更新する必要が発生するので、plistでのファイル名指定の方が
		 * 変更点が少なく運用が楽。
		 */
		public static void OnPostProcessBuild(BuildTarget buildTarget, string path)
		{
			Debug.Log($"--------------- OnPostProcessBuild :{buildTarget.ToString()} / {path} --------------");
			if (buildTarget == BuildTarget.iOS)
			{
				// アプリ管理のInfo.plistを設定
				var plistPath = Path.Combine(path, "Info.plist");
				var plist = GetDefaultPlistDocument(plistPath);
				
				var projectPath = PBXProject.GetPBXProjectPath(path);

				if (BuildConfigurations != null && !string.IsNullOrEmpty(BuildConfigurations.cfBundleName))
				{
					// PRODUCT_NAME_APPの差し替え（NOTE:PlayerSettingsで変更できそうにないのでここで行う)
					// どこからこの設定値が来ているか不明情報求む
					// productNameが微妙に連動していそうだが、合致はしていない。直値指定なので注意すること
					var pbxProjectContent = File.ReadAllText(projectPath);
					pbxProjectContent = pbxProjectContent.Replace("PRODUCT_NAME_APP = mcombat;", $"PRODUCT_NAME_APP = {BuildConfigurations.cfBundleName};");
					File.WriteAllText(projectPath, pbxProjectContent);
				}

				var project = new PBXProject();
				project.ReadFromFile(projectPath);

				var mainTargetGuid = project.GetUnityMainTargetGuid();
				var frameworkTargetGuid = project.GetUnityFrameworkTargetGuid();
				
				// debug/releaseの情報を取得
				var debug = project.BuildConfigByName(mainTargetGuid, "Debug");
				var release = project.BuildConfigByName(mainTargetGuid, "Release");

				// DWARFの設定 (dSYM使うかどうか) debug/releaseで設定が違うので注意
				project.SetBuildPropertyForConfig(debug, "DEBUG_INFORMATION_FORMAT", "DWARF");
				project.SetBuildPropertyForConfig(release, "DEBUG_INFORMATION_FORMAT", "DWARF with dSYM File");

				// bitCodeを再コンパイルするかどうか
				if (plist.root.values.TryGetValue("compileBitcode", out var enableBitcode))
				{
					var bitCodeValue = enableBitcode.AsBoolean() ? "YES" : "NO";
					
					project.SetBuildProperty(mainTargetGuid, "ENABLE_BITCODE", bitCodeValue);
					project.SetBuildProperty(frameworkTargetGuid, "ENABLE_BITCODE",bitCodeValue);
				}
				
				project.SetBuildProperty(frameworkTargetGuid, "CODE_SIGN_STYLE", "Automatic");
				
				// targetにlibzを追加
				if (plist.root.values.TryGetValue("libz.1.2.11.tbd", out var needLibz))
				{
					project.AddFrameworkToProject(mainTargetGuid, "libz.1.2.11.tbd", needLibz.AsBoolean());
					project.AddFrameworkToProject(frameworkTargetGuid, "libz.1.2.11.tbd", needLibz.AsBoolean());
				}
				
				// iOS14対応
				if (plist.root.values.TryGetValue("NSUserTrackingUsageDescription", out var attString))
				{
					if (!String.IsNullOrEmpty((attString.AsString())))
					{
						// 我们将其删除了。cocone到底拿AppTrackingTransparency作何用不清楚
						project.AddFrameworkToProject(frameworkTargetGuid, "AppTrackingTransparency.framework", true);
						project.AddFrameworkToProject(frameworkTargetGuid, "AdSupport.framework", true);
					}
				}
				
				project.SetBuildProperty(mainTargetGuid, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "NO");
				project.SetBuildProperty(frameworkTargetGuid, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "NO");

				// 我们自己加的。网上看到的所谓处理linker问题的办法
				//project.SetBuildProperty(frameworkTargetGuid, "EXCLUDED_ARCHS", "i386");
				//project.AddBuildProperty(frameworkTargetGuid, "EXCLUDED_ARCHS", "x86_64");
				
				// IOSカメラロールへのアクセス処理を使うためのフレームワーク追加
				//project.AddFrameworkToProject(frameworkTargetGuid, "Photos.framework", false);

				//project.AddFrameworkToProject(frameworkTargetGuid, "LinkPresentation.framework", false);

				// Firebase ユーザー通知フレームワークを追加
				//project.AddFrameworkToProject(frameworkTargetGuid, "UserNotifications.framework", true);
				//project.AddFrameworkToProject(mainTargetGuid, "UserNotifications.framework", true);
				
				// 署名設定をする
				SetSignByExportProfile(project);
				
				// NotificationTargetについて設定を行う
				//AddNotificationExtension(project, mainTargetGuid, path);

				project.WriteToFile(projectPath);

				// Firebase プッシュ通知を有効にする
				const string targetName = "Unity-iPhone";
				const string entitlementFileName = "mcombat.entitlements";
				var isDevelopment = Debug.isDebugBuild;
				var capabilities = new ProjectCapabilityManager(projectPath, targetName + "/" + entitlementFileName, targetName);
				//capabilities.AddPushNotifications(isDevelopment);
				//capabilities.AddBackgroundModes(BackgroundModesOptions.RemoteNotifications);
				
				capabilities.WriteToFile();
				
				plist.WriteToFile(plistPath);
			}
		}

		/**
		 * project: アプリのプロジェクト
		 * targetGuid:target（ビルドターゲット）
		 * path:基準となるパス
		 */
		private static void AddNotificationExtension(PBXProject project, string targetGuid, string path)
		{
			var buildType = GetBuildKind(playerBuildConfig.buildKind);
	        const string OriginPath = "Assets/App/Editor/Build/NotificationService";
	        const string ServiceName = "NotificationService";

	        var bundleId = $"{PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.iOS)}.notification";

	        var notificationServicePath = Path.Combine(path, ServiceName);
	        Directory.CreateDirectory(notificationServicePath);

	        File.Copy(Path.Combine(OriginPath, "Info.plist"), Path.Combine(notificationServicePath, "Info.plist"), true);
	        File.Copy(Path.Combine(OriginPath, "NotificationService.h"), Path.Combine(notificationServicePath, "NotificationService.h"), true);
	        File.Copy(Path.Combine(OriginPath, "NotificationService.m"), Path.Combine(notificationServicePath, "NotificationService.m"), true);

	        // Assets下に置いてあるものをXcodeProjectにコピー
	        var notificationServicePlist = new PlistDocument();
	        notificationServicePlist.ReadFromFile(Path.Combine(notificationServicePath, "Info.plist"));
	        notificationServicePlist.root.SetString("CFBundleShortVersionString", PlayerSettings.bundleVersion);
	        notificationServicePlist.root.SetString("CFBundleVersion", PlayerSettings.iOS.buildNumber.ToString());

	        // NotificationServiceExtensionを追加
	        var extensionGuid = project.AddAppExtension(targetGuid, ServiceName, bundleId, Path.Combine(notificationServicePath, "Info.plist"));
	        project.AddFileToBuild(extensionGuid, project.AddFile(Path.Combine(notificationServicePath, "NotificationService.h"), Path.Combine(ServiceName, "NotificationService.h")));
	        project.AddFileToBuild(extensionGuid, project.AddFile(Path.Combine(notificationServicePath, "NotificationService.m"), Path.Combine(ServiceName, "NotificationService.m")));

	        // export用のplistから、provisioning関連の設定を拝借
	        // mainTargetに設定するplistファイルから署名情報を拝借(pbxprojectからだとなぜか取れない）
	        // if文長いがtryGetしないのも問題なので長文
	        var plist = new PlistDocument();
	        if (!File.Exists(ExportPlistPath))
	        {
		        return;
	        }

	        plist.ReadFromFile(ExportPlistPath);
	        
	        if (plist.root.values.TryGetValue("Bundle identifier", out var bundleIdentifier))
	        {
		        if (plist.root.values.TryGetValue("provisioningProfiles", out var profiles))
		        {
			        var provisioningProfiles = profiles as PlistElementDict;
			        var notificationBundleIdentifier = $"{bundleIdentifier.AsString()}.notification";
			        if (provisioningProfiles != null && provisioningProfiles.values.TryGetValue(notificationBundleIdentifier, out var provisioningProfileSpecifier))
			        {
				        project.SetBuildProperty(extensionGuid, "PROVISIONING_PROFILE_SPECIFIER", provisioningProfileSpecifier.AsString());
			        }
		        }
	        }

	        if (plist.root.values.TryGetValue("teamID", out var teamId))
	        {
		        project.SetBuildProperty(extensionGuid, "DEVELOPMENT_TEAM", teamId.AsString());
	        }

	        if (plist.root.values.TryGetValue("signingCertificate", out var codeSignIdentity))
	        {
		        project.SetBuildProperty(extensionGuid, "CODE_SIGN_IDENTITY", codeSignIdentity.AsString());
	        }

	        project.SetBuildProperty(extensionGuid, "CLANG_ENABLE_MODULES", "YES");
	        project.SetBuildProperty(extensionGuid, "TARGETED_DEVICE_FAMILY", "1,2");
	        project.SetBuildProperty(extensionGuid, "ARCHS", "$(ARCHS_STANDARD)");
	        project.SetBuildProperty(extensionGuid, "ENABLE_BITCODE", "NO");
	        //TODO: Main Targetからもってくるべき？
	        project.SetBuildProperty(extensionGuid,"IPHONEOS_DEPLOYMENT_TARGET", "11.0");
	        notificationServicePlist.WriteToFile(Path.Combine(notificationServicePath, "Info.plist"));

		}
		
		private readonly static string CFBundleURLName = "pokepia";
		private readonly static List<string> urlschemes = new List<string>
		{
			"mcombat",
			"mcombatpromo",
		};

		private readonly static List<string> allowUrlScheme = new List<string>
		{
			// "sweetdays",
			// "ccnpokecolo",
			// "minime",
			// "sensil",
			// "niagho",
			// "mld",
			// "edenpoiyo",
			// "ccnstory",
			// "roomage",
			// "ccnpocketcolony",
			// "livlyisland",
			// "purenista",
			// "poketwin"
		};

		
		static void AddURLSchemes(PlistDocument plist)
		{
			var buildType = GetBuildKind(playerBuildConfig.buildKind);
			PlistElementArray urlTypes = plist.root["CFBundleURLTypes"] as PlistElementArray;
			if (urlTypes == null)
			{
				urlTypes = plist.root.CreateArray("CFBundleURLTypes");
			}
			PlistElementDict urlschemeDict = urlTypes.AddDict();
			urlschemeDict.SetString("CFBundleURLName", CFBundleURLName);

			PlistElementArray schemes = urlschemeDict.CreateArray("CFBundleURLSchemes");

			foreach (string urlscheme in urlschemes)
			{
				schemes.AddString(urlscheme);
			}

			if (buildType != PlayerBuildConfig.BuildKind.Release)
			{
				schemes.AddString($"{CFBundleURLName}.{buildType}");
			}
		}

		static void AddAllowURLSchemes(PlistDocument plist)
		{
			PlistElementArray urlschemes = plist.root["LSApplicationQueriesSchemes"] as PlistElementArray;
			if (urlschemes == null)
			{
				urlschemes = plist.root.CreateArray("LSApplicationQueriesSchemes");
			}

			foreach (var scheme in allowUrlScheme)
			{
				urlschemes.AddString(scheme);
			}
		}

#endif
	}
}
