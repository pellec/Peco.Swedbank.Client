using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;

namespace Peco.Swedbank.Client.IntegrationTests
{
    [TestFixture]
    public class SwedbankClientTests
    {
        private SwedbankClient _sut;

        [SetUp]
        public void Setup()
        {
            _sut = new SwedbankClient();
        }

		[Test, Explicit]
        public async Task ThisTestIsJustForVerifyingThatStuffWorks()
        {
            var transactions = await _sut.GetTransactionsAsync();

            foreach (var transaction in transactions)
            {
                Console.WriteLine(transaction.ToString());
            }

            transactions.Count().Should().BeGreaterThan(0);
        }
    }
}
