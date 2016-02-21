using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Peco.Swedbank.Client.Entities;
using Peco.Swedbank.Client.Helpers;

namespace Peco.Swedbank.Client
{
	[Obsolete("Swedbank removed the support for using a personal password in February 2016")]
    public class SwedbankInternetBankClient : ISwedbankClient
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
        private const string FourthLoginActionUrl = "https://internetbank.swedbank.se/idp/portal/identifieringidp/idp/dap1/ver=2.0/action/rparam=execution=e1s4";
        private const string FifthLoginRequestUrl = "https://internetbank.swedbank.se/idp/portal/identifieringidp/idp/dap1/ver=2.0/rparam=execution=e1s5";

        private readonly string _civicNumber;
        private readonly string _password;

        private HttpClient _client;
        private readonly ITransactionBuilder _transactionBuilder;

        private class LoginContext
        {
            public string StartAuthId { get; set; }
            public string ViewStateForSecondRequest { get; set; }
            public string LoginAuthId { get; set; }
        }

        public SwedbankInternetBankClient(string civicNumber, string password)
            : this(civicNumber, password,
                new SwedbankHtmlTransactionBuilder(new TransactionDtoGenerateId()))
        {
        }

        public SwedbankInternetBankClient(string civicNumber, string password, ITransactionBuilder transactionBuilder)
        {
            _civicNumber = civicNumber;
            _password = password;
            _transactionBuilder = transactionBuilder;
        }

        private void CreateClient()
        {
            _client = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false });
        }

        public async Task<IEnumerable<TransactionDto>> GetTransactionsAsync(string accountId)
        {
            CreateClient();

            if (string.IsNullOrEmpty(accountId))
            {
                return Enumerable.Empty<TransactionDto>();
            }

            accountId = accountId.Trim();

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
            if (td == null)
            {
                return false;
            }

            var a = td.ParentNode.SelectSingleNode("td//a");
            if (a == null)
            {
                return false;
            }

            url = a.Attributes["href"].Value.Replace("amp;", "");

            return true;
        }

        private async Task<HttpResponseMessage> Send(HttpRequestMessage request)
        {
            AddDefaultHeaders(request);
            return await _client.SendAsync(request);
        }

        private async Task<HttpResponseMessage> Login()
        {
            var context = new LoginContext();

            await SendStartRequest(context);
            await SendIdpRequest(context);
            await SendFirstActionRequest(context);
            await SendSecondActionRequest();
            await SendThirdActionRequest();
            await SendFourthRequest();
            await SendFifthRequest(context);

            var req = new HttpRequestMessage(HttpMethod.Post, LandingPageUrl)
            {
                Content = new FormUrlEncodedContent(new[]
				{
					new KeyValuePair<string, string>("dapPortalWindowId", "dap1"),
					new KeyValuePair<string, string>("locale", "sv_SE"),
					new KeyValuePair<string, string>("IntId1", "dap1"),
					new KeyValuePair<string, string>("authid", context.LoginAuthId)
				})
            };

            return await Send(req);
        }

        private async Task SendFifthRequest(LoginContext context)
        {
            var res = await Send(new HttpRequestMessage(HttpMethod.Get, FifthLoginRequestUrl));

            string content = await res.Content.ReadAsStringAsync();

            var doc = new HtmlDocument();
            doc.LoadHtml(content);

            var authId = doc.DocumentNode.SelectSingleNode("//input[@name='authid']");

            if (authId != null)
            {
                context.LoginAuthId =
                    doc.DocumentNode.SelectSingleNode("//input[@name='authid']").GetAttributeValue("value", "");
            }
        }

        private async Task SendFourthRequest()
        {
            var fourthResponse = await Send(new HttpRequestMessage(HttpMethod.Get, FourthLoginRequestUrl));

            var content = await fourthResponse.Content.ReadAsStringAsync();

            var doc = new HtmlDocument();
            doc.LoadHtml(content);

            var viewState = doc.DocumentNode.SelectSingleNode("//input[@name='javax.faces.ViewState']").GetAttributeValue("value", "");

            var req = new HttpRequestMessage(HttpMethod.Post, FourthLoginActionUrl)
            {
                Content = new FormUrlEncodedContent(new[]
				{
					new KeyValuePair<string, string>("execution", "e1s4"),
					new KeyValuePair<string, string>("form:pinkod", _password),
					new KeyValuePair<string, string>("form:efield", "1"),
					new KeyValuePair<string, string>("form:fortsett_knapp", "Fortsätt"),
					new KeyValuePair<string, string>("form_SUBMIT", "1"),
					new KeyValuePair<string, string>("javax.faces.ViewState", viewState)
				})
            };

            var res = await Send(req);

            if (!res.Headers.Location.ToString().EndsWith("e1s5"))
            {
                throw new Exception("Location not ending with e1s5 after step e1s4");
            }
        }

        private async Task SendThirdActionRequest()
        {
            var thirdResponse = await Send(new HttpRequestMessage(HttpMethod.Get, ThirdLoginRequestUrl));

            var content = await thirdResponse.Content.ReadAsStringAsync();

            var doc = new HtmlDocument();
            doc.LoadHtml(content);

            var viewState = doc.DocumentNode.SelectSingleNode("//input[@name='javax.faces.ViewState']").GetAttributeValue("value", "");

            var req = new HttpRequestMessage(HttpMethod.Post, ThirdLoginActionUrl)
            {
                Content = new FormUrlEncodedContent(new[]
				{
					new KeyValuePair<string, string>("execution", "e1s3"),
					new KeyValuePair<string, string>("auth:kundnummer", _civicNumber),
					new KeyValuePair<string, string>("auth:metod_2", "PIN6"),
					new KeyValuePair<string, string>("auth:efield", "1"),
					new KeyValuePair<string, string>("auth:fortsett_knapp", "Fortsätt"),
					new KeyValuePair<string, string>("auth_SUBMIT", "1"),
					new KeyValuePair<string, string>("javax.faces.ViewState", viewState)
				})
            };

            await Send(req);
        }

        private async Task SendSecondActionRequest()
        {
            var paramRes = await Send(new HttpRequestMessage(HttpMethod.Get, SecondLoginRequestUrl));

            var content = await paramRes.Content.ReadAsStringAsync();

            var doc = new HtmlDocument();
            doc.LoadHtml(content);

            var viewState = doc.DocumentNode.SelectSingleNode("//input[@name='javax.faces.ViewState']").GetAttributeValue("value", "");

            var req = new HttpRequestMessage(HttpMethod.Post, SecondLoginActionUrl)
            {
                Content = new FormUrlEncodedContent(new[]
				{
					new KeyValuePair<string, string>("dummy_form:dummy_button", "Button"),
					new KeyValuePair<string, string>("dummy_form_SUBMIT", "1"),
					new KeyValuePair<string, string>("javax.faces.ViewState", viewState)
				})
            };

            var res = await Send(req);

            if (!res.Headers.Location.ToString().EndsWith("e1s3"))
            {
                throw new Exception("Location not ending with e1s3 after step e1s2");
            }
        }

        private async Task SendFirstActionRequest(LoginContext loginContext)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, FirstLoginActionUrl)
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
            };

            await Send(req);
        }

        private async Task SendIdpRequest(LoginContext loginContext)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, IdpUrl)
            {
                Content = new FormUrlEncodedContent(new[]
				{
					new KeyValuePair<string, string>("authid", loginContext.StartAuthId)
				})
            };

            var res = await Send(req);

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

            loginContext.StartAuthId = doc.DocumentNode.SelectSingleNode("//input[@name='authid']").GetAttributeValue("value", "");
        }

        private static void AddDefaultHeaders(HttpRequestMessage req)
        {
            req.Headers.Accept.TryParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            req.Headers.Host = "internetbank.swedbank.se";
            req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 6.3; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/38.0.2125.111 Safari/537.36");
            req.Headers.Add("Origin", "https://internetbank.swedbank.se");
        }
    }

}