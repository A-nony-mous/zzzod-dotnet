using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using YamlDotNet.Serialization;
using ZzzOd.AppHost.Notifications;
using ZzzOd.AppHost.Overlay;
using ZzzOd.GameLogic.Application.ChargePlan;
using ZzzOd.GameLogic.Application.Coffee;
using ZzzOd.GameLogic.Application.CommissionAssistant;
using ZzzOd.GameLogic.Application.DailySignIn;
using ZzzOd.GameLogic.Application.Devtools.OperationDebug;
using ZzzOd.GameLogic.Application.Devtools.ScreenshotHelper;
using ZzzOd.GameLogic.Application.DriveDiscDismantle;
using ZzzOd.GameLogic.Application.HollowZero.LostVoid;
using ZzzOd.GameLogic.Application.HollowZero.WitheredDomain;
using ZzzOd.GameLogic.Application.IntelBoard;
using ZzzOd.GameLogic.Application.LifeOnLine;
using ZzzOd.GameLogic.Application.NotoriousHunt;
using ZzzOd.GameLogic.Application.RandomPlay;
using ZzzOd.GameLogic.Application.RedemptionCode;
using ZzzOd.GameLogic.Application.ShiyuDefense;
using ZzzOd.GameLogic.Application.SuibianTemple;
using ZzzOd.GameLogic.Application.WorldPatrol;
using ZzzOd.GameLogic.Config;

namespace ZzzOd.AppHost.Backend;

internal sealed class ZzzConfigScopeService
{
	private interface IConfigScopeDefinition
	{
		string? AppId { get; }

		ZzzConfigScopeDescriptorDto Descriptor { get; }

		ZzzConfigScopeValuesDto Read(OneDragonEnvironment environment, int? instanceIndex, string groupId);

		ZzzConfigScopeValuesDto Save(OneDragonEnvironment environment, int? instanceIndex, string groupId, IReadOnlyDictionary<string, object?> values);
	}

	private sealed class EmptyConfigScopeDefinition : IConfigScopeDefinition
	{
		public string? AppId => null;

		public ZzzConfigScopeDescriptorDto Descriptor { get; }

		public EmptyConfigScopeDefinition(ZzzConfigScopeDescriptorDto descriptor)
		{
			Descriptor = descriptor;
		}

		public ZzzConfigScopeValuesDto Read(OneDragonEnvironment environment, int? instanceIndex, string groupId)
		{
			return new ZzzConfigScopeValuesDto(Descriptor, null, null, new Dictionary<string, object>());
		}

		public ZzzConfigScopeValuesDto Save(OneDragonEnvironment environment, int? instanceIndex, string groupId, IReadOnlyDictionary<string, object?> values)
		{
			throw new ZzzConfigValidationException(Descriptor.Scope, null, "配置 scope 当前不可写。");
		}
	}

	private sealed class OverlayConfigScope : IConfigScopeDefinition
	{
		private static readonly IDeserializer Deserializer = new DeserializerBuilder().Build();

		private static readonly ISerializer Serializer = new SerializerBuilder().ConfigureDefaultValuesHandling(DefaultValuesHandling.Preserve).Build();

		private static readonly string[] PanelNames = new string[6] { "log_panel", "state_panel", "battle_panel", "decision_panel", "timeline_panel", "performance_panel" };

		private static readonly string[] PanelPhysicalGeometryKeys = new string[4] { "x", "y", "w", "h" };

		private static readonly string[] PanelLockedGeometryKeys = new string[4] { "locked_x", "locked_y", "locked_w", "locked_h" };

		private static readonly string[] PanelFreeGeometryKeys = new string[4] { "free_x", "free_y", "free_w", "free_h" };

		private static readonly string[] PanelFreeWorkAreaKeys = new string[4] { "free_work_area_x", "free_work_area_y", "free_work_area_w", "free_work_area_h" };

		private static readonly IReadOnlyDictionary<string, bool> DefaultPerformanceMetrics = new Dictionary<string, bool>(StringComparer.Ordinal)
		{
			["ocr_ms"] = true,
			["yolo_ms"] = true,
			["cv_pipeline_ms"] = true,
			["operation_round_ms"] = true,
			["overlay_refresh_ms"] = true
		};

		private static readonly IReadOnlyDictionary<string, object?> ScalarDefaults = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["enabled"] = false,
			["visible"] = true,
			["anti_capture"] = true,
			["toggle_hotkey"] = "o",
			["vision_layer_enabled"] = true,
			["vision_yolo_enabled"] = true,
			["vision_yolo_dedup_iou_threshold"] = ZzzOverlayDisplayOptionsDto.DefaultYoloDedupIouThreshold,
			["vision_ocr_enabled"] = true,
			["vision_template_enabled"] = true,
			["vision_cv_enabled"] = true,
			["vision_offset_x"] = 0,
			["vision_offset_y"] = 0,
			["vision_scale_x"] = 1.0,
			["vision_scale_y"] = 1.0,
			["patched_capture_enabled"] = false,
			["patched_capture_suffix"] = "_patched",
			["font_family"] = "Segoe UI",
			["font_size"] = 12,
			["panel_opacity"] = 70,
			["panel_text_color"] = "#f2f2f2",
			["log_panel_enabled"] = true,
			["state_panel_enabled"] = true,
			["battle_panel_enabled"] = true,
			["battle_state_filter"] = "",
			["decision_panel_enabled"] = true,
			["timeline_panel_enabled"] = true,
			["performance_panel_enabled"] = true,
			["panel_edit_mode"] = false,
			["panel_lock_to_game_window"] = true,
			["log_max_lines"] = 120,
			["log_fade_seconds"] = 12,
			["follow_interval_ms"] = 120,
			["input_poll_interval_ms"] = 50,
			["state_poll_interval_ms"] = 200
		};

		public string? AppId => null;

		public ZzzConfigScopeDescriptorDto Descriptor { get; }

		public OverlayConfigScope()
		{
			List<ZzzConfigSettingDescriptorDto> list = new List<ZzzConfigSettingDescriptorDto>();
			list.AddRange(ScalarDefaults.Select<KeyValuePair<string, object>, ZzzConfigSettingDescriptorDto>((KeyValuePair<string, object> pair) => new ZzzConfigSettingDescriptorDto(pair.Key, pair.Key, ValueType(pair.Value), Writable: true, pair.Value)));
			list.Add(new ZzzConfigSettingDescriptorDto("performance_metric_enabled_map", "performance_metric_enabled_map", ZzzConfigValueType.Complex, Writable: true, DefaultPerformanceMetrics));
			list.Add(new ZzzConfigSettingDescriptorDto("panel_free_mode_map", "panel_free_mode_map", ZzzConfigValueType.Complex, Writable: true, DefaultPanelFreeModes()));
			list.Add(new ZzzConfigSettingDescriptorDto("panel_geometry", "panel_geometry", ZzzConfigValueType.Complex, Writable: true, DefaultPanelGeometry()));
			list.Add(new ZzzConfigSettingDescriptorDto("panel_appearance", "panel_appearance", ZzzConfigValueType.Complex, Writable: true, DefaultPanelAppearance()));
			Descriptor = new ZzzConfigScopeDescriptorDto("overlay", "Overlay", InstanceBound: false, GroupBound: false, Writable: true, list);
		}

		public ZzzConfigScopeValuesDto Read(OneDragonEnvironment environment, int? instanceIndex, string groupId)
		{
			Dictionary<string, object> dictionary = ReadRaw(ResolvePath(environment));
			object value;
			Dictionary<string, object> dictionary2 = (dictionary.TryGetValue("overlay", out value) ? ConvertMap(value) : new Dictionary<string, object>(StringComparer.Ordinal));
			Dictionary<string, object> dictionary3 = new Dictionary<string, object>(StringComparer.Ordinal);
			foreach (KeyValuePair<string, object> scalarDefault in ScalarDefaults)
			{
				scalarDefault.Deconstruct(out var key, out var value2);
				string key2 = key;
				object obj = value2;
				dictionary3[key2] = NormalizeScalar(key2, dictionary2.TryGetValue(key2, out var value3) ? value3 : obj);
			}
			dictionary3["performance_metric_enabled_map"] = NormalizePerformanceMetrics(dictionary2.TryGetValue("performance_metric_enabled_map", out var value4) ? value4 : null);
			Dictionary<string, bool> panelFreeModes = NormalizePanelFreeModes(dictionary2.TryGetValue("panel_free_mode_map", out var value5) ? value5 : null, Convert.ToBoolean(dictionary3["panel_lock_to_game_window"], CultureInfo.InvariantCulture));
			dictionary3["panel_free_mode_map"] = panelFreeModes;
			dictionary3["panel_lock_to_game_window"] = AreAllPanelsLocked(panelFreeModes);
			dictionary3["panel_geometry"] = NormalizePanelGeometry(dictionary2.TryGetValue("panel_geometry", out var value6) ? value6 : null);
			dictionary3["panel_appearance"] = NormalizePanelAppearance(dictionary2.TryGetValue("panel_appearance", out var value7) ? value7 : null);
			return new ZzzConfigScopeValuesDto(Descriptor, null, null, dictionary3);
		}

		public ZzzConfigScopeValuesDto Save(OneDragonEnvironment environment, int? instanceIndex, string groupId, IReadOnlyDictionary<string, object?> values)
		{
			string path = ResolvePath(environment);
			Dictionary<string, object> dictionary = ReadRaw(path);
			object value;
			Dictionary<string, object> dictionary2 = (dictionary.TryGetValue("overlay", out value) ? ConvertMap(value) : new Dictionary<string, object>(StringComparer.Ordinal));
			foreach (KeyValuePair<string, object> item in values)
			{
				item.Deconstruct(out var key, out var value2);
				if (ScalarDefaults.ContainsKey(key))
				{
					dictionary2[key] = NormalizeScalar(key, value2);
				}
				else if (key is not "performance_metric_enabled_map" and not "panel_free_mode_map" and not "panel_geometry" and not "panel_appearance")
				{
					throw new ZzzConfigValidationException(Descriptor.Scope, key, "未知配置 key。");
				}
			}

			bool globalPanelLockChanged = values.ContainsKey("panel_lock_to_game_window");
			bool lockedToGameWindow = dictionary2.TryGetValue("panel_lock_to_game_window", out object? lockValue)
				? Convert.ToBoolean(lockValue, CultureInfo.InvariantCulture)
				: Convert.ToBoolean(ScalarDefaults["panel_lock_to_game_window"], CultureInfo.InvariantCulture);
			foreach (KeyValuePair<string, object> item2 in values)
			{
				item2.Deconstruct(out var key2, out var value3);
				if (ScalarDefaults.ContainsKey(key2))
				{
					continue;
				}

				switch (key2)
				{
					case "performance_metric_enabled_map":
						dictionary2[key2] = MergePerformanceMetrics(dictionary2.TryGetValue(key2, out object? existingMetrics) ? existingMetrics : null, value3);
						break;
					case "panel_free_mode_map":
						if (!globalPanelLockChanged)
						{
							dictionary2[key2] = MergePanelFreeModes(dictionary2.TryGetValue(key2, out object? existingModes) ? existingModes : null, value3, lockedToGameWindow);
						}
						break;
					case "panel_geometry":
						dictionary2[key2] = MergePanelGeometry(dictionary2.TryGetValue(key2, out object? existingGeometry) ? existingGeometry : null, value3);
						break;
					case "panel_appearance":
						dictionary2[key2] = NormalizePanelAppearance(value3);
						break;
				}
			}
			if (globalPanelLockChanged)
			{
				dictionary2["panel_free_mode_map"] = CreatePanelFreeModes(!lockedToGameWindow);
			}
			else if (values.ContainsKey("panel_free_mode_map"))
			{
				Dictionary<string, bool> panelFreeModes = NormalizePanelFreeModes(
					dictionary2.TryGetValue("panel_free_mode_map", out object? storedModes) ? storedModes : null,
					lockedToGameWindow);
				dictionary2["panel_lock_to_game_window"] = AreAllPanelsLocked(panelFreeModes);
			}
			dictionary["overlay"] = dictionary2;
			WriteRaw(path, dictionary);
			return Read(environment, null, groupId);
		}

		private static object NormalizeScalar(string key, object? value)
		{
			try
			{
				if (1 == 0)
				{
				}
				object result;
				switch (key)
				{
				case "enabled":
				case "visible":
				case "anti_capture":
				case "vision_layer_enabled":
				case "vision_yolo_enabled":
				case "vision_ocr_enabled":
				case "vision_template_enabled":
				case "vision_cv_enabled":
				case "patched_capture_enabled":
				case "log_panel_enabled":
				case "state_panel_enabled":
				case "battle_panel_enabled":
				case "decision_panel_enabled":
				case "timeline_panel_enabled":
				case "performance_panel_enabled":
				case "panel_edit_mode":
				case "panel_lock_to_game_window":
					result = Convert.ToBoolean(value, CultureInfo.InvariantCulture);
					break;
				case "toggle_hotkey":
					result = NormalizeHotkey(Convert.ToString(value, CultureInfo.InvariantCulture));
					break;
				case "patched_capture_suffix":
					result = NormalizeSuffix(Convert.ToString(value, CultureInfo.InvariantCulture));
					break;
				case "panel_text_color":
					result = NormalizeColor(Convert.ToString(value, CultureInfo.InvariantCulture));
					break;
				case "font_family":
					result = NormalizeFontFamily(Convert.ToString(value, CultureInfo.InvariantCulture));
					break;
				case "battle_state_filter":
					result = (Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty).Trim();
					break;
				case "vision_offset_x":
				case "vision_offset_y":
					result = Convert.ToInt32(value, CultureInfo.InvariantCulture);
					break;
				case "vision_scale_x":
				case "vision_scale_y":
					result = Math.Clamp(Convert.ToDouble(value, CultureInfo.InvariantCulture), 0.5, 1.5);
					break;
				case "vision_yolo_dedup_iou_threshold":
					double yoloDedupIouThreshold = Convert.ToDouble(value, CultureInfo.InvariantCulture);
					result = double.IsFinite(yoloDedupIouThreshold)
						? Math.Clamp(yoloDedupIouThreshold, 0.01d, 1d)
						: ZzzOverlayDisplayOptionsDto.DefaultYoloDedupIouThreshold;
					break;
				case "font_size":
					result = Math.Clamp(Convert.ToInt32(value, CultureInfo.InvariantCulture), 10, 28);
					break;
				case "panel_opacity":
					result = Math.Clamp(Convert.ToInt32(value, CultureInfo.InvariantCulture), 5, 100);
					break;
				case "log_max_lines":
					result = Math.Max(20, Convert.ToInt32(value, CultureInfo.InvariantCulture));
					break;
				case "log_fade_seconds":
					result = Math.Max(3, Convert.ToInt32(value, CultureInfo.InvariantCulture));
					break;
				case "follow_interval_ms":
					result = Math.Max(30, Convert.ToInt32(value, CultureInfo.InvariantCulture));
					break;
				case "input_poll_interval_ms":
					result = Math.Max(20, Convert.ToInt32(value, CultureInfo.InvariantCulture));
					break;
				case "state_poll_interval_ms":
					result = Math.Max(80, Convert.ToInt32(value, CultureInfo.InvariantCulture));
					break;
				default:
					throw new ZzzConfigValidationException("overlay", key, "未知配置 key。");
				}
				if (1 == 0)
				{
				}
				return result;
			}
			catch (Exception ex) when (((ex is FormatException || ex is InvalidCastException || ex is OverflowException) ? 1 : 0) != 0)
			{
				throw new ZzzConfigValidationException("overlay", key, "配置值无效：" + ex.Message);
			}
		}

		private static string NormalizeHotkey(string? value)
		{
			string text = (value ?? string.Empty).Trim().ToLowerInvariant();
			if (text.Length == 0)
			{
				return "o";
			}
			int result = default(int);
			bool flag = text.StartsWith("vk_", StringComparison.Ordinal) && int.TryParse(text.AsSpan(3), out result);
			bool flag2 = flag;
			if (flag2)
			{
				bool flag3;
				switch (result)
				{
				case 48:
				case 49:
				case 50:
				case 51:
				case 52:
				case 53:
				case 54:
				case 55:
				case 56:
				case 57:
				case 65:
				case 66:
				case 67:
				case 68:
				case 69:
				case 70:
				case 71:
				case 72:
				case 73:
				case 74:
				case 75:
				case 76:
				case 77:
				case 78:
				case 79:
				case 80:
				case 81:
				case 82:
				case 83:
				case 84:
				case 85:
				case 86:
				case 87:
				case 88:
				case 89:
				case 90:
					flag3 = true;
					break;
				default:
					flag3 = false;
					break;
				}
				flag2 = flag3;
			}
			if (flag2)
			{
				return char.ToLowerInvariant((char)result).ToString();
			}
			return text;
		}

		private static string NormalizeColor(string? value)
		{
			string text = (value ?? string.Empty).Trim();
			if (text.Length == 0)
			{
				return "#f2f2f2";
			}
			if (!text.StartsWith('#'))
			{
				text = "#" + text;
			}
			text = text.ToLowerInvariant();
			return Regex.IsMatch(text, "^#[0-9a-f]{6}$", RegexOptions.CultureInvariant) ? text : "#f2f2f2";
		}

		private static string NormalizeFontFamily(string? value)
		{
			string text = (value ?? string.Empty).Trim();
			if (text.Length == 0)
			{
				return "Segoe UI";
			}
			return text.Substring(0, Math.Min(64, text.Length));
		}

		private static string NormalizeSuffix(string? value)
		{
			string text = (value ?? string.Empty).Trim();
			if (text.Length == 0)
			{
				text = "_patched";
			}
			if (!text.StartsWith('_'))
			{
				text = "_" + text;
			}
			return text.Substring(0, Math.Min(40, text.Length));
		}

		private static Dictionary<string, bool> NormalizePerformanceMetrics(object? value)
		{
			Dictionary<string, bool> dictionary = DefaultPerformanceMetrics.ToDictionary<KeyValuePair<string, bool>, string, bool>((KeyValuePair<string, bool> pair) => pair.Key, (KeyValuePair<string, bool> pair) => pair.Value, StringComparer.Ordinal);
			foreach (KeyValuePair<string, object> item in ConvertMap(value))
			{
				item.Deconstruct(out var key, out var value2);
				string key2 = key;
				object value3 = value2;
				dictionary[key2] = Convert.ToBoolean(value3, CultureInfo.InvariantCulture);
			}
			return dictionary;
		}

		private static Dictionary<string, bool> MergePerformanceMetrics(object? existing, object? update)
		{
			Dictionary<string, bool> dictionary = NormalizePerformanceMetrics(existing);
			foreach (KeyValuePair<string, object> item in ConvertMap(update))
			{
				item.Deconstruct(out var key, out var value);
				string key2 = key;
				object value2 = value;
				dictionary[key2] = Convert.ToBoolean(value2, CultureInfo.InvariantCulture);
			}
			return dictionary;
		}

		private static Dictionary<string, bool> NormalizePanelFreeModes(object? value, bool lockedToGameWindow)
		{
			Dictionary<string, object?> source = ConvertMap(value);
			return PanelNames.ToDictionary<string, string, bool>((string panel) => panel, (string panel) => source.TryGetValue(panel, out object value2) ? Convert.ToBoolean(value2, CultureInfo.InvariantCulture) : (!lockedToGameWindow), StringComparer.Ordinal);
		}

		private static Dictionary<string, bool> CreatePanelFreeModes(bool isFreeMode)
		{
			return PanelNames.ToDictionary<string, string, bool>((string panel) => panel, (string _) => isFreeMode, StringComparer.Ordinal);
		}

		private static bool AreAllPanelsLocked(IReadOnlyDictionary<string, bool> panelFreeModes)
		{
			return PanelNames.All((string panel) => !panelFreeModes.TryGetValue(panel, out bool isFreeMode) || !isFreeMode);
		}

		private static Dictionary<string, bool> MergePanelFreeModes(object? existing, object? update, bool lockedToGameWindow)
		{
			Dictionary<string, bool> merged = NormalizePanelFreeModes(existing, lockedToGameWindow);
			Dictionary<string, object?> source = ConvertMap(update);
			foreach (string panel in PanelNames)
			{
				if (source.TryGetValue(panel, out object? value))
				{
					merged[panel] = Convert.ToBoolean(value, CultureInfo.InvariantCulture);
				}
			}

			return merged;
		}

		private static Dictionary<string, object?> NormalizePanelGeometry(object? value)
		{
			Dictionary<string, object> dictionary = ConvertMap(value);
			Dictionary<string, object> dictionary2 = DefaultPanelGeometry();
			string[] panelNames = PanelNames;
			foreach (string key in panelNames)
			{
				Dictionary<string, object> dictionary3 = ConvertMap(dictionary2[key]);
				object value2;
				Dictionary<string, object> dictionary4 = (dictionary.TryGetValue(key, out value2) ? ConvertMap(value2) : new Dictionary<string, object>());
				foreach (string key2 in PanelPhysicalGeometryKeys)
				{
					if (dictionary4.TryGetValue(key2, out var value3))
					{
						dictionary3[key2] = NormalizePhysicalCoordinate(key2, value3, Convert.ToDouble(dictionary3[key2], CultureInfo.InvariantCulture));
					}
				}

				bool hasAdvancedLayoutField = false;
				foreach (string key3 in PanelLockedGeometryKeys)
				{
					if (dictionary4.TryGetValue(key3, out object? value4))
					{
						dictionary3[key3] = NormalizeFiniteDouble(value4, Convert.ToDouble(dictionary3[key3], CultureInfo.InvariantCulture));
						hasAdvancedLayoutField = true;
					}
				}

				foreach (string key4 in PanelFreeGeometryKeys)
				{
					if (dictionary4.TryGetValue(key4, out object? value5))
					{
						dictionary3[key4] = NormalizePhysicalCoordinate(key4, value5, Convert.ToDouble(dictionary3[key4], CultureInfo.InvariantCulture));
						hasAdvancedLayoutField = true;
					}
				}

				if (dictionary4.TryGetValue("free_dpi", out object? value6))
				{
					dictionary3["free_dpi"] = NormalizeDpi(value6);
					hasAdvancedLayoutField = true;
				}

				if (dictionary4.TryGetValue("free_display_name", out object? displayName))
				{
					string? normalizedDisplayName = Convert.ToString(displayName, CultureInfo.InvariantCulture)?.Trim();
					if (string.IsNullOrWhiteSpace(normalizedDisplayName))
					{
						dictionary3.Remove("free_display_name");
					}
					else
					{
						dictionary3["free_display_name"] = normalizedDisplayName;
						hasAdvancedLayoutField = true;
					}
				}


				foreach (string key5 in PanelFreeWorkAreaKeys)
				{
					if (dictionary4.TryGetValue(key5, out object? workAreaValue) &&
						TryNormalizeFreeWorkAreaCoordinate(key5, workAreaValue, out double normalizedWorkAreaCoordinate))
					{
						dictionary3[key5] = normalizedWorkAreaCoordinate;
						hasAdvancedLayoutField = true;
					}
					else if (dictionary4.ContainsKey(key5))
					{
						dictionary3.Remove(key5);
					}
				}

				NormalizeFreeWorkAreaAnchor(dictionary3);

				if (dictionary4.TryGetValue("layout_version", out object? value7))
				{
					dictionary3["layout_version"] = NormalizeLayoutVersion(value7);
				}
				else if (hasAdvancedLayoutField)
				{
					dictionary3["layout_version"] = HasCompleteFreeWorkAreaAnchor(dictionary3) ? 3 : 2;
				}

				if (dictionary4.TryGetValue("pending_source_free_mode", out object? pendingSourceMode) && pendingSourceMode is not null)
				{
					dictionary3["pending_source_free_mode"] = Convert.ToBoolean(pendingSourceMode, CultureInfo.InvariantCulture);
				}
				else
				{
					dictionary3.Remove("pending_source_free_mode");
				}

				if (!dictionary4.ContainsKey("free_x"))
				{
					dictionary3["free_x"] = dictionary3["x"];
				}
				if (!dictionary4.ContainsKey("free_y"))
				{
					dictionary3["free_y"] = dictionary3["y"];
				}
				if (!dictionary4.ContainsKey("free_w"))
				{
					dictionary3["free_w"] = dictionary3["w"];
				}
				if (!dictionary4.ContainsKey("free_h"))
				{
					dictionary3["free_h"] = dictionary3["h"];
				}
				dictionary2[key] = dictionary3;
			}
			return dictionary2;
		}

		private static Dictionary<string, object?> MergePanelGeometry(object? existing, object? update)
		{
			Dictionary<string, object?> merged = NormalizePanelGeometry(existing);
			Dictionary<string, object?> source = ConvertMap(update);
			foreach (string panel in PanelNames)
			{
				if (!source.TryGetValue(panel, out object? value))
				{
					continue;
				}

				Dictionary<string, object?> target = ConvertMap(merged[panel]);
				foreach (KeyValuePair<string, object> item in ConvertMap(value))
				{
					if (item.Key is "x" or "y" or "w" or "h" or "layout_version" or "locked_x" or "locked_y" or "locked_w" or "locked_h" or "free_x" or "free_y" or "free_w" or "free_h" or "free_dpi" or "free_display_name" or "free_work_area_x" or "free_work_area_y" or "free_work_area_w" or "free_work_area_h" or "pending_source_free_mode")
					{
						target[item.Key] = item.Value;
					}
				}

				merged[panel] = target;
			}

			return NormalizePanelGeometry(merged);
		}

		private static double NormalizePhysicalCoordinate(string key, object? value, double fallback)
		{
			double result = NormalizeFiniteDouble(value, fallback);
			return key is "w" or "h" or "free_w" or "free_h" or "free_work_area_w" or "free_work_area_h"
				? Math.Max(1d, result)
				: result;
		}

		private static double NormalizeFiniteDouble(object? value, double fallback)
		{
			double result = Convert.ToDouble(value, CultureInfo.InvariantCulture);
			return double.IsFinite(result) ? result : fallback;
		}

		private static bool TryNormalizeFreeWorkAreaCoordinate(string key, object? value, out double normalized)
		{
			try
			{
				normalized = Convert.ToDouble(value, CultureInfo.InvariantCulture);
				return double.IsFinite(normalized) &&
					(key is not "free_work_area_w" and not "free_work_area_h" || normalized > 0d);
			}
			catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
			{
				normalized = 0d;
				return false;
			}
		}

		private static void NormalizeFreeWorkAreaAnchor(Dictionary<string, object> geometry)
		{
			if (HasCompleteFreeWorkAreaAnchor(geometry))
			{
				return;
			}

			foreach (string key in PanelFreeWorkAreaKeys)
			{
				geometry.Remove(key);
			}
		}

		private static bool HasCompleteFreeWorkAreaAnchor(IReadOnlyDictionary<string, object> geometry)
		{
			return TryReadFiniteCoordinate(geometry, "free_work_area_x", out _) &&
				TryReadFiniteCoordinate(geometry, "free_work_area_y", out _) &&
				TryReadFiniteCoordinate(geometry, "free_work_area_w", out double width) && width > 0d &&
				TryReadFiniteCoordinate(geometry, "free_work_area_h", out double height) && height > 0d;
		}

		private static bool TryReadFiniteCoordinate(IReadOnlyDictionary<string, object> geometry, string key, out double coordinate)
		{
			if (!geometry.TryGetValue(key, out object? raw))
			{
				coordinate = 0d;
				return false;
			}

			return TryNormalizeFreeWorkAreaCoordinate(key, raw, out coordinate);
		}

		private static int NormalizeLayoutVersion(object? value) => Math.Max(1, Convert.ToInt32(value, CultureInfo.InvariantCulture));

		private static uint NormalizeDpi(object? value)
		{
			uint result = Convert.ToUInt32(value, CultureInfo.InvariantCulture);
			return result == 0 ? 96u : result;
		}

		private static Dictionary<string, object?> NormalizePanelAppearance(object? value)
		{
			Dictionary<string, object> dictionary = ConvertMap(value);
			Dictionary<string, object> dictionary2 = DefaultPanelAppearance();
			string[] panelNames = PanelNames;
			foreach (string key in panelNames)
			{
				Dictionary<string, object> dictionary3 = ConvertMap(dictionary2[key]);
				object value2;
				Dictionary<string, object> dictionary4 = (dictionary.TryGetValue(key, out value2) ? ConvertMap(value2) : new Dictionary<string, object>());
				if (dictionary4.TryGetValue("font_size", out var value3))
				{
					dictionary3["font_size"] = Math.Clamp(Convert.ToInt32(value3, CultureInfo.InvariantCulture), 10, 28);
				}
				if (dictionary4.TryGetValue("opacity", out var value4))
				{
					dictionary3["opacity"] = Math.Clamp(Convert.ToInt32(value4, CultureInfo.InvariantCulture), 5, 100);
				}
				dictionary2[key] = dictionary3;
			}
			return dictionary2;
		}

		private static Dictionary<string, object?> DefaultPanelGeometry()
		{
			return new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["log_panel"] = CreatePanelGeometry(100d, 100d, 480d, 200d),
				["state_panel"] = CreatePanelGeometry(0d, 0d, 300d, 120d),
				["battle_panel"] = CreatePanelGeometry(0d, 0d, 320d, 220d),
				["decision_panel"] = CreatePanelGeometry(0d, 0d, 300d, 140d),
				["timeline_panel"] = CreatePanelGeometry(0d, 0d, 300d, 170d),
				["performance_panel"] = CreatePanelGeometry(0d, 0d, 300d, 110d)
			};
		}

		private static Dictionary<string, object> CreatePanelGeometry(double x, double y, double width, double height)
		{
			return new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["x"] = x,
				["y"] = y,
				["w"] = width,
				["h"] = height,
				["layout_version"] = 1,
				["locked_x"] = 0d,
				["locked_y"] = 0d,
				["locked_w"] = 0d,
				["locked_h"] = 0d,
				["free_x"] = x,
				["free_y"] = y,
				["free_w"] = width,
				["free_h"] = height,
				["free_dpi"] = 96u
			};
		}

		private static Dictionary<string, object?> DefaultPanelAppearance()
		{
			return PanelNames.ToDictionary<string, string, object>((string panel) => panel, (string _) => new Dictionary<string, object>
			{
				["font_size"] = 12,
				["opacity"] = 70
			}, StringComparer.Ordinal);
		}

		private static Dictionary<string, bool> DefaultPanelFreeModes()
		{
			return PanelNames.ToDictionary<string, string, bool>((string panel) => panel, (string _) => false, StringComparer.Ordinal);
		}

		private static ZzzConfigValueType ValueType(object? value)
		{
			if (1 == 0)
			{
			}
			ZzzConfigValueType result = ((value is bool) ? ZzzConfigValueType.Boolean : ((value is int) ? ZzzConfigValueType.Integer : ((value is double) ? ZzzConfigValueType.Number : ((!(value is string)) ? ZzzConfigValueType.Complex : ZzzConfigValueType.String))));
			if (1 == 0)
			{
			}
			return result;
		}

		private static string ResolvePath(OneDragonEnvironment environment)
		{
			return environment.GetPathUnderWorkDir("config", "overlay.yml");
		}

		private static Dictionary<string, object?> ReadRaw(string path)
		{
			if (!File.Exists(path) || string.IsNullOrWhiteSpace(File.ReadAllText(path)))
			{
				return new Dictionary<string, object>(StringComparer.Ordinal);
			}
			object value = Deserializer.Deserialize<object>(File.ReadAllText(path));
			return ConvertMap(value);
		}

		private static void WriteRaw(string path, IReadOnlyDictionary<string, object?> raw)
		{
			Directory.CreateDirectory(Path.GetDirectoryName(path));
			File.WriteAllText(path, Serializer.Serialize(raw));
			YamlOperator.InvalidateCache(path);
		}

		private static Dictionary<string, object?> ConvertMap(object? value)
		{
			if (value is IReadOnlyDictionary<string, object> source)
			{
				return source.ToDictionary<KeyValuePair<string, object>, string, object>((KeyValuePair<string, object> pair) => pair.Key, (KeyValuePair<string, object> pair) => NormalizeYaml(pair.Value), StringComparer.Ordinal);
			}
			if (value is IReadOnlyDictionary<string, bool> source2)
			{
				return source2.ToDictionary<KeyValuePair<string, bool>, string, object>((KeyValuePair<string, bool> pair) => pair.Key, (KeyValuePair<string, bool> pair) => pair.Value, StringComparer.Ordinal);
			}
			if (value is IDictionary<object, object> source3)
			{
				return source3.ToDictionary<KeyValuePair<object, object>, string, object>((KeyValuePair<object, object> pair) => Convert.ToString(pair.Key, CultureInfo.InvariantCulture) ?? string.Empty, (KeyValuePair<object, object> pair) => NormalizeYaml(pair.Value), StringComparer.Ordinal);
			}
			if (value is JsonElement { ValueKind: JsonValueKind.Object } jsonElement)
			{
				return jsonElement.EnumerateObject().ToDictionary<JsonProperty, string, object>((JsonProperty property) => property.Name, (JsonProperty property) => NormalizeJson(property.Value), StringComparer.Ordinal);
			}
			return new Dictionary<string, object>(StringComparer.Ordinal);
		}

		private static object? NormalizeYaml(object? value)
		{
			if (value is IDictionary<object, object> source)
			{
				return source.ToDictionary<KeyValuePair<object, object>, string, object>((KeyValuePair<object, object> pair) => Convert.ToString(pair.Key, CultureInfo.InvariantCulture) ?? string.Empty, (KeyValuePair<object, object> pair) => NormalizeYaml(pair.Value), StringComparer.Ordinal);
			}
			if (value is IDictionary<string, object> source2)
			{
				return source2.ToDictionary<KeyValuePair<string, object>, string, object>((KeyValuePair<string, object> pair) => pair.Key, (KeyValuePair<string, object> pair) => NormalizeYaml(pair.Value), StringComparer.Ordinal);
			}
			if (value is IEnumerable<object> source3 && !(value is string))
			{
				return source3.Select(NormalizeYaml).ToArray();
			}
			return value;
		}

		private static object? NormalizeJson(JsonElement value)
		{
			JsonValueKind valueKind = value.ValueKind;
			if (1 == 0)
			{
			}
			int value2;
			object result = valueKind switch
			{
				JsonValueKind.True => true, 
				JsonValueKind.False => false, 
				JsonValueKind.Number => (!value.TryGetInt32(out value2)) ? ((object)value.GetDouble()) : ((object)value2), 
				JsonValueKind.String => value.GetString(), 
				JsonValueKind.Object => value.EnumerateObject().ToDictionary<JsonProperty, string, object>((JsonProperty property) => property.Name, (JsonProperty property) => NormalizeJson(property.Value), StringComparer.Ordinal), 
				JsonValueKind.Array => value.EnumerateArray().Select(NormalizeJson).ToArray(), 
				JsonValueKind.Null => null, 
				_ => value.ToString(), 
			};
			if (1 == 0)
			{
			}
			return result;
		}
	}

	private sealed class PushConfigScope : IConfigScopeDefinition
	{
		private static readonly IDeserializer Deserializer = new DeserializerBuilder().Build();

		private static readonly ISerializer Serializer = new SerializerBuilder().ConfigureDefaultValuesHandling(DefaultValuesHandling.Preserve).Build();

		private static readonly IReadOnlyDictionary<string, object> Defaults = BuildDefaults();

		public string? AppId => null;

		public ZzzConfigScopeDescriptorDto Descriptor { get; }

		public PushConfigScope()
		{
			Descriptor = new ZzzConfigScopeDescriptorDto("push", "通知设置", InstanceBound: false, GroupBound: false, Writable: true, Defaults.Select<KeyValuePair<string, object>, ZzzConfigSettingDescriptorDto>((KeyValuePair<string, object> pair) => new ZzzConfigSettingDescriptorDto(pair.Key, pair.Key, (pair.Value is bool) ? ZzzConfigValueType.Boolean : ZzzConfigValueType.String, Writable: true, pair.Value)).ToArray());
		}

		public ZzzConfigScopeValuesDto Read(OneDragonEnvironment environment, int? instanceIndex, string groupId)
		{
			Dictionary<string, object?> raw = ReadRaw(ResolvePath(environment));
			Dictionary<string, object> values = Defaults.ToDictionary<KeyValuePair<string, object>, string, object>((KeyValuePair<string, object> pair) => pair.Key, (KeyValuePair<string, object> pair) => raw.TryGetValue(pair.Key, out object value) ? ConvertKnownValue(pair.Key, value) : pair.Value, StringComparer.Ordinal);
			return new ZzzConfigScopeValuesDto(Descriptor, null, null, values);
		}

		public ZzzConfigScopeValuesDto Save(OneDragonEnvironment environment, int? instanceIndex, string groupId, IReadOnlyDictionary<string, object?> values)
		{
			string text = ResolvePath(environment);
			Dictionary<string, object> dictionary = ReadRaw(text);
			foreach (var (key, value) in values)
			{
				if (!Defaults.ContainsKey(key))
				{
					throw new ZzzConfigValidationException(Descriptor.Scope, key, "未知配置 key。");
				}
				dictionary[key] = ConvertKnownValue(key, value);
			}
			Directory.CreateDirectory(Path.GetDirectoryName(text));
			File.WriteAllText(text, Serializer.Serialize(dictionary));
			YamlOperator.InvalidateCache(text);
			return Read(environment, null, groupId);
		}

		private static IReadOnlyDictionary<string, object> BuildDefaults()
		{
			PushConfig pushConfig = new PushConfig();
			Dictionary<string, object> dictionary = new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["send_image"] = pushConfig.SendImage,
				["proxy"] = pushConfig.Proxy,
				["smtp_server"] = pushConfig.SmtpServer,
				["smtp_ssl"] = pushConfig.SmtpSsl,
				["smtp_starttls"] = pushConfig.SmtpStarttls,
				["smtp_email"] = pushConfig.SmtpEmail,
				["smtp_password"] = pushConfig.SmtpPassword,
				["smtp_name"] = pushConfig.SmtpName,
				["webhook_url"] = pushConfig.WebhookUrl,
				["webhook_method"] = pushConfig.WebhookMethod,
				["webhook_content_type"] = pushConfig.WebhookContentType,
				["webhook_headers"] = pushConfig.WebhookHeaders,
				["webhook_body"] = pushConfig.WebhookBody,
				["serverchan_sendkey"] = pushConfig.ServerChanSendKey,
				["qywx_key"] = pushConfig.QywxKey
			};
			foreach (var (key, value) in ZzzPushChannelCatalog.FieldDefaults)
			{
				dictionary.TryAdd(key, value);
			}
			return dictionary;
		}

		private static object ConvertKnownValue(string key, object? value)
		{
			try
			{
				if (value == null)
				{
					throw new InvalidOperationException("值不能为空。");
				}
				return string.Equals(key, "send_image", StringComparison.Ordinal) ? ((object)Convert.ToBoolean(value, CultureInfo.InvariantCulture)) : (Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
			}
			catch (Exception ex) when (((ex is InvalidOperationException || ex is FormatException || ex is InvalidCastException) ? 1 : 0) != 0)
			{
				throw new ZzzConfigValidationException("push", key, "配置值无效：" + ex.Message);
			}
		}

		private static string ResolvePath(OneDragonEnvironment environment)
		{
			return environment.GetPathUnderWorkDir("config", "push.yml");
		}

		private static Dictionary<string, object?> ReadRaw(string path)
		{
			if (!File.Exists(path) || string.IsNullOrWhiteSpace(File.ReadAllText(path)))
			{
				return new Dictionary<string, object>(StringComparer.Ordinal);
			}
			Dictionary<string, object> dictionary = Deserializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(path));
			return (dictionary == null) ? new Dictionary<string, object>(StringComparer.Ordinal) : new Dictionary<string, object>(dictionary, StringComparer.Ordinal);
		}
	}

	private sealed class NotifyConfigScope : IConfigScopeDefinition
	{
		private static readonly IDeserializer Deserializer = new DeserializerBuilder().Build();

		private static readonly ISerializer Serializer = new SerializerBuilder().ConfigureDefaultValuesHandling(DefaultValuesHandling.Preserve).Build();

		private static readonly HashSet<string> KnownKeys = new HashSet<string> { "title", "enable_notify", "merge_error_immediate_notify", "applications", "notify_schema_version", "notify_on_error", "enable_before_notify" };

		public string? AppId => null;

		public ZzzConfigScopeDescriptorDto Descriptor { get; }

		public NotifyConfigScope()
		{
			NotifyConfig notifyConfig = new NotifyConfig();
			Descriptor = new ZzzConfigScopeDescriptorDto("notify", "通知标题", InstanceBound: true, GroupBound: false, Writable: true, new ZzzConfigSettingDescriptorDto[4]
			{
				new ZzzConfigSettingDescriptorDto("title", "title", ZzzConfigValueType.String, Writable: true, notifyConfig.Title),
				new ZzzConfigSettingDescriptorDto("enable_notify", "enable_notify", ZzzConfigValueType.Boolean, Writable: true, notifyConfig.EnableNotify),
				new ZzzConfigSettingDescriptorDto("merge_error_immediate_notify", "merge_error_immediate_notify", ZzzConfigValueType.Boolean, Writable: true, notifyConfig.MergeErrorImmediateNotify),
				new ZzzConfigSettingDescriptorDto("applications", "applications", ZzzConfigValueType.Complex, Writable: true, notifyConfig.Applications)
			});
		}

		public ZzzConfigScopeValuesDto Read(OneDragonEnvironment environment, int? instanceIndex, string groupId)
		{
			int valueOrDefault = instanceIndex.GetValueOrDefault();
			string path = ResolvePath(environment, valueOrDefault);
			Dictionary<string, object> dictionary = ReadRaw(path);
			Dictionary<string, NotifyApplicationSetting> dictionary2 = ReadApplications(dictionary);
			if (!dictionary.ContainsKey("applications"))
			{
				dictionary["applications"] = ToYamlApplications(dictionary2);
				dictionary["merge_error_immediate_notify"] = ReadBool(dictionary, "notify_on_error", fallback: true);
				dictionary["notify_schema_version"] = 2;
				WriteRaw(path, dictionary);
			}
			Dictionary<string, object> values = new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["title"] = ReadString(dictionary, "title", new NotifyConfig().Title),
				["enable_notify"] = ReadBool(dictionary, "enable_notify", fallback: true),
				["merge_error_immediate_notify"] = ReadBool(dictionary, "merge_error_immediate_notify", ReadBool(dictionary, "notify_on_error", fallback: true)),
				["applications"] = dictionary2
			};
			return new ZzzConfigScopeValuesDto(Descriptor, valueOrDefault, null, values);
		}

		public ZzzConfigScopeValuesDto Save(OneDragonEnvironment environment, int? instanceIndex, string groupId, IReadOnlyDictionary<string, object?> values)
		{
			int valueOrDefault = instanceIndex.GetValueOrDefault();
			string path = ResolvePath(environment, valueOrDefault);
			Dictionary<string, object> dictionary = ReadRaw(path);
			foreach (KeyValuePair<string, object> value2 in values)
			{
				value2.Deconstruct(out var key, out var value);
				string text = key;
				object obj = value;
				Dictionary<string, object> dictionary2 = dictionary;
				string key2 = text;
				if (1 == 0)
				{
				}
				switch (text)
				{
				case "title":
					value = Convert.ToString(obj, CultureInfo.InvariantCulture) ?? string.Empty;
					break;
				case "enable_notify":
				case "merge_error_immediate_notify":
					value = Convert.ToBoolean(obj, CultureInfo.InvariantCulture);
					break;
				case "applications":
					value = ToYamlApplications(ConvertApplications(obj));
					break;
				default:
					throw new ZzzConfigValidationException(Descriptor.Scope, text, "未知配置 key。");
				}
				if (1 == 0)
				{
				}
				dictionary2[key2] = value;
			}
			WriteRaw(path, dictionary);
			return Read(environment, valueOrDefault, groupId);
		}

		private static Dictionary<string, NotifyApplicationSetting> ReadApplications(IReadOnlyDictionary<string, object?> raw)
		{
			if (raw.TryGetValue("applications", out object value))
			{
				return ConvertApplications(value);
			}
			bool before = ReadBool(raw, "enable_before_notify", fallback: true);
			bool onError = ReadBool(raw, "notify_on_error", fallback: true);
			Dictionary<string, NotifyApplicationSetting> dictionary = new Dictionary<string, NotifyApplicationSetting>(StringComparer.Ordinal);
			foreach (var (text2, value2) in raw)
			{
				if (!KnownKeys.Contains(text2) && TryReadInt(value2, out var result))
				{
					dictionary[text2] = LegacySetting(result, before, onError);
				}
			}
			return dictionary;
		}

		private static Dictionary<string, NotifyApplicationSetting> ConvertApplications(object? value)
		{
			if (value is Dictionary<string, NotifyApplicationSetting> source)
			{
				return source.ToDictionary<KeyValuePair<string, NotifyApplicationSetting>, string, NotifyApplicationSetting>((KeyValuePair<string, NotifyApplicationSetting> pair) => pair.Key, (KeyValuePair<string, NotifyApplicationSetting> pair) => new NotifyApplicationSetting
				{
					Lifecycle = pair.Value.Lifecycle,
					Detail = pair.Value.Detail
				}, StringComparer.Ordinal);
			}
			object value2 = NormalizeYaml(value);
			return JsonSerializer.Deserialize<Dictionary<string, NotifyApplicationSetting>>(JsonSerializer.Serialize(value2)) ?? new Dictionary<string, NotifyApplicationSetting>(StringComparer.Ordinal);
		}

		private static Dictionary<string, object?> ToYamlApplications(IReadOnlyDictionary<string, NotifyApplicationSetting> applications)
		{
			return applications.ToDictionary<KeyValuePair<string, NotifyApplicationSetting>, string, object>((KeyValuePair<string, NotifyApplicationSetting> pair) => pair.Key, (KeyValuePair<string, NotifyApplicationSetting> pair) => new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["lifecycle"] = pair.Value.Lifecycle,
				["detail"] = pair.Value.Detail
			}, StringComparer.Ordinal);
		}

		private static NotifyApplicationSetting LegacySetting(int level, bool before, bool onError)
		{
			if (level <= 0)
			{
				return new NotifyApplicationSetting
				{
					Lifecycle = "off",
					Detail = "off"
				};
			}
			string lifecycle = (before ? "start_and_finish" : "finish_only");
			if (1 == 0)
			{
			}
			string text = level switch
			{
				1 => onError ? "error_only" : "off", 
				3 => "merge", 
				_ => "all", 
			};
			if (1 == 0)
			{
			}
			string detail = text;
			return new NotifyApplicationSetting
			{
				Lifecycle = lifecycle,
				Detail = detail
			};
		}

		private static object? NormalizeYaml(object? value)
		{
			if (value is IDictionary<object, object> source)
			{
				return source.ToDictionary<KeyValuePair<object, object>, string, object>((KeyValuePair<object, object> pair) => Convert.ToString(pair.Key, CultureInfo.InvariantCulture) ?? string.Empty, (KeyValuePair<object, object> pair) => NormalizeYaml(pair.Value), StringComparer.Ordinal);
			}
			if (value is IDictionary<string, object> source2)
			{
				return source2.ToDictionary<KeyValuePair<string, object>, string, object>((KeyValuePair<string, object> pair) => pair.Key, (KeyValuePair<string, object> pair) => NormalizeYaml(pair.Value), StringComparer.Ordinal);
			}
			if (value is IEnumerable<object> source3 && !(value is string))
			{
				return source3.Select(NormalizeYaml).ToArray();
			}
			return value;
		}

		private static bool ReadBool(IReadOnlyDictionary<string, object?> raw, string key, bool fallback)
		{
			object value;
			return (raw.TryGetValue(key, out value) && value != null) ? Convert.ToBoolean(value, CultureInfo.InvariantCulture) : fallback;
		}

		private static string ReadString(IReadOnlyDictionary<string, object?> raw, string key, string fallback)
		{
			object value;
			return (raw.TryGetValue(key, out value) && value != null) ? (Convert.ToString(value, CultureInfo.InvariantCulture) ?? fallback) : fallback;
		}

		private static bool TryReadInt(object? value, out int result)
		{
			try
			{
				result = Convert.ToInt32(value, CultureInfo.InvariantCulture);
				return true;
			}
			catch (Exception ex) when (((ex is FormatException || ex is InvalidCastException || ex is OverflowException) ? 1 : 0) != 0)
			{
				result = 0;
				return false;
			}
		}

		private static string ResolvePath(OneDragonEnvironment environment, int instanceIndex)
		{
			return environment.GetPathUnderWorkDir("config", instanceIndex.ToString("00"), "notify.yml");
		}

		private static Dictionary<string, object?> ReadRaw(string path)
		{
			if (!File.Exists(path) || string.IsNullOrWhiteSpace(File.ReadAllText(path)))
			{
				return new Dictionary<string, object>(StringComparer.Ordinal);
			}
			Dictionary<string, object> dictionary = Deserializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(path));
			return (dictionary == null) ? new Dictionary<string, object>(StringComparer.Ordinal) : new Dictionary<string, object>(dictionary, StringComparer.Ordinal);
		}

		private static void WriteRaw(string path, IReadOnlyDictionary<string, object?> raw)
		{
			Directory.CreateDirectory(Path.GetDirectoryName(path));
			File.WriteAllText(path, Serializer.Serialize(raw));
			YamlOperator.InvalidateCache(path);
		}
	}

	private sealed class ChargePlanConfigScope : IConfigScopeDefinition
	{
		private static readonly IDeserializer Deserializer = new DeserializerBuilder().Build();

		private static readonly ISerializer Serializer = new SerializerBuilder().ConfigureDefaultValuesHandling(DefaultValuesHandling.Preserve).Build();

		public string? AppId => "charge_plan";

		public ZzzConfigScopeDescriptorDto Descriptor { get; }

		public ChargePlanConfigScope()
		{
			ChargePlanConfig chargePlanConfig = new ChargePlanConfig();
			Descriptor = new ZzzConfigScopeDescriptorDto("charge-plan", "体力计划", InstanceBound: true, GroupBound: true, Writable: true, new ZzzConfigSettingDescriptorDto[9]
			{
				new ZzzConfigSettingDescriptorDto("plan_list", "plan_list", ZzzConfigValueType.Complex, Writable: true, chargePlanConfig.PlanList),
				new ZzzConfigSettingDescriptorDto("restore_charge", "restore_charge", ZzzConfigValueType.String, Writable: true, chargePlanConfig.RestoreCharge),
				new ZzzConfigSettingDescriptorDto("history_list", "history_list", ZzzConfigValueType.Complex, Writable: true, chargePlanConfig.HistoryList),
				new ZzzConfigSettingDescriptorDto("loop", "loop", ZzzConfigValueType.Boolean, Writable: true, chargePlanConfig.Loop),
				new ZzzConfigSettingDescriptorDto("daily_reset_plan_times", "daily_reset_plan_times", ZzzConfigValueType.Boolean, Writable: true, chargePlanConfig.DailyResetPlanTimes),
				new ZzzConfigSettingDescriptorDto("last_daily_reset_dt", "last_daily_reset_dt", ZzzConfigValueType.String, Writable: true, chargePlanConfig.LastDailyResetDt),
				new ZzzConfigSettingDescriptorDto("skip_plan", "skip_plan", ZzzConfigValueType.Boolean, Writable: true, chargePlanConfig.SkipPlan),
				new ZzzConfigSettingDescriptorDto("double_reward", "double_reward", ZzzConfigValueType.Boolean, Writable: true, chargePlanConfig.DoubleReward),
				new ZzzConfigSettingDescriptorDto("combat_simulation_double_reward_config", "combat_simulation_double_reward_config", ZzzConfigValueType.Complex, Writable: true, chargePlanConfig.CombatSimulationDoubleRewardConfig)
			});
		}

		public ZzzConfigScopeValuesDto Read(OneDragonEnvironment environment, int? instanceIndex, string groupId)
		{
			int valueOrDefault = instanceIndex.GetValueOrDefault();
			string path = ResolvePath(environment, valueOrDefault, groupId);
			Dictionary<string, object> dictionary = ReadRaw(path);
			ChargePlanConfig chargePlanConfig = new ChargePlanConfig();
			Dictionary<string, object> values = new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["plan_list"] = ConvertValue<List<ChargePlanItem>>(dictionary.GetValueOrDefault("plan_list")) ?? new List<ChargePlanItem>(),
				["restore_charge"] = ReadString(dictionary, "restore_charge", chargePlanConfig.RestoreCharge),
				["history_list"] = ConvertValue<List<ChargePlanItem>>(dictionary.GetValueOrDefault("history_list")) ?? new List<ChargePlanItem>(),
				["loop"] = ReadBool(dictionary, "loop", chargePlanConfig.Loop),
				["daily_reset_plan_times"] = ReadBool(dictionary, "daily_reset_plan_times", chargePlanConfig.DailyResetPlanTimes),
				["last_daily_reset_dt"] = ReadString(dictionary, "last_daily_reset_dt", chargePlanConfig.LastDailyResetDt),
				["skip_plan"] = ReadBool(dictionary, "skip_plan", chargePlanConfig.SkipPlan),
				["double_reward"] = ReadBool(dictionary, "double_reward", chargePlanConfig.DoubleReward),
				["combat_simulation_double_reward_config"] = ConvertValue<ChargePlanItem>(dictionary.GetValueOrDefault("combat_simulation_double_reward_config")) ?? chargePlanConfig.CombatSimulationDoubleRewardConfig
			};
			return new ZzzConfigScopeValuesDto(Descriptor, valueOrDefault, groupId, values);
		}

		public ZzzConfigScopeValuesDto Save(OneDragonEnvironment environment, int? instanceIndex, string groupId, IReadOnlyDictionary<string, object?> values)
		{
			int valueOrDefault = instanceIndex.GetValueOrDefault();
			string text = ResolvePath(environment, valueOrDefault, groupId);
			Dictionary<string, object> dictionary = ReadRaw(text);
			foreach (KeyValuePair<string, object> value2 in values)
			{
				value2.Deconstruct(out var key, out var value);
				string text2 = key;
				object obj = value;
				Dictionary<string, object> dictionary2 = dictionary;
				string key2 = text2;
				if (1 == 0)
				{
				}
				switch (text2)
				{
				case "plan_list":
				case "history_list":
					value = ConvertValue<List<ChargePlanItem>>(obj) ?? new List<ChargePlanItem>();
					if (text2 == "plan_list" && value is List<ChargePlanItem> typedPlans)
					{
						ValidateChargePlanItems(typedPlans);
					}
					break;
				case "restore_charge":
				case "last_daily_reset_dt":
					value = Convert.ToString(obj, CultureInfo.InvariantCulture) ?? string.Empty;
					break;
				case "loop":
				case "daily_reset_plan_times":
				case "skip_plan":
				case "double_reward":
					value = Convert.ToBoolean(obj, CultureInfo.InvariantCulture);
					break;
				case "combat_simulation_double_reward_config":
					value = ConvertValue<ChargePlanItem>(obj) ?? new ChargePlanItem();
					break;
				default:
					throw new ZzzConfigValidationException(Descriptor.Scope, text2, "未知配置 key。");
				}
				if (1 == 0)
				{
				}
				dictionary2[key2] = value;
			}
			Directory.CreateDirectory(Path.GetDirectoryName(text));
			File.WriteAllText(text, Serializer.Serialize(dictionary));
			YamlOperator.InvalidateCache(text);
			return Read(environment, valueOrDefault, groupId);
		}

		private static T? ConvertValue<T>(object? value)
		{
			if (value is T result)
			{
				return result;
			}
			if (value is JsonElement jsonElement)
			{
				return JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
			}
			object obj = NormalizeYaml(value);
			return (obj == null) ? default(T) : Deserializer.Deserialize<T>(Serializer.Serialize(obj));
		}

		/// <summary>
		/// 校验体力计划项业务合法性（对应 Python charge_plan_config.validate_item）。
		/// 合成电池等无 mission_type 的分类要求 mission_type_name 为空，防止写入非法计划。
		/// </summary>
		private static void ValidateChargePlanItems(IReadOnlyList<ChargePlanItem> plans)
		{
			if (plans == null)
			{
				return;
			}
			foreach (ChargePlanItem plan in plans)
			{
				bool hasMissionType = !string.IsNullOrWhiteSpace(plan.MissionTypeName);
				if (plan.CategoryName == "合成电池" && hasMissionType)
				{
					throw new ZzzConfigValidationException("charge-plan", "plan_list", "合成电池 无 mission_type，mission_type_name 应为空");
				}
			}
		}

		private static object? NormalizeYaml(object? value)
		{
			if (value is IDictionary<object, object> source)
			{
				return source.ToDictionary<KeyValuePair<object, object>, string, object>((KeyValuePair<object, object> pair) => Convert.ToString(pair.Key, CultureInfo.InvariantCulture) ?? string.Empty, (KeyValuePair<object, object> pair) => NormalizeYaml(pair.Value), StringComparer.Ordinal);
			}
			if (value is IDictionary<string, object> source2)
			{
				return source2.ToDictionary<KeyValuePair<string, object>, string, object>((KeyValuePair<string, object> pair) => pair.Key, (KeyValuePair<string, object> pair) => NormalizeYaml(pair.Value), StringComparer.Ordinal);
			}
			if (value is IEnumerable<object> source3 && !(value is string))
			{
				return source3.Select(NormalizeYaml).ToArray();
			}
			return value;
		}

		private static bool ReadBool(IReadOnlyDictionary<string, object?> raw, string key, bool fallback)
		{
			object value;
			return (raw.TryGetValue(key, out value) && value != null) ? Convert.ToBoolean(value, CultureInfo.InvariantCulture) : fallback;
		}

		private static string ReadString(IReadOnlyDictionary<string, object?> raw, string key, string fallback)
		{
			object value;
			return (raw.TryGetValue(key, out value) && value != null) ? (Convert.ToString(value, CultureInfo.InvariantCulture) ?? fallback) : fallback;
		}

		private static string ResolvePath(OneDragonEnvironment environment, int instanceIndex, string groupId)
		{
			return environment.GetPathUnderWorkDir("config", instanceIndex.ToString("00"), groupId, "charge_plan.yml");
		}

		private static Dictionary<string, object?> ReadRaw(string path)
		{
			if (!File.Exists(path))
			{
				return new Dictionary<string, object>(StringComparer.Ordinal);
			}
			string text = File.ReadAllText(path);
			if (string.IsNullOrWhiteSpace(text))
			{
				return new Dictionary<string, object>(StringComparer.Ordinal);
			}
			Dictionary<string, object> dictionary = Deserializer.Deserialize<Dictionary<string, object>>(text);
			return (dictionary == null) ? new Dictionary<string, object>(StringComparer.Ordinal) : new Dictionary<string, object>(dictionary, StringComparer.Ordinal);
		}
	}

	private sealed class CommissionAssistantConfigScope : IConfigScopeDefinition
	{
		private static readonly IReadOnlyDictionary<string, (Type Type, object DefaultValue)> Fields = new Dictionary<string, (Type, object)>(StringComparer.Ordinal)
		{
			["pause_in_background"] = (typeof(bool), true),
			["dialog_click_interval"] = (typeof(double), 0.5),
			["dialog_option"] = (typeof(string), CommissionAssistantDialogOption.Last.Value),
			["story_mode"] = (typeof(string), CommissionAssistantStoryMode.Click.Value),
			["sleep_after_empty_screen"] = (typeof(double), 0.5),
			["dodge_config"] = (typeof(string), "闪避"),
			["dodge_switch"] = (typeof(string), "5"),
			["auto_battle"] = (typeof(string), "全配队通用"),
			["auto_battle_switch"] = (typeof(string), "6")
		};

		private static readonly IDeserializer Deserializer = new DeserializerBuilder().Build();

		private static readonly ISerializer Serializer = new SerializerBuilder().ConfigureDefaultValuesHandling(DefaultValuesHandling.Preserve).Build();

		public string? AppId => "commission_assistant";

		public ZzzConfigScopeDescriptorDto Descriptor { get; }

		public CommissionAssistantConfigScope()
		{
			Descriptor = new ZzzConfigScopeDescriptorDto("commission-assistant", "委托助手", InstanceBound: true, GroupBound: true, Writable: true, Fields.Select<KeyValuePair<string, (Type, object)>, ZzzConfigSettingDescriptorDto>((KeyValuePair<string, (Type Type, object DefaultValue)> field) => new ZzzConfigSettingDescriptorDto(field.Key, field.Key, GetValueType(field.Value.Type), Writable: true, field.Value.DefaultValue)).ToArray());
		}

		public ZzzConfigScopeValuesDto Read(OneDragonEnvironment environment, int? instanceIndex, string groupId)
		{
			int num = ResolveInstanceIndex(instanceIndex);
			string path = ResolvePath(environment, num, groupId);
			Dictionary<string, object> dictionary = ReadRaw(path);
			Dictionary<string, object> dictionary2 = new Dictionary<string, object>(StringComparer.Ordinal);
			foreach (KeyValuePair<string, (Type, object)> field in Fields)
			{
				field.Deconstruct(out var key, out var value);
				(Type, object) tuple = value;
				string key2 = key;
				Type item = tuple.Item1;
				object item2 = tuple.Item2;
				dictionary2[key2] = (dictionary.TryGetValue(key2, out var value2) ? ConvertValue(value2, item, Descriptor.Scope, key2) : item2);
			}
			return new ZzzConfigScopeValuesDto(Descriptor, num, groupId, dictionary2);
		}

		public ZzzConfigScopeValuesDto Save(OneDragonEnvironment environment, int? instanceIndex, string groupId, IReadOnlyDictionary<string, object?> values)
		{
			int num = ResolveInstanceIndex(instanceIndex);
			string text = ResolvePath(environment, num, groupId);
			Dictionary<string, object> dictionary = ReadRaw(text);
			foreach (var (key, value) in values)
			{
				if (!Fields.TryGetValue(key, out (Type, object) value2))
				{
					throw new ZzzConfigValidationException(Descriptor.Scope, key, "未知配置 key。");
				}
				dictionary[key] = ConvertValue(value, value2.Item1, Descriptor.Scope, key);
			}
			Directory.CreateDirectory(Path.GetDirectoryName(text));
			File.WriteAllText(text, Serializer.Serialize(dictionary));
			YamlOperator.InvalidateCache(text);
			return Read(environment, num, groupId);
		}

		private static int ResolveInstanceIndex(int? instanceIndex)
		{
			return instanceIndex.GetValueOrDefault();
		}

		private static ZzzConfigValueType GetValueType(Type type)
		{
			return (type == typeof(bool)) ? ZzzConfigValueType.Boolean : ((type == typeof(double)) ? ZzzConfigValueType.Number : ZzzConfigValueType.String);
		}

		private static object ConvertValue(object? value, Type targetType, string scope, string key)
		{
			try
			{
				if (value == null)
				{
					throw new InvalidOperationException("值不能为空。");
				}
				if (targetType == typeof(string))
				{
					return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
				}
				if (targetType == typeof(bool))
				{
					return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
				}
				if (targetType == typeof(double))
				{
					return Convert.ToDouble(value, CultureInfo.InvariantCulture);
				}
				throw new InvalidOperationException("不支持的配置类型 " + targetType.Name + "。");
			}
			catch (Exception ex) when (((ex is InvalidOperationException || ex is FormatException || ex is InvalidCastException || ex is OverflowException) ? 1 : 0) != 0)
			{
				throw new ZzzConfigValidationException(scope, key, "配置值无效：" + ex.Message);
			}
		}

		private static string ResolvePath(OneDragonEnvironment environment, int instanceIndex, string groupId)
		{
			string pathUnderWorkDir = environment.GetPathUnderWorkDir("config", instanceIndex.ToString("00"), groupId);
			string text = Path.Combine(pathUnderWorkDir, "screenshot_helper.yml");
			string pathUnderWorkDir2 = environment.GetPathUnderWorkDir("config", instanceIndex.ToString("00"), "screenshot_helper.yml");
			if (!File.Exists(text) && File.Exists(pathUnderWorkDir2))
			{
				Directory.CreateDirectory(pathUnderWorkDir);
				File.Copy(pathUnderWorkDir2, text, overwrite: false);
			}
			return text;
		}

		private static Dictionary<string, object?> ReadRaw(string path)
		{
			if (!File.Exists(path))
			{
				return new Dictionary<string, object>(StringComparer.Ordinal);
			}
			string text = File.ReadAllText(path);
			if (string.IsNullOrWhiteSpace(text))
			{
				return new Dictionary<string, object>(StringComparer.Ordinal);
			}
			Dictionary<string, object> dictionary = Deserializer.Deserialize<Dictionary<string, object>>(text);
			return (dictionary == null) ? new Dictionary<string, object>(StringComparer.Ordinal) : new Dictionary<string, object>(dictionary, StringComparer.Ordinal);
		}
	}

	private sealed class ConfigScope<T> : IConfigScopeDefinition where T : class, new()
	{
		private readonly string _moduleName;

		private readonly string[] _subDirectoryPrefix;

		private readonly bool _instanceBound;

		private readonly bool _groupBound;

		public string? AppId { get; }

		public ZzzConfigScopeDescriptorDto Descriptor { get; }

		private static IReadOnlyList<PropertyInfo> WritableProperties { get; } = (from property in typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public)
			where property.GetCustomAttribute<YamlIgnoreAttribute>() == null
			where (object)property.GetMethod != null
			where property.GetIndexParameters().Length == 0
			select property).ToArray();

		private ConfigScope(string scope, string title, string moduleName, bool instanceBound, bool groupBound, string? appId, IReadOnlyList<string> subDirectoryPrefix)
		{
			_moduleName = moduleName;
			_instanceBound = instanceBound;
			_groupBound = groupBound;
			_subDirectoryPrefix = subDirectoryPrefix.ToArray();
			AppId = appId;
			Descriptor = new ZzzConfigScopeDescriptorDto(scope, title, instanceBound, groupBound, Writable: true, BuildSettings());
		}

		public static ConfigScope<T> Shared(string scope, string title, string moduleName)
		{
			return new ConfigScope<T>(scope, title, moduleName, instanceBound: false, groupBound: false, null, Array.Empty<string>());
		}

		public static ConfigScope<T> Instance(string scope, string title, string moduleName)
		{
			return new ConfigScope<T>(scope, title, moduleName, instanceBound: true, groupBound: false, null, Array.Empty<string>());
		}

		public static ConfigScope<T> Application(string scope, string title, string appId, IReadOnlyList<string> subDirectoryPrefix)
		{
			return new ConfigScope<T>(scope, title, appId, instanceBound: true, groupBound: true, appId, subDirectoryPrefix);
		}

		public static ConfigScope<T> Group(string scope, string title, string moduleName)
		{
			return new ConfigScope<T>(scope, title, moduleName, instanceBound: true, groupBound: true, null, Array.Empty<string>());
		}

		public ZzzConfigScopeValuesDto Read(OneDragonEnvironment environment, int? instanceIndex, string groupId)
		{
			YamlConfig<T> yamlConfig = CreateConfig(environment, instanceIndex, groupId);
			return new ZzzConfigScopeValuesDto(Descriptor, _instanceBound ? new int?(ResolveInstanceIndex(instanceIndex)) : ((int?)null), _groupBound ? groupId : null, ReadValues(yamlConfig.Current));
		}

		public ZzzConfigScopeValuesDto Save(OneDragonEnvironment environment, int? instanceIndex, string groupId, IReadOnlyDictionary<string, object?> values)
		{
			YamlConfig<T> yamlConfig = CreateConfig(environment, instanceIndex, groupId);
			Dictionary<string, PropertyInfo> dictionary = WritableProperties.ToDictionary<PropertyInfo, string>(GetYamlKey, StringComparer.Ordinal);
			foreach (var (key, value) in values)
			{
				if (!dictionary.TryGetValue(key, out var value2))
				{
					throw new ZzzConfigValidationException(Descriptor.Scope, key, "未知配置 key。");
				}
				value2.SetValue(yamlConfig.Current, ConvertValue(value, value2.PropertyType, Descriptor.Scope, key));
			}
			yamlConfig.Save();
			return Read(environment, instanceIndex, groupId);
		}

		private YamlConfig<T> CreateConfig(OneDragonEnvironment environment, int? instanceIndex, string groupId)
		{
			IReadOnlyList<string> subDirectories = (_groupBound ? ((IReadOnlyList<string>)_subDirectoryPrefix.Concat(new string[] { groupId }).ToArray()) : ((IReadOnlyList<string>)_subDirectoryPrefix));
			return new YamlConfig<T>(environment, _moduleName, null, _instanceBound ? new int?(ResolveInstanceIndex(instanceIndex)) : ((int?)null), subDirectories);
		}

		private static int ResolveInstanceIndex(int? instanceIndex)
		{
			return instanceIndex.GetValueOrDefault();
		}

		private static IReadOnlyDictionary<string, object?> ReadValues(T value)
		{
			return WritableProperties.ToDictionary<PropertyInfo, string, object>(GetYamlKey, (PropertyInfo property) => property.GetValue(value), StringComparer.Ordinal);
		}

		private static IReadOnlyList<ZzzConfigSettingDescriptorDto> BuildSettings()
		{
			T defaults = new T();
			return WritableProperties.Select((PropertyInfo property) => new ZzzConfigSettingDescriptorDto(GetYamlKey(property), GetYamlKey(property), GetValueType(property.PropertyType), (object)property.SetMethod != null && property.SetMethod.IsPublic, property.GetValue(defaults))).ToArray();
		}

		private static string GetYamlKey(PropertyInfo property)
		{
			return property.GetCustomAttribute<YamlMemberAttribute>()?.Alias ?? ToSnakeCase(property.Name);
		}

		private static ZzzConfigValueType GetValueType(Type type)
		{
			Type type2 = Nullable.GetUnderlyingType(type) ?? type;
			if (type2 == typeof(bool))
			{
				return ZzzConfigValueType.Boolean;
			}
			if (type2 == typeof(byte) || type2 == typeof(short) || type2 == typeof(int) || type2 == typeof(long))
			{
				return ZzzConfigValueType.Integer;
			}
			if (type2 == typeof(float) || type2 == typeof(double) || type2 == typeof(decimal))
			{
				return ZzzConfigValueType.Number;
			}
			return (!(type2 == typeof(string))) ? ZzzConfigValueType.Complex : ZzzConfigValueType.String;
		}

		private static object? ConvertValue(object? value, Type targetType, string scope, string key)
		{
			Type type = Nullable.GetUnderlyingType(targetType) ?? targetType;
			try
			{
				if (value is JsonElement json)
				{
					return ConvertJsonValue(json, type);
				}
				if (value == null)
				{
					if (!(targetType == type))
					{
						return null;
					}
					throw new InvalidOperationException("值不能为空。");
				}
				if (type.IsInstanceOfType(value))
				{
					return value;
				}
				if (type == typeof(string))
				{
					return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
				}
				if (type == typeof(bool))
				{
					return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
				}
				if (type == typeof(int))
				{
					return Convert.ToInt32(value, CultureInfo.InvariantCulture);
				}
				if (type == typeof(long))
				{
					return Convert.ToInt64(value, CultureInfo.InvariantCulture);
				}
				if (type == typeof(float))
				{
					return Convert.ToSingle(value, CultureInfo.InvariantCulture);
				}
				if (type == typeof(double))
				{
					return Convert.ToDouble(value, CultureInfo.InvariantCulture);
				}
				if (type == typeof(decimal))
				{
					return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
				}
				return JsonSerializer.Deserialize(JsonSerializer.Serialize(value), type);
			}
			catch (Exception ex) when (((ex is InvalidOperationException || ex is FormatException || ex is InvalidCastException || ex is JsonException) ? 1 : 0) != 0)
			{
				throw new ZzzConfigValidationException(scope, key, "配置值无效：" + ex.Message);
			}
		}

		private static object? ConvertJsonValue(JsonElement json, Type target)
		{
			if (json.ValueKind == JsonValueKind.Null)
			{
				return null;
			}
			if (target == typeof(string))
			{
				return (json.ValueKind == JsonValueKind.String) ? (json.GetString() ?? string.Empty) : json.ToString();
			}
			return JsonSerializer.Deserialize(json.GetRawText(), target);
		}

		private static string ToSnakeCase(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				return value;
			}
			List<char> list = new List<char>();
			for (int i = 0; i < value.Length; i++)
			{
				char c = value[i];
				if (char.IsUpper(c) && i > 0)
				{
					list.Add('_');
				}
				list.Add(char.ToLowerInvariant(c));
			}
			return new string(list.ToArray());
		}
	}

	private sealed class ZzzConfigValidationException : Exception
	{
		public string? Scope { get; }

		public string? Key { get; }

		public ZzzConfigValidationException(string? scope, string? key, string message)
			: base(message)
		{
			Scope = scope;
			Key = key;
		}
	}

	private const string DefaultGroupId = "default";

	private readonly string _runRoot;

	private readonly IReadOnlyDictionary<string, IConfigScopeDefinition> _scopes;

	private OneDragonEnvironment Environment => new OneDragonEnvironment(_runRoot);

	public ZzzConfigScopeService(string runRoot)
	{
		_runRoot = runRoot;
		_scopes = BuildScopes();
	}

	public IReadOnlyList<ZzzConfigScopeDescriptorDto> GetDescriptors()
	{
		return _scopes.Values.Select((IConfigScopeDefinition scope) => scope.Descriptor).ToArray();
	}

	public ZzzBackendResult<ZzzConfigScopeValuesDto> Read(string scopeName, int? instanceIndex, string? groupId)
	{
		if (!_scopes.TryGetValue(scopeName, out IConfigScopeDefinition value))
		{
			return Validation<ZzzConfigScopeValuesDto>(scopeName, null, "未知配置 scope。");
		}
		try
		{
			return ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(value.Read(Environment, instanceIndex, NormalizeGroupId(groupId)));
		}
		catch (Exception ex)
		{
			return ZzzBackendResult<ZzzConfigScopeValuesDto>.Fail(ZzzBackendErrorCode.NotReady, ex.Message);
		}
	}

	public ZzzBackendResult<ZzzConfigScopeValuesDto> Save(ZzzSaveConfigScopeRequest request)
	{
		ArgumentNullException.ThrowIfNull(request, "request");
		if (!_scopes.TryGetValue(request.Scope, out IConfigScopeDefinition value))
		{
			return Validation<ZzzConfigScopeValuesDto>(request.Scope, null, "未知配置 scope。");
		}
		if (!value.Descriptor.Writable)
		{
			return Validation<ZzzConfigScopeValuesDto>(request.Scope, null, "配置 scope 当前不可写。");
		}
		try
		{
			return ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(value.Save(Environment, request.InstanceIndex, NormalizeGroupId(request.GroupId), request.Values));
		}
		catch (ZzzConfigValidationException ex)
		{
			return Validation<ZzzConfigScopeValuesDto>(ex.Scope, ex.Key, ex.Message);
		}
		catch (Exception ex2)
		{
			return ZzzBackendResult<ZzzConfigScopeValuesDto>.Fail(ZzzBackendErrorCode.NotReady, ex2.Message);
		}
	}

	public IReadOnlyList<string> GetScopesForApp(string appId)
	{
		return (from scope in _scopes.Values
			where string.Equals(scope.AppId, appId, StringComparison.Ordinal)
			select scope.Descriptor.Scope).ToArray();
	}

	private static string NormalizeGroupId(string? groupId)
	{
		return string.IsNullOrWhiteSpace(groupId) ? "default" : groupId.Trim();
	}

	private static IReadOnlyDictionary<string, IConfigScopeDefinition> BuildScopes()
	{
		IConfigScopeDefinition[] source = new IConfigScopeDefinition[31]
		{
			ConfigScope<GameConfig>.Instance("game", "游戏设置", "game"),
			ConfigScope<GameAccountConfig>.Instance("instance", "当前账号", "game_account"),
			ConfigScope<StandaloneAppConfig>.Instance("standalone-app", "独立应用运行", "standalone_app"),
			ConfigScope<ZzzOd.GameLogic.Config.ModelConfig>.Shared("model", "业务模型", "model"),
			ConfigScope<TeamConfig>.Instance("team", "预备编队", "team"),
			ConfigScope<OneDragonConfig>.Shared("one-dragon", "一条龙运行", "one_dragon"),
			ConfigScope<OneDragonApplicationGroupConfig>.Group("one-dragon-group", "一条龙应用组", "_group"),
			ConfigScope<EnvConfig>.Shared("env", "脚本环境", "env"),
			ConfigScope<ProjectConfig>.Shared("project", "项目设置", "project"),
			ConfigScope<CustomGuiConfig>.Shared("custom", "自定义设置", "custom"),
			new OverlayConfigScope(),
			new NotifyConfigScope(),
			new PushConfigScope(),
			ConfigScope<CoffeeConfig>.Application("coffee", "咖啡计划", "coffee", new string[] { "app_config" }),
			new ChargePlanConfigScope(),
			ConfigScope<NotoriousHuntConfig>.Application("notorious-hunt", "恶名狩猎计划", "notorious_hunt", new string[] { "app_config" }),
			ConfigScope<ShiyuDefenseConfig>.Application("shiyu-defense", "式舆防卫战", "shiyu_defense", new string[] { "app_config" }),
			ConfigScope<SuibianTempleConfig>.Application("suibian-temple", "随便观", "suibian_temple", new string[] { "app_config" }),
			ConfigScope<LostVoidConfig>.Application("lost-void", "迷失之地", "lost_void", new string[] { "app_config" }),
			ConfigScope<WitheredDomainConfig>.Application("withered-domain", "枯萎之都", "withered_domain", new string[] { "app_config" }),
			ConfigScope<WorldPatrolConfig>.Application("world-patrol", "锄大地", "world_patrol", new string[] { "app_config" }),
			ConfigScope<DriveDiscDismantleConfig>.Application("drive-disc-dismantle", "驱动盘拆解", "drive_disc_dismantle", new string[] { "app_config" }),
			ConfigScope<RandomPlayConfig>.Application("random-play", "录像店营业", "random_play", new string[] { "app_config" }),
			ConfigScope<DailySignInConfig>.Application("daily-signin", "每日签到", "daily_signin", new string[] { "app_config" }),
			ConfigScope<LifeOnLineConfig>.Application("life-on-line", "生命热线", "life_on_line", new string[] { "app_config" }),
			ConfigScope<IntelBoardConfig>.Application("intel-board", "情报板", "intel_board", new string[] { "app_config" }),
			ConfigScope<ScreenshotHelperConfig>.Application("screenshot-helper", "截图助手", "screenshot_helper", new string[] { "app_config" }),
			ConfigScope<OperationDebugConfig>.Application("operation-debug", "指令调试", "operation_debug", new string[] { "app_config" }),
			new CommissionAssistantConfigScope(),
			ConfigScope<BattleAssistantConfig>.Instance("battle-assistant", "战斗助手", "battle_assistant"),
			ConfigScope<RedemptionCodeConfigData>.Shared("redemption-code", "兑换码", "redemption_codes")
		};
		return source.ToDictionary<IConfigScopeDefinition, string>((IConfigScopeDefinition scope) => scope.Descriptor.Scope, StringComparer.Ordinal);
	}

	private static IConfigScopeDefinition EmptyScope(string scope, string title)
	{
		return new EmptyConfigScopeDefinition(new ZzzConfigScopeDescriptorDto(scope, title, InstanceBound: false, GroupBound: false, Writable: false, Array.Empty<ZzzConfigSettingDescriptorDto>()));
	}

	private static ZzzBackendResult<T> Validation<T>(string? scope, string? key, string message)
	{
		ZzzValidationErrorDto value = new ZzzValidationErrorDto(scope, key, message);
		string error = JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
		return ZzzBackendResult<T>.Fail(ZzzBackendErrorCode.Validation, error);
	}
}
