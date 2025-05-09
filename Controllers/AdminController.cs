using JakeScerriPFTC_Assignment.Models;
using Microsoft.AspNetCore.Mvc;
using JakeScottPFTC_Assignment.Services;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace JakeScerriPFTC_Assignment.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly FirestoreService _firestoreService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(FirestoreService firestoreService, ILogger<AdminController> logger)
        {
            _firestoreService = firestoreService;
            _logger = logger;
        }

        [HttpGet("create-technician")]
        public async Task<IActionResult> CreateTechnician(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                return BadRequest("Email is required");
            }

            try
            {
                var user = await _firestoreService.SaveUserAsync(email, UserRole.Technician);
                _logger.LogInformation($"Created technician: {email}");
                return Ok(new { message = $"Technician {email} created successfully" });
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, $"Error creating technician: {email}");
                return StatusCode(500, $"Error creating technician: {ex.Message}");
            }
        }

        [HttpGet("technicians")]
        public async Task<IActionResult> GetTechnicians()
        {
            try
            {
                var technicians = await _firestoreService.GetTechniciansAsync();
                return Ok(technicians);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error getting technicians");
                return StatusCode(500, $"Error getting technicians: {ex.Message}");
            }
        }
    }
}