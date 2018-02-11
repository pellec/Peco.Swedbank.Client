# Swedbank.Client

This is a very basic client to the swedish bank Swedbank (swedbank.se), which under the hood uses the api from their homepage.

## Update February 2018

Swedbank updated their homepage so the html client is now obsolete. A new client 'SwedbankMobileBankIdJsonClient' is added which under the hood uses the same api as their homepage does.

## Update February 2016

Swedbank removed their support for using a password to login to your bank account. Therefor the `SwedbankInternetBankClient` is marked as obsolete (saved for sentimental reasons) and replaced by the `SwedbankMobileBankIdClient`. Unfortunately this means that reading the transactions no longer can be automated since you need sign in with your mobile bankid on your phone.

## How-to

In order to sign in with the `SwedbankMobileBankIdJsonClient` you need to have the [mobile bankid application](https://itunes.apple.com/se/app/bankid-sakerhetsapp/id433151512?l=en&mt=8) installed on your phone and ready to go.

When you execute `GetTransactionsAsync` you need to have the mobile bankid application started on your phone. You will then see in your phone that Swedbank wants you to authenticate. Do this and the client will check for a successful login. By default the client will check for a successful login five times and sleep four seconds between each try.
Here is a code sample of how to use it: 

	string civicNumber = "xxxxxxxx-xxxx; // specify your real civicnumber here"
	string accountId = "xxxx-x,xxx xxx xxx-x"; // specify your real accountid here
	var client = new SwedbankMobileBankIdJsonClient(civicNumber);
	var transactions = await client.GetTransactionsAsync(accountId);
	// Do stuff to your transactions.

Have fun!
