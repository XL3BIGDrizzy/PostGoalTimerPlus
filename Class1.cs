using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace PostGoalTimerPlus
{
	[BepInPlugin("superbattlegolf.postgoaltimerplus", "PostGoalTimerPlus", "1.1.0")]
	public class PostGoalTimerPlusPlugin : BaseUnityPlugin
	{
		private const int DisabledCountdownSeconds = 0;

		private enum CountdownOption
		{
			Disabled = 0,
			OneMinute = 60,
			TwoMinutes = 120,
			ThreeMinutes = 180,
			FourMinutes = 240,
			FiveMinutes = 300
		}

		private Harmony _harmony;
		private static ConfigEntry<CountdownOption> _countdownOption;

		private void Awake()
		{
			_countdownOption = Config.Bind(
				"General",
				"CountdownOption",
				CountdownOption.Disabled,
				"Post-score countdown behavior. Options: Disabled, OneMinute, TwoMinutes, ThreeMinutes, FourMinutes, FiveMinutes.");

			_harmony = new Harmony("superbattlegolf.postgoaltimerplus");
			TryApplyPatch();
		}

		private void TryApplyPatch()
		{
			var patchTargets = new List<(string TypeName, string MethodName, string PatchMethodName, string Description)>
			{
				("CourseManager", "BeginCountdownToMatchEnd", nameof(BeginCountdownPrefix), "countdown start gate"),
				("CourseManager", "InformPlayerScoredInternal", nameof(InformPlayerScoredPostfix), "countdown duration override")
			};

			foreach (var patchTarget in patchTargets)
			{
				var targetMethod = FindTargetMethod(patchTarget.TypeName, patchTarget.MethodName);
				if (targetMethod == null)
				{
					continue;
				}

				ApplyPatch(targetMethod, patchTarget.PatchMethodName, patchTarget.Description);
			}
		}

		private void ApplyPatch(MethodInfo targetMethod, string patchMethodName, string description)
		{
			if (targetMethod == null)
			{
				return;
			}

			var patchMethod = AccessTools.Method(typeof(PostGoalTimerPlusPlugin), patchMethodName);
			if (patchMethod == null)
			{
				Logger.LogError($"Patch method '{patchMethodName}' was not found in PostGoalTimerPlusPlugin.");
				return;
			}

			if (patchMethod.ReturnType == typeof(bool))
			{
				_harmony.Patch(targetMethod, prefix: new HarmonyMethod(patchMethod));
			}
			else
			{
				_harmony.Patch(targetMethod, postfix: new HarmonyMethod(patchMethod));
			}

			Logger.LogInfo($"Patched {targetMethod.DeclaringType?.FullName}.{targetMethod.Name} for {description}.");
		}

		private static MethodInfo FindTargetMethod(string typeName, string methodName)
		{
			var type = AccessTools.TypeByName(typeName);
			if (type != null)
			{
				return AccessTools.DeclaredMethod(type, methodName);
			}

			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				Type[] types;
				try
				{
					types = assembly.GetTypes();
				}
				catch
				{
					continue;
				}

				foreach (var candidate in types)
				{
					if (!string.Equals(candidate.Name, typeName, StringComparison.Ordinal))
					{
						continue;
					}

					var method = AccessTools.DeclaredMethod(candidate, methodName);
					if (method != null)
					{
						return method;
					}
				}
			}

			return null;
		}

		private static bool BeginCountdownPrefix()
		{
			return GetConfiguredCountdownSeconds() > DisabledCountdownSeconds;
		}

		private static void InformPlayerScoredPostfix(object __instance)
		{
			var countdownSeconds = GetConfiguredCountdownSeconds();
			if (countdownSeconds <= DisabledCountdownSeconds || __instance == null)
			{
				return;
			}

			var instanceType = __instance.GetType();
			var matchStateField = AccessTools.Field(instanceType, "matchState");
			var countdownField = AccessTools.Field(instanceType, "countdownRemainingTime");
			if (matchStateField == null || countdownField == null)
			{
				return;
			}

			var matchState = matchStateField.GetValue(__instance);
			if (!string.Equals(matchState?.ToString(), "CountingDownToEnd", StringComparison.Ordinal))
			{
				return;
			}

			countdownField.SetValue(__instance, (float)countdownSeconds);
		}

		private static int GetConfiguredCountdownSeconds()
		{
			if (_countdownOption == null)
			{
				return DisabledCountdownSeconds;
			}

			return (int)_countdownOption.Value;
		}

	}
}
