using JakeScerriPFTC_Assignment.Services;
using Microsoft.AspNetCore.Mvc;
using JakeScottPFTC_Assignment.Services;

namespace JakeScerriPFTC_Assignment.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestController : ControllerBase
    {
        private readonly StorageService _storageService;
        private readonly FirestoreService _firestoreService;
        private readonly PubSubService _pubSubService;

        public TestController(
            StorageService storageService,
            FirestoreService firestoreService,
            PubSubService pubSubService)
        {
            _storageService = storageService;
            _firestoreService = firestoreService;
            _pubSubService = pubSubService;
        }

        [HttpGet]
        public IActionResult TestGoogleCloud()
        {
            // This verifies that all services are properly initialized
            return Ok(new { 
                message = "Google Cloud services initialized successfully", 
                timestamp = DateTime.UtcNow 
            });
        }
    }
}