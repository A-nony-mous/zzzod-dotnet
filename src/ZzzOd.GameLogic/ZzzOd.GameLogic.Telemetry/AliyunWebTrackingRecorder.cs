using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Serilog;

namespace ZzzOd.GameLogic.Telemetry;

/// <summary>
/// 阿里云 WebTracking 遥测记录器。
/// </summary>
public sealed class AliyunWebTrackingRecorder : ITelemetryRecorder, IDisposable
{
	private readonly HttpClient _httpClient;

	private readonly bool _ownsHttpClient;

	private readonly Uri _endpoint;

	/// <summary>
	/// 初始化阿里云 WebTracking 记录器。
	/// </summary>
	/// <param name="endpoint">上报地址。</param>
	/// <param name="httpClient">可替换的 HTTP 客户端。</param>
	public AliyunWebTrackingRecorder(string endpoint, HttpClient? httpClient = null)
	{
		if (string.IsNullOrWhiteSpace(endpoint))
		{
			throw new ArgumentException("Aliyun WebTracking endpoint is required.", "endpoint");
		}
		_endpoint = new Uri(endpoint, UriKind.Absolute);
		_httpClient = httpClient ?? new HttpClient
		{
			Timeout = TimeSpan.FromSeconds(3L)
		};
		_ownsHttpClient = httpClient == null;
	}

	/// <inheritdoc />
	public void Record(string eventName, IReadOnlyDictionary<string, string> properties)
	{
		try
		{
			Uri requestUri = BuildRequestUri(eventName, properties);
			using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUri);
			using HttpResponseMessage httpResponseMessage = _httpClient.SendAsync(request).GetAwaiter().GetResult();
			if (!httpResponseMessage.IsSuccessStatusCode)
			{
				Log.Debug("Aliyun WebTracking returned {StatusCode}.", (int)httpResponseMessage.StatusCode);
			}
		}
		catch (Exception exception)
		{
			Log.Debug(exception, "Failed to send event to Aliyun WebTracking.");
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (_ownsHttpClient)
		{
			_httpClient.Dispose();
		}
	}

	private Uri BuildRequestUri(string eventName, IReadOnlyDictionary<string, string> properties)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.Ordinal) { ["event_name"] = eventName };
		foreach (KeyValuePair<string, string> property in properties)
		{
			property.Deconstruct(out var key, out var value);
			string key2 = key;
			string value2 = value;
			dictionary[key2] = value2;
		}
		string text = string.Join("&", dictionary.Select((KeyValuePair<string, string> pair) => Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(pair.Value)));
		string leftPart = _endpoint.GetLeftPart(UriPartial.Path);
		string text2 = _endpoint.Query.TrimStart('?');
		string query = (string.IsNullOrEmpty(text2) ? text : (text2 + "&" + text));
		return new UriBuilder(leftPart)
		{
			Query = query
		}.Uri;
	}
}
