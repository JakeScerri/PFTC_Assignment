using JakeScerriPFTC_Assignment.Models;
using JakeScerriPFTC_Assignment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
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
        private readonly RedisService _redisService;
        private readonly ILogger<TechniciansController> _logger;

        public TechniciansController(
            FirestoreService firestoreService,
            RedisService redisService,
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
        // Add this to your TechniciansController.cs
        [HttpGet("test-redis")]
        public async Task<IActionResult> TestRedis()
        {
            try
            {
                // Create a test ticket
                var testTicket = new Ticket
                {
                    Id = "test-" + DateTime.UtcNow.Ticks,
                    Title = "Test Redis Ticket",
                    Description = "Direct Redis test",
                    Priority = TicketPriority.High,
                    Status = TicketStatus.Open,
                    UserEmail = User.FindFirstValue(ClaimTypes.Email),
                    DateUploaded = DateTime.UtcNow
                };
        
                // Directly save to Redis
                await _redisService.SaveTicketAsync(testTicket);
        
                return Ok(new { 
                    message = "Test ticket saved to Redis",
                    ticketId = testTicket.Id
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving test ticket to Redis");
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }
    }
}