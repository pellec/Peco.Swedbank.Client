using System;

namespace Peco.Swedbank.Client.Entities
{
    public class TransactionDto
    {
        public DateTime Date { get; set; }
        public int Amount { get; set; }
        public string Receiver { get; set; }
	    public string Id { get; set; }

	    public override string ToString()
	    {
		    return string.Format("Date: {0}, Amount: {1}, Receiver: {2}, Id: {3}", Date, Amount, Receiver, Id);
	    }
    }
}