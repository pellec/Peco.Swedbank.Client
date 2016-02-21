using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using HtmlAgilityPack;
using Peco.Swedbank.Client.Entities;

namespace Peco.Swedbank.Client.Helpers
{
	public interface ITransactionBuilder
	{
		IEnumerable<TransactionDto> Build(string content);
	}

	public class SwedbankHtmlTransactionBuilder : ITransactionBuilder
	{
		private readonly IGenerateId<TransactionDto> _idGenerator;

		public SwedbankHtmlTransactionBuilder(IGenerateId<TransactionDto> idGenerator)
		{
			_idGenerator = idGenerator;
		}

		public IEnumerable<TransactionDto> Build(string content)
		{
			if (string.IsNullOrEmpty(content))
			{
				yield break;
			}

			var doc = new HtmlDocument();
			doc.LoadHtml(content);

			var nodes =
				doc.DocumentNode.SelectNodes("//h3[starts-with(., 'De senaste transaktionerna')]/following-sibling::tr").Skip(1);

			foreach (var node in nodes)
			{
				var dateText = node.ChildNodes.Skip(1).First().FirstChild.InnerHtml.Trim();

				DateTime date;
				if (!DateTime.TryParseExact(
					dateText, "yy-MM-dd", CultureInfo.GetCultureInfo("sv-SE"), DateTimeStyles.AdjustToUniversal, out date))
				{
					// If we don't have a date it is probably worth skipping this node
					continue;
				}

				string receiver = node.ChildNodes.Skip(7).First().ChildNodes.Skip(1).First().InnerHtml.Trim().Replace("&nbsp;", "");
				string amount = node.ChildNodes.Skip(11).First().FirstChild.InnerHtml.Trim().Replace(" ", "").Replace(",", ".");

				var t = new TransactionDto
				{
					Date = date.ToUniversalTime(),
					Receiver = WebUtility.HtmlDecode(receiver),
					Amount = Convert.ToInt32(double.Parse(amount, CultureInfo.InvariantCulture))
				};

				t.Id = _idGenerator.Generate(t);

				yield return t;
			}
		}
	}
}