using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Peco.Swedbank.Client.Entities;
using Peco.Swedbank.Client.Helpers;

namespace Peco.Swedbank.Client
{
    public class SwedbankClient : ISwedbankClient
    {
	    private readonly SwedbankResponseReader _reader;
	    private readonly HttpClientHandler _clientHandler;

	    private readonly string _civicNumber;
	    private readonly string _password;
	    private readonly string _loginUrl;
	    private readonly string _loginNextUrl;
	    private readonly string _accountUrlFormat;
	    private readonly int _accountId;

	    public SwedbankClient()
            : this(new AppSettings(), new SwedbankResponseReader())
        {
        }

        public SwedbankClient(AppSettings settings, SwedbankResponseReader reader)
        {
	        _reader = reader;

	        _civicNumber = settings.Get("SwedbankCivicnumber");
			_password = settings.Get("SwedbankPassword");
			_loginUrl = settings.Get("SwedbankLoginUrl");
			_loginNextUrl = settings.Get("SwedbankLoginNextUrl");
			_accountUrlFormat = settings.Get("SwedbankAccountUrlFormat");
	        _accountId = int.Parse(settings.Get("SwedbankAccountId"));

	        _clientHandler = new HttpClientHandler { CookieContainer = new CookieContainer() };
        }

        public async Task<IEnumerable<TransactionDto>> GetTransactionsAsync()
        {
	        await Login();

	        var response = await GetAccountTransactionsAsync(_accountId);

	        return await _reader.ReadTransactions(await response.Content.ReadAsStringAsync());
        }

	    private Task<HttpResponseMessage> GetAccountTransactionsAsync(int accountId)
	    {
		    return Send(new HttpRequestMessage(HttpMethod.Get, string.Format(_accountUrlFormat, accountId)));
	    }

	    private async Task<HttpResponseMessage> Login()
        {
			var firstResponse = await ExecuteFirstLoginRequest();
			var secondResponse = await ExecuteSecondLoginStep(firstResponse);
			return await ExecuteThirdLoginStep(secondResponse);
        }

        private async Task<HttpResponseMessage> ExecuteFirstLoginRequest()
        {
            var req = new HttpRequestMessage(HttpMethod.Get, _loginUrl);
            req.Headers.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 6.2; WOW64) AppleWebKit/537.31 (KHTML, like Gecko) Chrome/26.0.1410.64 Safari/537.31");

            return await Send(req);
        }

        private async Task<HttpResponseMessage> ExecuteSecondLoginStep(HttpResponseMessage response)
        {
	        return await ExecuteSecondLoginRequest(SwedbankResponseReader.ReadToken(await response.Content.ReadAsStringAsync()));
        }

        private async Task<HttpResponseMessage> ExecuteSecondLoginRequest(string firstToken)
        {
	        var request = new HttpRequestMessage(HttpMethod.Post,
		        _loginNextUrl);

	        var data = new FormUrlEncodedContent(
                new[]
                    {
                        new KeyValuePair<string, string>("_csrf_token", firstToken),
                        new KeyValuePair<string, string>("auth-method", "code"),
                        new KeyValuePair<string, string>("xyz", _civicNumber),
                        new KeyValuePair<string, string>("busJavascriptSupported", "true")
                    });

            request.Content = data;

            return await Send(request);
        }

        private async Task<HttpResponseMessage> ExecuteThirdLoginStep(HttpResponseMessage response)
        {
	        return await ExecuteThirdLoginRequest(SwedbankResponseReader.ReadToken(await response.Content.ReadAsStringAsync()));
        }

        private async Task<HttpResponseMessage> ExecuteThirdLoginRequest(string secondToken)
        {
	        var request = new HttpRequestMessage(HttpMethod.Post, _loginUrl) {
		        Content = new FormUrlEncodedContent(new[] {
			        new KeyValuePair<string, string>("_csrf_token", secondToken),
			        new KeyValuePair<string, string>("zyx", _password)
		        })
	        };

	        return await Send(request);
        }

		private Task<HttpResponseMessage> Send(HttpRequestMessage request)
		{
			return new HttpClient(_clientHandler).SendAsync(request);
		}
    }
}