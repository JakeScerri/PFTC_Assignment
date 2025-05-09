// Controllers/TicketsController.cs
using JakeScerriPFTC_Assignment.Models;
using JakeScerriPFTC_Assignment.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using JakeScottPFTC_Assignment.Services;
using Microsoft.AspNetCore.Http;

namespace JakeScerriPFTC_Assignment.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Add this attribute to require authentication
    public class TicketsController : ControllerBase
    {
        private readonly StorageService _storageService;
        private readonly FirestoreService _firestoreService;
        private readonly PubSubService _pubSubService;

        public TicketsController(
            StorageService storageService,
            FirestoreService firestoreService,
            PubSubService pubSubService)
        {
            _storageService = storageService;
            _firestoreService = firestoreService;
            _pubSubService = pubSubService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateTicket([FromForm] TicketCreateModel model)
        {
            try
            {
                // Get email from authenticated user
                string userEmail = User.FindFirstValue(ClaimTypes.Email) ?? "anonymous@example.com";
                
                // Ensure user exists in Firestore (AA2.1.d)
                await _firestoreService.SaveUserAsync(userEmail);
                
                // Upload screenshots to Cloud Storage (AA2.1.c & KU4.3.a)
                var imageUrls = new List<string>();
                if (model.Screenshots != null && model.Screenshots.Count > 0)
                {
                    imageUrls = await _storageService.UploadFilesAsync(model.Screenshots, userEmail);
                }
                
                // Create ticket object
                var ticket = new Ticket
                {
                    Title = model.Title,
                    Description = model.Description,
                    UserEmail = userEmail,
                    Priority = model.Priority,
                    ImageUrls = imageUrls,
                    Status = TicketStatus.Open,
                    DateUploaded = DateTime.UtcNow
                };
                
                // Publish ticket to PubSub with priority attribute (AA2.1.a & AA2.1.b)
                var messageId = await _pubSubService.PublishTicketAsync(ticket);
                
                return Ok(new 
                { 
                    message = "Ticket created successfully", 
                    ticketId = ticket.Id, 
                    messageId,
                    imageUrls
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }
    }

    public class TicketCreateModel
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public TicketPriority Priority { get; set; }
        public List<IFormFile> Screenshots { get; set; } = new List<IFormFile>();
    }
}