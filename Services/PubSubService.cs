// Services/PubSubService.cs
using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using JakeScerriPFTC_Assignment.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace JakeScerriPFTC_Assignment.Services
{
    public class PubSubService
    {
        private readonly string _projectId;
        private readonly string _topicName;
        private readonly PublisherClient _publisherClient;
        private readonly ILogger<PubSubService> _logger;

        public PubSubService(IConfiguration configuration, ILogger<PubSubService> logger)
        {
            _logger = logger;
            _projectId = configuration["GoogleCloud:ProjectId"];
            _topicName = configuration["GoogleCloud:TopicName"] ?? "tickets-topic-jakescerri";
            
            _logger.LogInformation($"Initializing PubSubService with project: {_projectId}, topic: {_topicName}");
            
            // Create the topic name
            var topicName = new TopicName(_projectId, _topicName);
            
            // Create the publisher client
            _publisherClient = PublisherClient.Create(topicName);
        }

        // AA2.1.a & AA2.1.b - Publish ticket to PubSub with priority attribute
        public async Task<string> PublishTicketAsync(Ticket ticket)
        {
            try
            {
                _logger.LogInformation($"Publishing ticket {ticket.Id} with priority {ticket.Priority}");
                
                // Serialize the ticket to JSON
                var ticketJson = JsonConvert.SerializeObject(ticket);
                
                // Create the message with priority attribute
                var message = new PubsubMessage
                {
                    Data = ByteString.CopyFromUtf8(ticketJson),
                    // Set priority as an attribute for filtering (AA2.1.b)
                    Attributes = 
                    {
                        ["priority"] = ticket.Priority.ToString().ToLower()
                    }
                };

                // Publish the message
                string messageId = await _publisherClient.PublishAsync(message);
                _logger.LogInformation($"Ticket published with ID: {messageId}, Priority: {ticket.Priority}");
                return messageId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error publishing ticket: {ex.Message}");
                throw;
            }
        }
        
        // KU4.1 - Get a ticket from PubSub for processing (mock implementation)
        public async Task<Ticket> GetNextTicketAsync()
        {
            try
            {
                _logger.LogInformation("Getting next ticket from PubSub (mock implementation)");
                
                // For testing purposes, create a mock ticket
                await Task.Delay(100); // Simulate async operation
                
                // Randomly decide whether to return a ticket or null (for testing)
                if (new Random().Next(0, 4) == 0) // 25% chance of returning null
                {
                    _logger.LogInformation("No tickets in the queue (mock)");
                    return null;
                }
                
                // Create a mock ticket
                var ticket = CreateMockTicket();
                
                _logger.LogInformation($"Retrieved ticket {ticket.Id} from PubSub (mock)");
                return ticket;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting next ticket from PubSub");
                // For testing, create a mock ticket
                return CreateMockTicket();
            }
        }
        
        // SE4.6.a - Get tickets by priority (mock implementation)
        public async Task<List<Ticket>> GetTicketsByPriorityAsync(TicketPriority priority)
        {
            try
            {
                _logger.LogInformation($"Getting tickets with priority: {priority} (mock implementation)");
                
                // Simulate async operation
                await Task.Delay(100);
                
                // For testing, create 0-3 mock tickets with the requested priority
                int count = new Random().Next(0, 4);
                var tickets = new List<Ticket>();
                
                for (int i = 0; i < count; i++)
                {
                    tickets.Add(CreateMockTicket(priority));
                }
                
                _logger.LogInformation($"Retrieved {tickets.Count} tickets with priority {priority} (mock)");
                return tickets;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting tickets with priority {priority}");
                return new List<Ticket>();
            }
        }
        
        // Create a mock ticket for testing
        private Ticket CreateMockTicket(TicketPriority? priority = null)
        {
            TicketPriority ticketPriority = priority ?? (TicketPriority)new Random().Next(0, 3);
            string priorityName = ticketPriority.ToString();
            
            return new Ticket
            {
                Id = Guid.NewGuid().ToString(),
                Title = $"Mock {priorityName} Ticket",
                Description = $"This is a mock {priorityName.ToLower()} priority ticket created for testing",
                UserEmail = "test@example.com",
                Priority = ticketPriority,
                Status = TicketStatus.Open,
                DateUploaded = DateTime.UtcNow.AddHours(-new Random().Next(0, 72)), // Random age up to 3 days
                ImageUrls = new List<string>()
            };
        }
    }
}