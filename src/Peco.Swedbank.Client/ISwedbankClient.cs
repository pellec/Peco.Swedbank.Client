using System.Collections.Generic;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Peco.Swedbank.Client.Entities;

namespace Peco.Swedbank.Client
{
	public interface ISwedbankClient
	{
		Task<Result<IReadOnlyCollection<TransactionDto>>> GetTransactionsAsync(string accountId);
	}
}