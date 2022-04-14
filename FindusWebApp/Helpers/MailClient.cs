using MailKit.Net.Smtp;
using MimeKit;

namespace FindusWebApp.Helpers
{
    public static class MailClient
    {
        public static void SendMessage(MimeMessage message)
        {
            using var client = new SmtpClient();
            client.Connect("smtp.gmail.com", 587, false);
            //client.Connect("smtp.gmail.com", 465, true);
            try
            {
                client.Authenticate("partners@gamerbulk.com", "poiy brby jppa xzve");
            }
            catch (System.Exception)
            {
                client.Authenticate("partners@gamerbulk.com", "poiybrbyjppaxzve");
            }

            client.Send(message);
            client.Disconnect(true);
        }
        public static void SendMessage()
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Findus Web App", "partners@gamerbulk.com"));
            message.To.Add(new MailboxAddress("Joaqim", "mail@joaqim.xyz"));
            message.Subject = "Test Message";

            message.Body = new TextPart("plain")
            {
                Text =
                    @"This is a test,
-- Findus"
            };

            using var client = new SmtpClient();
            //client.Connect("smtp.gmail.com", 587, false);
            client.Connect("smtp.gmail.com", 465, true);
            //client.Connect("webmail.beebyte.se", 587, false);
            const string  login = "partners@gamerbulk.com";
            //const string login = "findus";
            try
            {
                client.Authenticate(login, "poiy brby jppa xzve");
            }
            catch (System.Exception)
            {
                client.Authenticate(login, "poiybrbyjppaxzve");
            }

            //client.Connect("mail.gmx.net", 587, false);
            //client.Authenticate("1649321657.447369.45605.nullmailer@gmx.com", "planGbhackER123");

            client.Send(message);
            client.Disconnect(true);
        }
    }
}
