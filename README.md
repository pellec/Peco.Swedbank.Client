# Swedbank.Client

This is a very basic client to the swedish bank Swedbank (swedbank.se). It can login to your bank account using the civicnumber and private code that you specify and read your latest transactions. You have to apply for a private code via their web page first though.

## How-to

Specify your civic number and private code when invoking the constructor for SwedbankInternetBankClient.cs and then specify your account id when calling the GetTransactionsAsync-method. Your account id at Swedbank will be in the format of 'xxxx-x,xxx xxx xxx-x'. Example:

		string civicNumer = "xxxxxxxxxx";
		string password = "xxxxxx";
		var client = new SwedbankInternetBankClient(civicNumer, password);
		string accountId = "xxxx-x,xxx xxx xxx-x";
		var transactions = await client.GetTransactionsAsync(accountId);
		// Do stuff to your transactions.

The account id can be found by logging in to internetbank.swedbank.se with your credentials. Then go to your accounts start page.

Currently it can only read the 20 latest transactions on your account.

Have fun!
