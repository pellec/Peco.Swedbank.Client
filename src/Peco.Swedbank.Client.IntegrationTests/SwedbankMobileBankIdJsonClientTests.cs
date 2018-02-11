using System;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Peco.Swedbank.Client.Helpers;

namespace Peco.Swedbank.Client.IntegrationTests
{
	[TestFixture]
	public class SwedbankMobileBankIdJsonClientTests
	{
		[SetUp]
		public void Setup()
		{
			_sut = new SwedbankMobileBankIdJsonClient("<civicnumber>", new SwedbankJsonTransactionBuilder(new TransactionDtoGenerateId()));
		}

		private ISwedbankClient _sut;

		[Test, Explicit]
		public async Task ThisTestIsJustForVerifyingThatStuffWorks()
		{
			var accountId = "xxxx-x,xxx xxx xxx-x";
			var result = await _sut.GetTransactionsAsync(accountId).ConfigureAwait(false);

			result.IsSuccess.Should().BeTrue();

			foreach (var transaction in result.Value)
			{
				Console.WriteLine(transaction.ToString());
			}

			result.Value.Count.Should().BeGreaterThan(0);
		}
	}
}