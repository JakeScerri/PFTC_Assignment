using JakeScerriPFTC_Assignment.Models;
using JakeScerriPFTC_Assignment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace JakeScerriPFTC_Assignment.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Technician")]
    public class TechniciansController : ControllerBase
    {
        private readonly FirestoreService _firestoreService;
        private readonly IRedisService _redisService;
        private readonly ILogger<TechniciansController> _logger;

        public TechniciansController(
            FirestoreService firestoreService,
            IRedisService redisService,
            ILogger<TechniciansController> logger)
        {
            _firestoreService = firestoreService;
            _redisService = redisService;
            _logger = logger;
        }

        [HttpGet("tickets")]
        public async Task<IActionResult> GetTechnicianTickets()
        {
            try
            {
                // KU4.1.a - Read from Redis cache for tickets
                var tickets = await _redisService.GetTechnicianTicketsAsync();
                
                return Ok(new { 
                    message = "Tickets retrieved successfully", 
                    tickets = tickets,
                    count = tickets.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving technician tickets");
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        [HttpPost("tickets/{id}/close")]
        public async Task<IActionResult> CloseTicket(string id)
        {
            try
            {
                string technicianEmail = User.FindFirstValue(ClaimTypes.Email);
                
                // KU4.1.c - Close ticket and handle caching logic
                await _redisService.CloseTicketAsync(id, technicianEmail, _firestoreService);
                
                return Ok(new { 
                    message = $"Ticket {id} closed successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error closing ticket {id}");
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }
        
        [HttpGet("test-redis")]
        public async Task<IActionResult> TestRedis()
        {
            try
            {
                // Create a real test ticket
                var testTicket = new Ticket
                {
                    Id = Guid.NewGuid().ToString(), // Use Guid instead of "test-" prefix
                    Title = "Real Test Ticket", // No [MOCK] prefix
                    Description = "This is a real test ticket created by the technician",
                    Priority = TicketPriority.High,
                    Status = TicketStatus.Open,
                    UserEmail = User.FindFirstValue(ClaimTypes.Email), // Use technician's email
                    DateUploaded = DateTime.UtcNow
                };
        
                // Directly save to Redis
                await _redisService.SaveTicketAsync(testTicket);
        
                return Ok(new { 
                    message = "Real test ticket saved to Redis",
                    ticketId = testTicket.Id
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving test ticket to Redis");
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }
        
        [HttpGet("redis-status")]
        public IActionResult GetRedisStatus()
        {
            try
            {
                bool isConnected = _redisService.IsConnected();
                string connectionInfo = _redisService.GetConnectionInfo();
                string implementationType = _redisService.GetType().Name;
                
                return Ok(new {
                    isConnected,
                    connectionInfo,
                    implementationType
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Redis status");
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }
        
        // New endpoint to clear mock tickets
        [HttpPost("clear-mock-tickets")]
        public async Task<IActionResult> ClearMockTickets()
        {
            try
            {
                _logger.LogInformation("Clearing mock tickets from Redis");
                await _redisService.ClearMockTicketsAsync();
                
                return Ok(new { 
                    message = "Mock tickets cleared successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing mock tickets");
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }
        
        // New endpoint to create a real test ticket with custom details
        [HttpPost("create-real-test-ticket")]
        public async Task<IActionResult> CreateRealTestTicket([FromBody] CreateTestTicketModel model)
        {
            try
            {
                string technicianEmail = User.FindFirstValue(ClaimTypes.Email);
                
                // Create ticket with proper ID
                var ticket = new Ticket
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = model.Title ?? "Real Test Ticket",
                    Description = model.Description ?? "This is a real test ticket created via the technician area",
                    UserEmail = technicianEmail, // Use the current user's email
                    Priority = model.Priority,
                    Status = TicketStatus.Open,
                    DateUploaded = DateTime.UtcNow
                };
                
                _logger.LogInformation($"Creating real test ticket: {ticket.Id}, Priority: {ticket.Priority}");
                
                // Save directly to Redis
                await _redisService.SaveTicketAsync(ticket);
                
                return Ok(new { 
                    message = "Real test ticket created successfully", 
                    ticketId = ticket.Id
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating real test ticket");
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }
    }
   
    // Model for creating a test ticket
    public class CreateTestTicketModel
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public TicketPriority Priority { get; set; } = TicketPriority.High;
    }
}