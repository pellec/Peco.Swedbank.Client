﻿using System;
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
			var transactions = (await _sut.GetTransactionsAsync(accountId)).ToArray();

			foreach (var transaction in transactions)
			{
				Console.WriteLine(transaction.ToString());
			}

			transactions.Length.Should().BeGreaterThan(0);
		}
	}
}