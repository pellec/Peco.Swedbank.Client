# Swedbank.Client

This is a very basic client to the swedish bank Swedbank (swedbank.se). It can login to your bank account using the civicnumber and private code that you specify.
You have to apply for a private code via their web page first though.

There are some app settings that needs to be supplied for it to work.

## How to configure

There is a test project with an app.config where you need to type in the following settings:

	<add key="SwedbankCivicNumber" value="XXXXXXXXXX"/>
	<add key="SwedbankPassword" value="XXXXXX"/>
	<add key="SwedbankAccountId" value="0"/>

The account id can be found by logging in to mobilbank.swedbank.se with your credentials. Then go to your accounts landing page and hover over the different accounts with your mouse.
The account id will show up as a query parameter (id) on the accounts url.

Currently it can only read the 20 latest transactions on your account :I

Have fun!