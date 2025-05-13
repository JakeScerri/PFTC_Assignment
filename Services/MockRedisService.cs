// Services/MockRedisService.cs
using JakeScerriPFTC_Assignment.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace JakeScerriPFTC_Assignment.Services
{
    public class MockRedisService : IRedisService
    {
        private readonly ILogger<MockRedisService> _logger;
        
        // In-memory storage to replace Redis
        private readonly ConcurrentDictionary<string, string> _stringValues = new ConcurrentDictionary<string, string>();
        private readonly ConcurrentDictionary<string, SortedList<double, string>> _sortedSets = new ConcurrentDictionary<string, SortedList<double, string>>();
        
        // Constants
        private readonly string _ticketPrefix = "ticket:";
        private readonly string _openTicketsKey = "open-tickets";

        public MockRedisService(IConfiguration configuration, ILogger<MockRedisService> logger)
        {
            _logger = logger;
            _logger.LogInformation("Using mock Redis implementation");
            
            // Initialize sorted set for open tickets
            _sortedSets[_openTicketsKey] = new SortedList<double, string>();
        }

        // KU4.1.b - Write to cache from HTTP function
        public async Task SaveTicketAsync(Ticket ticket)
        {
            try
            {
                _logger.LogInformation($"[MOCK] Saving ticket {ticket.Id} to Redis");
                string json = JsonConvert.SerializeObject(ticket);
                
                // Store ticket by ID
                _stringValues[$"{_ticketPrefix}{ticket.Id}"] = json;
                
                // Add to sorted set by priority
                double score = (int)ticket.Priority * 1000000 + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                
                if (!_sortedSets.ContainsKey(_openTicketsKey))
                {
                    _sortedSets[_openTicketsKey] = new SortedList<double, string>();
                }
                
                var sortedSet = _sortedSets[_openTicketsKey];
                
                // Remove existing entry if present (to avoid duplicates)
                foreach (var pair in sortedSet.Where(kv => kv.Value == ticket.Id).ToList())
                {
                    sortedSet.Remove(pair.Key);
                }
                
                // Add with new score
                sortedSet.Add(score, ticket.Id);
                
                _logger.LogInformation($"[MOCK] Ticket {ticket.Id} saved to Redis");
                
                await Task.CompletedTask; // To maintain the async pattern
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[MOCK] Error saving ticket {ticket.Id} to Redis");
                throw;
            }
        }
        
        // Helper method to identify mock tickets
        private bool IsMockTicket(Ticket ticket)
        {
            return ticket.UserEmail == "test@example.com" ||
                   ticket.UserEmail.Contains("mock") ||
                   ticket.Title.StartsWith("[MOCK]") ||
                   ticket.Id.StartsWith("test-");
        }
        
        // KU4.1.a - Read from cache for technician dashboard
        // Updated to filter out mock tickets
        public async Task<List<Ticket>> GetTechnicianTicketsAsync()
        {
            try
            {
                _logger.LogInformation("[MOCK] Getting technician tickets from Redis");
                
                var tickets = new List<Ticket>();
                
                if (_sortedSets.TryGetValue(_openTicketsKey, out var sortedSet))
                {
                    foreach (var id in sortedSet.Values)
                    {
                        var ticket = await GetTicketAsync(id);
                        
                        if (ticket != null)
                        {
                            // Filter out mock tickets
                            bool isMockTicket = IsMockTicket(ticket);
                            
                            if (!isMockTicket)
                            {
                                // KU4.1.a - Filter based on one week old OR still open
                                bool isRecent = (DateTime.UtcNow - ticket.DateUploaded).TotalDays <= 7;
                                bool isOpen = ticket.Status == TicketStatus.Open;
                                
                                if (isRecent || isOpen)
                                {
                                    tickets.Add(ticket);
                                }
                            }
                        }
                    }
                }
                
                _logger.LogInformation($"[MOCK] Retrieved {tickets.Count} real tickets for technician");
                return tickets;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MOCK] Error getting technician tickets");
                return new List<Ticket>();
            }
        }
        
        // Helper method to get a single ticket
        public async Task<Ticket> GetTicketAsync(string ticketId)
        {
            try
            {
                if (_stringValues.TryGetValue($"{_ticketPrefix}{ticketId}", out string json))
                {
                    return JsonConvert.DeserializeObject<Ticket>(json);
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[MOCK] Error getting ticket {ticketId}");
                return null;
            }
        }
        
        // KU4.1.c - Remove tickets from cache that are closed and > 1 week old
        public async Task CloseTicketAsync(string ticketId, string technicianEmail, FirestoreService firestoreService)
        {
            try
            {
                _logger.LogInformation($"[MOCK] Closing ticket {ticketId} in Redis");
                
                // Get the ticket
                var ticket = await GetTicketAsync(ticketId);
                if (ticket == null)
                {
                    _logger.LogWarning($"[MOCK] Ticket {ticketId} not found in Redis");
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
                    
                    // Remove from mock Redis
                    _stringValues.TryRemove($"{_ticketPrefix}{ticketId}", out _);
                    
                    // Remove from sorted set
                    if (_sortedSets.TryGetValue(_openTicketsKey, out var sortedSet))
                    {
                        foreach (var pair in sortedSet.Where(kv => kv.Value == ticketId).ToList())
                        {
                            sortedSet.Remove(pair.Key);
                        }
                    }
                    
                    _logger.LogInformation($"[MOCK] Ticket {ticketId} archived and removed from Redis");
                }
                else
                {
                    // Just update the ticket
                    await SaveTicketAsync(ticket);
                    _logger.LogInformation($"[MOCK] Ticket {ticketId} marked as closed in Redis");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[MOCK] Error closing ticket {ticketId} in Redis");
                throw;
            }
        }
        
        // New method to clear mock tickets
        public async Task ClearMockTicketsAsync()
        {
            try
            {
                _logger.LogInformation("[MOCK] Clearing mock tickets from Redis");
                
                if (_sortedSets.TryGetValue(_openTicketsKey, out var sortedSet))
                {
                    var ticketsToRemove = new List<KeyValuePair<double, string>>();
                    
                    foreach (var pair in sortedSet)
                    {
                        string ticketId = pair.Value;
                        var ticket = await GetTicketAsync(ticketId);
                        
                        if (ticket != null && IsMockTicket(ticket))
                        {
                            ticketsToRemove.Add(pair);
                            _stringValues.TryRemove($"{_ticketPrefix}{ticketId}", out _);
                            _logger.LogInformation($"[MOCK] Removed mock ticket {ticketId} from Redis");
                        }
                    }
                    
                    // Remove from sorted set
                    foreach (var pair in ticketsToRemove)
                    {
                        sortedSet.Remove(pair.Key);
                    }
                    
                    _logger.LogInformation($"[MOCK] Finished clearing {ticketsToRemove.Count} mock tickets");
                }
                
                await Task.CompletedTask; // Maintain async
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MOCK] Error clearing mock tickets");
                throw;
            }
        }
        
        // Helper methods for diagnostics
        public bool IsConnected() => true;  // Always return connected
        
        public string GetConnectionInfo() => "Mock Redis Implementation (In-Memory)";
    }
}