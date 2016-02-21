using System.Collections.Generic;
using System.Threading.Tasks;
using Peco.Swedbank.Client.Entities;

namespace Peco.Swedbank.Client
{
	public interface ISwedbankClient
	{
		Task<IEnumerable<TransactionDto>> GetTransactionsAsync(string accountId);
	}
}