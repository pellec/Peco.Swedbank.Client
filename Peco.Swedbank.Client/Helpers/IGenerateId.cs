using System.Security.Cryptography;
using System.Text;
using System.Web;
using Peco.Swedbank.Client.Entities;

namespace Peco.Swedbank.Client.Helpers
{
	public interface IGenerateId<in T> where T : class
	{
		string Generate(T value);
	}

	public class TransactionDtoGenerateId : BaseGenerateId, IGenerateId<TransactionDto>
	{
		public string Generate(TransactionDto value)
		{
			return Generate(string.Concat(value.Date.ToShortDateString(), value.Receiver, value.Amount));
		}
	}

	public abstract class BaseGenerateId
	{
		protected static string Generate(string s)
		{
			using (var crypto =
				new MD5CryptoServiceProvider())
			{
				return HttpServerUtility.UrlTokenEncode(crypto.ComputeHash(Encoding.UTF8.GetBytes(s)));
			}
		}
	}
}