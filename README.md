# Findus Web App MVC5
DotNet Core MVC5 Sample app using .NET Standard SDK

This example of using Fortnox API with OAuth 2.0 Authentication in .Net Core(C#) MVC5 was adapted from Intuit Sample App of their integration which can be found [here](https://github.com/IntuitDeveloper/OAuth2_DotnetCore_MVC5_SampleApp).

Before you begin, you should be aware of the steps to integrate Fortnox API as a developer.
[Here](https://apps.fortnox.se/integration-registration-form/start) is a useful guide.
You should have a functional App in your Developer portal with Client-Id and Client-Secret.

## PreRequisites

1. Visual Studio 2019 or above, Visual Studio Code
2. Microsoft.Net.Compilers 2.10.0
3. .Net Core 3.1

## Configuring your app
All configuration for this app is located in [appsettings.sample.json](https://github.com/CompassMotionAB/fortnox-api-example-mvc5/raw/main/OAuth2_CoreMVC_Sample/appsettings.sample.json).
Rename to appsettings.json and change:
1. ClientId
2. ClientSecret
and optionally:
1. CallbackPath - Fortnox OAuth 2.0 API entry after authorization, Should be "/Connect/Index"
2. DBConnectionString - Database Source database [connection string](https://docs.microsoft.com/en-us/dotnet/api/system.data.sqlclient.sqlconnection.connectionstring)
3. Fortnox Base Url - "https://apps.fortnox.se"
4. Fortnox Authentication Endpoint - "/oauth-v1/auth"
5. Fortnox Token Endpoint - "/oauth-v1/token"
6. Fortnox [Scopes](https://developer.fortnox.se/general/scopes)
  Valid formats are case-insensitive string arrays, i.e:
  
   `"Scopes": ["companyinformation", "invoices", "customers", "article"]`

### DBConnectionString
This sample app uses a SQLite database to store the tokens(AccessToken and RefreshToken) used for doing our API calls and also update the token with new tokens when the token expires.
This database is created for you the first time your run the sample.

### Connect To Fortnox 
This flow goes through authorization flow where Fortnox user logs in and authorizes your app. At the end of this process, the app will end up with tokens and if you are a first time user it will create new tokens in the database and if you a recurring user and if your tokens are expired then it will update the database.

### Fortnox API request
Access tokens from Connect to Fortnox flow are used to make a Customer and Invoice request which allows to create a customer and invoice in your company. If any tokens are expired, then it refresh those tokens based on the refresh token.
