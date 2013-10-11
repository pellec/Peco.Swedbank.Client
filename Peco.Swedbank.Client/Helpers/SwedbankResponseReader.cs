using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Peco.Swedbank.Client.Entities;

namespace Peco.Swedbank.Client.Helpers
{
    public class SwedbankResponseReader
    {
	    private readonly IGenerateId<TransactionDto> _idGenerator;

	    public SwedbankResponseReader() : this(new TransactionDtoGenerateId())
	    {
	    }

	    public SwedbankResponseReader(IGenerateId<TransactionDto> idGenerator)
	    {
		    _idGenerator = idGenerator;
	    }

	    public Task<IEnumerable<TransactionDto>> ReadTransactions(string html)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

		    var transactionNodes = doc.DocumentNode.SelectNodes(
			    "/html/body/div[@id='content']/div[@class='list-wrapper mb-list nowrap-list']/dl[@class='list-content']/dd");

            var tcs = new TaskCompletionSource<IEnumerable<TransactionDto>>();
            tcs.SetResult(transactionNodes.Select(BuildTransaction));
            return tcs.Task;
        }

        private TransactionDto BuildTransaction(HtmlNode node)
        {
            DateTime date;
            if (!DateTime.TryParseExact(
                node.Descendants("span").First(span => span.GetAttributeValue("class", "") == "date").
                    InnerHtml.Trim(), "yy-MM-dd", CultureInfo.GetCultureInfo("sv-SE"), DateTimeStyles.None, out date))
            {
                date = DateTime.Now;
            }

            var tran = new TransactionDto
                       {
                           Date = date,
                           Receiver = WebUtility.HtmlDecode(
                               node.Descendants("span").First(span => span.GetAttributeValue("class", "") == "receiver")
                               .InnerHtml.Trim()),
                           Amount =
                               int.Parse(
                                   node.Descendants("span").First(
                                       span => span.GetAttributeValue("class", "") == "amount").InnerHtml.Replace(" ", ""))
                       };

	        tran.Id = _idGenerator.Generate(tran);

	        return tran;
        }

        public static string ReadToken(string html)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            return doc.DocumentNode.SelectSingleNode("//input[@name='_csrf_token']").GetAttributeValue("value", "");
        }
    }
}