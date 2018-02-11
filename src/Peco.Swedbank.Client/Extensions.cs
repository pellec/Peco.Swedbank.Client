using System;
using System.Net;
using System.Net.Http;

namespace Peco.Swedbank.Client
{
	public static class Extensions
	{
		public static void AddDefaultHeaders(this HttpRequestMessage req)
		{
			req.Headers.Connection.Add("keep-alive");
			req.Headers.AcceptLanguage.ParseAdd("sv");
			req.Headers.Accept.TryParseAdd("*/*");
			req.Headers.Host = "online.swedbank.se";
			req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/63.0.3239.108 Safari/537.36");
			req.Headers.Add("Origin", "https://online.swedbank.se");
		}

		public static string GetCookieValue(this CookieContainer cookieContainer, string name, Uri uri)
		{
			foreach (Cookie cookie in cookieContainer.GetCookies(uri))
			{
				if (cookie.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase))
				{
					return cookie.Value;
				}
			}

			return string.Empty;
		}
	}
}