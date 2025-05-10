// Services/EmailService.cs
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace JakeScerriPFTC_Assignment.Services
{
    public class EmailService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _domain;
        private readonly string _from;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _logger = logger;
            
            _apiKey = configuration["Mailgun:ApiKey"] ?? "";
            _domain = configuration["Mailgun:Domain"] ?? "";
            _from = configuration["Mailgun:From"] ?? "support@ittickets.com";
            
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Basic", 
                Convert.ToBase64String(Encoding.ASCII.GetBytes($"api:{_apiKey}"))
            );
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            try
            {
                _logger.LogInformation($"Sending email to {to} with subject: {subject}");
                
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("from", _from),
                    new KeyValuePair<string, string>("to", to),
                    new KeyValuePair<string, string>("subject", subject),
                    new KeyValuePair<string, string>("html", body)
                });
                
                var response = await _httpClient.PostAsync(
                    $"https://api.mailgun.net/v3/{_domain}/messages", 
                    content
                );
                
                response.EnsureSuccessStatusCode();
                
                _logger.LogInformation($"Email sent successfully to {to}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending email to {to}");
                throw;
            }
        }

        public async Task SendTicketNotificationAsync(string technicianEmail, string ticketId, string title, string priority)
        {
            string subject = $"New Ticket: {title} [{priority}]";
            string body = $@"
                <h1>New Support Ticket: {title}</h1>
                <p>A new {priority} priority ticket has been assigned to you.</p>
                <p>Ticket ID: {ticketId}</p>
                <p>Please log in to the support portal to view the details and take action.</p>
                <a href='https://yourapp.com/tickets/{ticketId}'>View Ticket</a>
            ";
            
            await SendEmailAsync(technicianEmail, subject, body);
        }
    }
}