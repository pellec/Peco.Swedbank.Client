using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Peco.Swedbank.Client.Entities;
using Peco.Swedbank.Client.Helpers;

namespace Peco.Swedbank.Client
{
	public class SwedbankMobileBankIdClient : ISwedbankClient
	{
		private const string LandingPageUrl = "https://internetbank.swedbank.se/bviPrivat/privat?_new_flow_=false";
		private const string BaseUrl = "https://internetbank.swedbank.se/bviPrivat/privat";
		private const string IdpUrl = "https://internetbank.swedbank.se/idp/portal";

		private const string FirstLoginActionUrl = "https://internetbank.swedbank.se/idp/portal/identifieringidp/idp/dap1/ver=2.0/action/rparam=execution=e1s1";

		private const string SecondLoginRequestUrl = "https://internetbank.swedbank.se/idp/portal/identifieringidp/idp/dap1/ver=2.0/rparam=execution=e1s2";

		private const string SecondLoginActionUrl = "https://internetbank.swedbank.se/idp/portal/identifieringidp/idp/dap1/ver=2.0/action/rparam=execution=e1s2";

		private const string ThirdLoginRequestUrl = "https://internetbank.swedbank.se/idp/portal/identifieringidp/idp/dap1/ver=2.0/rparam=execution=e1s3";

		private const string ThirdLoginActionUrl = "https://internetbank.swedbank.se/idp/portal/identifieringidp/idp/dap1/ver=2.0/action/rparam=execution=e1s3";

		private const string FourthLoginRequestUrl = "https://internetbank.swedbank.se/idp/portal/identifieringidp/idp/dap1/ver=2.0/rparam=execution=e1s4";

		private const string WaitForLoginPollingUrl = "https://internetbank.swedbank.se/idp/portal/identifieringidp/busresponsecheck/main-dapPortalWindowId";

		private readonly int _nbrOfSecondsToWaitBetweenLoginChecks;

		private readonly int _nbrOfTimesToCheckIfLoginIsDone;
		private readonly ITransactionBuilder _transactionBuilder;

		private HttpClient _client;

		public SwedbankMobileBankIdClient() : this(5, 6, new SwedbankHtmlTransactionBuilder(new TransactionDtoGenerateId()))
		{
		}

		public SwedbankMobileBankIdClient(int nbrOfTimesToCheckIfLoginIsDone, int nbrOfSecondsToWaitBetweenLoginChecks,
			ITransactionBuilder transactionBuilder)
		{
			_nbrOfTimesToCheckIfLoginIsDone = nbrOfTimesToCheckIfLoginIsDone;
			_nbrOfSecondsToWaitBetweenLoginChecks = nbrOfSecondsToWaitBetweenLoginChecks;
			_transactionBuilder = transactionBuilder;
		}

		public async Task<IEnumerable<TransactionDto>> GetTransactionsAsync(string accountId)
		{
			_client = CreateClient();

			var loginResponse = await Login();
			var landingPageContent = await loginResponse.Content.ReadAsStringAsync();

			string url;
			if (!TryFindAccountUrl(accountId, landingPageContent, out url))
			{
				return Enumerable.Empty<TransactionDto>();
			}

			var res = await Send(new HttpRequestMessage(HttpMethod.Get, string.Concat(BaseUrl, url)));

			if (!res.IsSuccessStatusCode)
			{
				return Enumerable.Empty<TransactionDto>();
			}

			return _transactionBuilder.Build(await res.Content.ReadAsStringAsync());
		}

		private static bool TryFindAccountUrl(string accountId, string html, out string url)
		{
			url = null;
			var doc = new HtmlDocument();
			doc.LoadHtml(html);

			var td = doc.DocumentNode.SelectSingleNode("//td[starts-with(., '" + accountId.Trim() + "')]");

			var a = td?.ParentNode.SelectSingleNode("td//a");
			if (a == null)
			{
				return false;
			}

			url = a.Attributes["href"].Value.Replace("amp;", "");

			return true;
		}

		private async Task<HttpResponseMessage> Login()
		{
			var context = new LoginContext();

			await SendStartRequest(context);
			await SendIdpRequest(context);
			await SendFirstActionRequest(context);
			await SendSecondActionRequest();
			await SendThirdActionRequest();
			await SendFourthRequest(context);

			return await Send(new HttpRequestMessage(HttpMethod.Post, LandingPageUrl)
			{
				Content = new FormUrlEncodedContent(new[]
				{
					new KeyValuePair<string, string>("dapPortalWindowId", "dap1"),
					new KeyValuePair<string, string>("locale", "sv_SE"),
					new KeyValuePair<string, string>("IntId1", "dap1"),
					new KeyValuePair<string, string>("authid", context.LoginAuthId)
				})
			});
		}

		private async Task SendFourthRequest(LoginContext loginContext)
		{
			var req = new HttpRequestMessage(HttpMethod.Get, FourthLoginRequestUrl);

			req.Headers.Referrer = new Uri(ThirdLoginActionUrl);

			var res = await Send(req);

			var content = await res.Content.ReadAsStringAsync();

			var doc = new HtmlDocument();
			doc.LoadHtml(content);

			var authId = doc.DocumentNode.SelectSingleNode("//input[@name='authid']");

			if (authId != null)
			{
				loginContext.LoginAuthId = doc.DocumentNode.SelectSingleNode("//input[@name='authid']")
					.GetAttributeValue("value", "");
			}
		}

		private async Task SendThirdActionRequest()
		{
			var res = await Send(new HttpRequestMessage(HttpMethod.Get, ThirdLoginRequestUrl));

			var content = await res.Content.ReadAsStringAsync();

			var doc = new HtmlDocument();
			doc.LoadHtml(content);

			var viewState = doc.DocumentNode.SelectSingleNode("//input[@name='javax.faces.ViewState']")
				.GetAttributeValue("value", "");

			var result = await WaitForMobileBankIdLogin();
			if (!result)
			{
				throw new Exception("Could not read successfull status from mobile login in");
			}

			await Send(new HttpRequestMessage(HttpMethod.Post, ThirdLoginActionUrl)
			{
				Content = new FormUrlEncodedContent(new[]
				{
					new KeyValuePair<string, string>("execution", "e1s3"),
					new KeyValuePair<string, string>("form:returnCode", "1"),
					new KeyValuePair<string, string>("form:refId", ""),
					new KeyValuePair<string, string>("form:fortsett_knapp", "Fortsätt"),
					new KeyValuePair<string, string>("form_SUBMIT", "1"),
					new KeyValuePair<string, string>("javax.faces.ViewState", viewState)
				})
			});
		}

		private async Task<bool> WaitForMobileBankIdLogin()
		{
			var regex = new Regex(@"\[responsechecker.status\](?<status>\d+)");

			for (var i = 0; i < _nbrOfTimesToCheckIfLoginIsDone; i++)
			{
				var req = new HttpRequestMessage(HttpMethod.Get, WaitForLoginPollingUrl);
				req.Headers.Add("X-Requested-With", "XMLHttpRequest");
				req.Headers.Referrer = new Uri(ThirdLoginRequestUrl);

				var res = await Send(req);
				var content = await res.Content.ReadAsStringAsync();

				var status = regex.Match(content).Groups["status"];

				if (status.Success && status.Value == "1")
				{
					return true;
				}

				await Task.Delay(TimeSpan.FromSeconds(_nbrOfSecondsToWaitBetweenLoginChecks));
			}

			return false;
		}


		private async Task SendSecondActionRequest()
		{
			var res = await Send(new HttpRequestMessage(HttpMethod.Get, SecondLoginRequestUrl));

			var content = await res.Content.ReadAsStringAsync();

			var doc = new HtmlDocument();
			doc.LoadHtml(content);

			var viewState = doc.DocumentNode.SelectSingleNode("//input[@name='javax.faces.ViewState']")
				.GetAttributeValue("value", "");

			var req = new HttpRequestMessage(HttpMethod.Post, SecondLoginActionUrl)
			{
				Content = new FormUrlEncodedContent(new[]
				{
					new KeyValuePair<string, string>("execution", "e1s2"),
					new KeyValuePair<string, string>("auth:kundnummer", "8308253957"),
					new KeyValuePair<string, string>("auth:metod_2", "MOBILBID"),
					new KeyValuePair<string, string>("auth:efield", "1"),
					new KeyValuePair<string, string>("auth:fortsett_knapp", "Fortsätt"),
					new KeyValuePair<string, string>("auth_SUBMIT", "1"),
					new KeyValuePair<string, string>("javax.faces.ViewState", viewState)
				})
			};

			req.Headers.Referrer = new Uri(SecondLoginRequestUrl);

			await Send(req);
		}


		private async Task SendFirstActionRequest(LoginContext loginContext)
		{
			await Send(new HttpRequestMessage(HttpMethod.Post, FirstLoginActionUrl)
			{
				Content = new FormUrlEncodedContent(new[]
				{
					new KeyValuePair<string, string>("execution", "e1s1"),
					new KeyValuePair<string, string>("dapPortalWindowId", "dap1"),
					new KeyValuePair<string, string>("locale", "sv_SE"),
					new KeyValuePair<string, string>("IntId", "dap1"),
					new KeyValuePair<string, string>("authid", ""),
					new KeyValuePair<string, string>("form1:fortsett_knapp", "klicka"),
					new KeyValuePair<string, string>("form1_SUBMIT", "1"),
					new KeyValuePair<string, string>("javax.faces.ViewState", loginContext.ViewStateForSecondRequest)
				})
			});
		}

		private async Task SendIdpRequest(LoginContext loginContext)
		{
			var res = await Send(new HttpRequestMessage(HttpMethod.Post, IdpUrl)
			{
				Content = new FormUrlEncodedContent(new[]
				{
					new KeyValuePair<string, string>("authid", loginContext.StartAuthId)
				})
			});

			var content = await res.Content.ReadAsStringAsync();

			var doc = new HtmlDocument();
			doc.LoadHtml(content);

			loginContext.ViewStateForSecondRequest =
				doc.DocumentNode.SelectSingleNode("//input[@name='javax.faces.ViewState']").GetAttributeValue("value", "");
		}

		private async Task SendStartRequest(LoginContext loginContext)
		{
			var res = await Send(new HttpRequestMessage(HttpMethod.Get, BaseUrl + "?ns=1"));

			var content = await res.Content.ReadAsStringAsync();

			var doc = new HtmlDocument();
			doc.LoadHtml(content);

			loginContext.StartAuthId = doc.DocumentNode.SelectSingleNode("//input[@name='authid']")
				.GetAttributeValue("value", "");
		}

		private async Task<HttpResponseMessage> Send(HttpRequestMessage request)
		{
			AddDefaultHeaders(request);
			return await _client.SendAsync(request);
		}

		private static void AddDefaultHeaders(HttpRequestMessage req)
		{
			req.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.8,da;q=0.6,sv;q=0.4");
			req.Headers.Add("Upgrade-Insecure-Requests", "1");
			req.Headers.Accept.TryParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
			req.Headers.Host = "internetbank.swedbank.se";
			req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/57.0.2987.133 Safari/537.36");
			req.Headers.Add("Origin", "https://internetbank.swedbank.se");
		}

		private static HttpClient CreateClient()
		{
			return new HttpClient(new HttpClientHandler {CookieContainer = new CookieContainer(), AllowAutoRedirect = false});
		}

		private class LoginContext
		{
			public string StartAuthId { get; set; }
			public string ViewStateForSecondRequest { get; set; }
			public string LoginAuthId { get; set; }
		}
	}
}