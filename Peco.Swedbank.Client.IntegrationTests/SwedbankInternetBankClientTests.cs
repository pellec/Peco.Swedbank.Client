using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;

namespace Peco.Swedbank.Client.IntegrationTests
{
    [TestFixture]
	public class SwedbankInternetBankClientTests
    {
		private SwedbankInternetBankClient _sut;

        [SetUp]
        public void Setup()
        {
	        string civicNumer = "xxxxxxxxxx";
	        string password = "xxxxxx";
			_sut = new SwedbankInternetBankClient(civicNumer, password);
        }

		[Test, Explicit]
        public async Task ThisTestIsJustForVerifyingThatStuffWorks()
		{
			string accountId = "xxxx-x,xxx xxx xxx-x";
			var transactions = (await _sut.GetTransactionsAsync(accountId)).ToArray();

            foreach (var transaction in transactions)
            {
                Console.WriteLine(transaction.ToString());
            }

            transactions.Count().Should().BeGreaterThan(0);
        }
    }
}
