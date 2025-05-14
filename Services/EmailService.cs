using Microsoft.Extensions.Configuration;
using RestSharp;
using RestSharp.Authenticators;
using System;
using System.Threading.Tasks;
using Google.Cloud.SecretManager.V1;

public class EmailService : IEmailService
{
    private readonly string _mailgunApiKey;
    private readonly string _mailgunDomain;
    private readonly string _fromEmail;
    private readonly string _fromName;

    public EmailService(IConfiguration configuration)
    {
        // Fetch secrets from Google Secret Manager
        SecretManagerServiceClient secretClient = SecretManagerServiceClient.Create();
        
        // The full resource name for the secret (format: projects/{project-id}/secrets/{secret-id}/versions/latest)
        string mailgunSecretName = $"projects/{configuration["ProjectId"]}/secrets/mailgun-api-key/versions/latest";
        
        // Access the secret
        AccessSecretVersionResponse response = secretClient.AccessSecretVersion(mailgunSecretName);
        _mailgunApiKey = response.Payload.Data.ToStringUtf8();
        
        // Get values from configuration
        _mailgunDomain = configuration["Mailgun:Domain"];
        _fromEmail = configuration["Mailgun:FromEmail"];
        _fromName = configuration["Mailgun:FromName"];
    }

    public async Task<bool> SendEmailAsync(string to, string subject, string htmlContent, string correlationId = null)
    {
        try
        {
            // Create RestClient with Mailgun Base URL
            var client = new RestClient
            {
                BaseUrl = new Uri($"https://api.mailgun.net/v3/{_mailgunDomain}"),
                Authenticator = new HttpBasicAuthenticator("api", _mailgunApiKey)
            };

            // Create request
            var request = new RestRequest();
            request.AddParameter("domain", _mailgunDomain, ParameterType.UrlSegment);
            request.Resource = "{domain}/messages";
            request.AddParameter("from", $"{_fromName} <{_fromEmail}>");
            request.AddParameter("to", to);
            request.AddParameter("subject", subject);
            request.AddParameter("html", htmlContent);
            
            // Add custom X-header for tracking correlation ID if provided
            if (!string.IsNullOrEmpty(correlationId))
            {
                request.AddParameter("h:X-Correlation-ID", correlationId);
            }
            
            request.Method = Method.POST;

            // Send the request
            var response = await client.ExecuteAsync(request);
            
            // Check if successful
            return response.IsSuccessful;
        }
        catch (Exception ex)
        {
            // Log exception
            Console.WriteLine($"Error sending email: {ex.Message}");
            return false;
        }
    }
}