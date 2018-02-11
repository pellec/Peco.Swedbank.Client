using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;

namespace Peco.Swedbank.Client.IntegrationTests
{
	[TestFixture]
	public class SwedbankMobileBankIdClientTests
	{
		[SetUp]
		public void Setup()
		{
			_sut = new SwedbankMobileBankIdClient();
		}

		private SwedbankMobileBankIdClient _sut;

		[Test, Explicit]
		public async Task ThisTestIsJustForVerifyingThatStuffWorks()
		{
			var accountId = "xxxx-x,xxx xxx xxx-x";
			var result = (await _sut.GetTransactionsAsync(accountId));

			result.IsSuccess.Should().BeTrue();

			foreach (var transaction in result.Value)
			{
				Console.WriteLine(transaction.ToString());
			}

			result.Value.Count.Should().BeGreaterThan(0);
		}
	}
}