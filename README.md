# Findus Web App
[![Builtd and Deployed as ASP.Net Core app to Azure Web App](https://github.com/CompassMotionAB/Findus-WebApp/actions/workflows/azure-webapps-dotnet-core.yml/badge.svg)](https://github.com/CompassMotionAB/Findus-WebApp/actions/workflows/azure-webapps-dotnet-core.yml)

## Verifikat 
Skapar och Verifierar Fortnox faktureringar av beställning hämtad från
WooCommerce.

Proforma faktura:

-   Får VAT för Standard eller Reducerad(livsmedel) skatt från WooCommerce.  
    ![](FindusWebApp/wwwroot/images/table_header.png)
    ![](FindusWebApp/wwwroot/images/stripe_proforma.png)
-   Hämtar Riksbankens Växelkurs för EUR-SEK i dagsavslut för
    betalningsdatumet.  
    ![](/FindusWebApp/wwwroot/images/currency_rate.png)  
-   När betalningen har gjorts med Stripe så tar vi deras växelkurs som
    är kalkylerad vid betalningen.  
    ![](FindusWebApp/wwwroot/images/stripe_currency_rate.png)
-   Automatisk hantering av Stripe/PayPal avgift  
    ![](FindusWebApp/wwwroot/images/table_header.png)
    ![](FindusWebApp/wwwroot/images/stripe_fee.png)

Faktura som speglar ordern från WooCommerce:

-   Kundinformation, Adress, Email, m.m.
-   Produkt information i ordern, SKU & Pris
