using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Peco.Swedbank.Client.Entities;

namespace Peco.Swedbank.Client.Helpers
{
	public class SwedbankJsonTransactionBuilder : ITransactionBuilder
	{
		private readonly IGenerateId<TransactionDto> _idGenerator;

		public SwedbankJsonTransactionBuilder(IGenerateId<TransactionDto> idGenerator)
		{
			_idGenerator = idGenerator;
		}

		public IEnumerable<TransactionDto> Build(string content)
		{
			if (string.IsNullOrWhiteSpace(content))
			{
				yield break;
			}

			var root = JsonConvert.DeserializeObject<Root>(content);

			var transactions = root.Transactions
				.Select((x, i) => new TransactionDto
				{
					Receiver = x.Description,
					Date = DateTime.Parse(x.Date).ToUniversalTime(),
					Amount = Convert.ToInt32(decimal.Parse(x.Amount?.Replace(" ", "").Replace(",", ".") ?? "0",
						CultureInfo.InvariantCulture))
				})
				.ToArray();

			foreach (var transaction in transactions)
			{
				transaction.Id = _idGenerator.Generate(transaction);

				yield return transaction;
			}
		}

		private class Root
		{
			public Transaction[] Transactions { get; set; }
		}

		private class Transaction
		{
			public string Description { get; set; }
			public string Date { get; set; }
			public string Amount { get; set; }
		}
	}
}