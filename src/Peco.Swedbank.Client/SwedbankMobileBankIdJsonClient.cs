using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Newtonsoft.Json;
using Peco.Swedbank.Client.Entities;
using Peco.Swedbank.Client.Helpers;
using static CSharpFunctionalExtensions.Result;

namespace Peco.Swedbank.Client
{
	public class SwedbankMobileBankIdJsonClient : ISwedbankClient
	{
		private readonly string _civicNumber;
		private readonly SwedbankJsonTransactionBuilder _transactionBuilder;
		private const string RootUri = "https://online.swedbank.se";
		private static readonly string RootApiUri = $"{RootUri}/TDE_DAP_Portal_REST_WEB/api";

		private static readonly string StartUri = $"{RootUri}/app/privat/login";
		private static readonly string IdentificationUri = $"{RootUri}/TDE_DAP_Portal_REST_WEB/api/v5/identification/";
		private static readonly string BankIdIdentificationUri = $"{RootUri}/TDE_DAP_Portal_REST_WEB/api/v5/identification/bankid/mobile";
		private static readonly string BankIdVerificationUri = $"{RootUri}/TDE_DAP_Portal_REST_WEB/api/v5/identification/bankid/mobile/verify";
		private static readonly string ProfileUri = $"{RootUri}/TDE_DAP_Portal_REST_WEB/api/v5/profile/";
		private static readonly string OverviewUri = $"{RootUri}/TDE_DAP_Portal_REST_WEB/api/v5/engagement/overview";

		private const int NbrOfTimesToWaitForBankIdLogin = 5;
		private readonly TimeSpan _nbrOfSecondsToWaitForBankIdVerification = TimeSpan.FromSeconds(4);

		private static HttpClient _client;
		private readonly CookieContainer _cookieContainer;

		public SwedbankMobileBankIdJsonClient(string civicNumber, SwedbankJsonTransactionBuilder transactionBuilder)
		{
			_civicNumber = civicNumber;
			_transactionBuilder = transactionBuilder;
			_cookieContainer = new CookieContainer();
			_client = new HttpClient(new HttpClientHandler { CookieContainer = _cookieContainer, AllowAutoRedirect = false });
		}

		public async Task<Result<IReadOnlyCollection<TransactionDto>>> GetTransactionsAsync(string accountId)
		{
			await Send(new HttpRequestMessage(HttpMethod.Get, StartUri)).ConfigureAwait(false);

			var session = new Session(_cookieContainer.GetCookieValue("dsid", new Uri(RootUri)), _client);

			var loginResult = await Login(session).ConfigureAwait(false);
			if(loginResult.IsFailure)
			{
				return Fail<IReadOnlyCollection<TransactionDto>>(loginResult.Error);
			}

			var profileResult = await StartProfileSession(session).ConfigureAwait(false);
			if(profileResult.IsFailure)
			{
				return Fail<IReadOnlyCollection<TransactionDto>>(profileResult.Error);
			}

			var overviewResponse = await session.Send(new HttpRequestMessage(HttpMethod.Get, OverviewUri)).ConfigureAwait(false);
			var account = JsonConvert.DeserializeObject<Dtos.Root>(await overviewResponse.Content.ReadAsStringAsync().ConfigureAwait(false))
				.TransactionAccounts
				.FirstOrDefault(x => x.FullyFormattedNumber.Equals(accountId, StringComparison.InvariantCultureIgnoreCase));

			if (account == null)
			{
				return Fail<IReadOnlyCollection<TransactionDto>>("Unable to find account");
			}

			var accountResponse = await session
				.Send(new HttpRequestMessage(HttpMethod.Get, $"{RootApiUri}{account.Links.Next.Uri}"))
				.ConfigureAwait(false);

			var content = await accountResponse.Content.ReadAsStringAsync().ConfigureAwait(false);

			return Ok<IReadOnlyCollection<TransactionDto>>(_transactionBuilder.Build(content).ToArray());
		}

		private static async Task<Result> StartProfileSession(Session session)
		{
			var profile = await SendProfileRequest(session).ConfigureAwait(false);

			var response = await session
				.Send(new HttpRequestMessage(HttpMethod.Post, new Uri($"{ProfileUri}{profile.Banks.First().PrivateProfile.Id}")))
				.ConfigureAwait(false);

			if (response.StatusCode != HttpStatusCode.Created)
			{
				return Fail("Unable to start profile session");
			}

			return Ok();
		}

		private static async Task<Dtos.Profile> SendProfileRequest(Session session)
		{
			var response = await session.Send(new HttpRequestMessage(HttpMethod.Get, new Uri(ProfileUri))).ConfigureAwait(false);
			return JsonConvert.DeserializeObject<Dtos.Profile>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
		}

		private async Task<Result> Login(Session session)
		{
			await session.Send(new HttpRequestMessage(HttpMethod.Get, IdentificationUri)).ConfigureAwait(false);

			var result = await WaitForBankIdVerification(session).ConfigureAwait(false);
			if (!result.IsFailure)
			{
				return result;
			}

			return Ok();
		}

		private async Task<Result> WaitForBankIdVerification(Session session)
		{
			var verificationRequest = new HttpRequestMessage(HttpMethod.Post, BankIdIdentificationUri)
			{
				Content = new StringContent(
					JsonConvert.SerializeObject(new { userId = _civicNumber, useEasyLogin = false, generateEasyLoginId = false }),
					Encoding.UTF8, "application/json")
			};

			await session.Send(verificationRequest).ConfigureAwait(false);

			await Task.Delay(_nbrOfSecondsToWaitForBankIdVerification).ConfigureAwait(false);

			for (var i = 0; i < NbrOfTimesToWaitForBankIdLogin; i++)
			{
				var res = await session.Send(new HttpRequestMessage(HttpMethod.Get, BankIdVerificationUri)).ConfigureAwait(false);
				var content = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
				var obj = JsonConvert.DeserializeObject<dynamic>(content);
				if (((string)obj.status).Equals("COMPLETE", StringComparison.InvariantCultureIgnoreCase))
				{
					return Ok();
				}
				await Task.Delay(_nbrOfSecondsToWaitForBankIdVerification).ConfigureAwait(false);
			}

			return Fail("Failed to verify bankid");
		}

		private static Task<HttpResponseMessage> Send(HttpRequestMessage request)
		{
			request.AddDefaultHeaders();
			return _client.SendAsync(request);
		}

		private class Session
		{
			private readonly HttpClient _client;
			private readonly string _sessionId;
			private readonly string _authorizationValue;
			private static readonly DateTime BeginningOfTime = new DateTime(1970, 1, 1);

			private const string SuperSecretKey = "B7dZHQcY78VRVz9l";

			public Session(string sessionId, HttpClient client)
			{
				_client = client;
				_sessionId = sessionId
					.Replace("=", "%3D")
					.Replace("/", "%2f")
					.Replace(":", "%3a")
					.Replace("+", "%2b");

				_authorizationValue = GetAuthorizationValue(DateTime.UtcNow);
			}

			private static string GetAuthorizationValue(DateTime now)
			{
				var time = Convert.ToInt64((now - BeginningOfTime).TotalMilliseconds);

				return Convert.ToBase64String(Encoding.UTF8.GetBytes($"{SuperSecretKey}:{time}"));
			}

			public Task<HttpResponseMessage> Send(HttpRequestMessage req)
			{
				req.RequestUri = new Uri(req.RequestUri + "?dsid=" + _sessionId);

				req.AddDefaultHeaders();

				if (req.Method == HttpMethod.Post)
				{
					req.Headers.TryAddWithoutValidation("Content-Type", "application/json");
				}

				req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

				if (!req.Headers.Contains("Authorization"))
				{
					req.Headers.Add("Authorization", _authorizationValue);
				}

				return _client.SendAsync(req);
			}
		}

		private class Dtos
		{
			public class Profile
			{
				public Bank[] Banks { get; set; }
			}

			public class Bank
			{
				public PrivateProfile PrivateProfile { get; set; }
			}

			public class PrivateProfile
			{
				public string Id { get; set; }
			}

			public class Root
			{
				public TransactionAccounts[] TransactionAccounts { get; set; }
			}

			public class TransactionAccounts
			{
				public string FullyFormattedNumber { get; set; }
				public Links Links { get; set; }
			}

			public class Links
			{
				public Next Next { get; set; }
			}

			public class Next
			{
				public string Uri { get; set; }
			}
		}
	}
}