// Services/RedisService.cs
using JakeScerriPFTC_Assignment.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JakeScerriPFTC_Assignment.Services
{
    public class RedisService
    {
        private readonly ConnectionMultiplexer _redis;
        private readonly IDatabase _database;
        private readonly ILogger<RedisService> _logger;
        private readonly string _ticketPrefix = "ticket:";
        private readonly string _openTicketsKey = "open-tickets";

        public RedisService(IConfiguration configuration, ILogger<RedisService> logger)
        {
            _logger = logger;
            string connectionString = configuration["Redis:ConnectionString"];
            _logger.LogInformation($"Initializing Redis with connection: {connectionString}");
            
            _redis = ConnectionMultiplexer.Connect(connectionString);
            _database = _redis.GetDatabase();
        }

        // KU4.1.b - Write to cache from HTTP function
        public async Task SaveTicketAsync(Ticket ticket)
        {
            try
            {
                _logger.LogInformation($"Saving ticket {ticket.Id} to Redis");
                string json = JsonConvert.SerializeObject(ticket);
                
                // Store ticket by ID
                await _database.StringSetAsync($"{_ticketPrefix}{ticket.Id}", json);
                
                // Add to sorted set by priority (helps with quick retrieval by priority)
                double score = (int)ticket.Priority * 1000000 + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                await _database.SortedSetAddAsync(_openTicketsKey, ticket.Id, score);
                
                _logger.LogInformation($"Ticket {ticket.Id} saved to Redis");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error saving ticket {ticket.Id} to Redis");
                throw;
            }
        }
        
        // KU4.1.a - Read from cache for technician dashboard
        public async Task<List<Ticket>> GetTechnicianTicketsAsync()
        {
            try
            {
                _logger.LogInformation("Getting technician tickets from Redis");
                // Get tickets sorted by priority (lowest score = highest priority)
                var ticketIds = await _database.SortedSetRangeByScoreAsync(_openTicketsKey);
                
                var tickets = new List<Ticket>();
                foreach (var id in ticketIds)
                {
                    string ticketId = id.ToString();
                    var ticket = await GetTicketAsync(ticketId);
                    
                    if (ticket != null)
                    {
                        // KU4.1.a - Filter based on one week old OR still open
                        bool isRecent = (DateTime.UtcNow - ticket.DateUploaded).TotalDays <= 7;
                        bool isOpen = ticket.Status == TicketStatus.Open;
                        
                        if (isRecent || isOpen)
                        {
                            tickets.Add(ticket);
                        }
                        // KU4.1.c - Otherwise remove from cache and archive
                        else if (!isRecent && ticket.Status == TicketStatus.Closed)
                        {
                            // This will be handled in CloseTicketAsync method
                        }
                    }
                }
                
                _logger.LogInformation($"Retrieved {tickets.Count} tickets for technician");
                return tickets;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting technician tickets from Redis");
                throw;
            }
        }
        
        // Helper method to get a single ticket
        public async Task<Ticket> GetTicketAsync(string ticketId)
        {
            try
            {
                string json = await _database.StringGetAsync($"{_ticketPrefix}{ticketId}");
                if (string.IsNullOrEmpty(json))
                {
                    return null;
                }
                
                return JsonConvert.DeserializeObject<Ticket>(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting ticket {ticketId} from Redis");
                return null;
            }
        }
        
        // KU4.1.c - Remove tickets from cache that are closed and > 1 week old
        public async Task CloseTicketAsync(string ticketId, string technicianEmail, FirestoreService firestoreService)
        {
            try
            {
                _logger.LogInformation($"Closing ticket {ticketId} in Redis");
                
                // Get the ticket from Redis
                var ticket = await GetTicketAsync(ticketId);
                if (ticket == null)
                {
                    _logger.LogWarning($"Ticket {ticketId} not found in Redis");
                    return;
                }
                
                // Update ticket status
                ticket.Status = TicketStatus.Closed;
                
                // Check if ticket is more than 1 week old
                bool isOlderThanOneWeek = (DateTime.UtcNow - ticket.DateUploaded).TotalDays > 7;
                
                if (isOlderThanOneWeek)
                {
                    // Archive the ticket in Firestore
                    await firestoreService.ArchiveTicketAsync(ticket, technicianEmail);
                    
                    // Remove from Redis
                    await _database.KeyDeleteAsync($"{_ticketPrefix}{ticketId}");
                    await _database.SortedSetRemoveAsync(_openTicketsKey, ticketId);
                    
                    _logger.LogInformation($"Ticket {ticketId} archived and removed from Redis");
                }
                else
                {
                    // Just update the ticket in Redis
                    await SaveTicketAsync(ticket);
                    _logger.LogInformation($"Ticket {ticketId} marked as closed in Redis");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error closing ticket {ticketId} in Redis");
                throw;
            }
        }
    }
}