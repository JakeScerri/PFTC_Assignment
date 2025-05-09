using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using JakeScerriPFTC_Assignment.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth;
using JakeScerriPFTC_Assignment.Services;
using JakeScottPFTC_Assignment.Services;

namespace JakeScerriPFTC_Assignment.Controllers
{
    [Route("api/[controller]")]
    public class AuthController : Controller
    {
        private GoogleAuthConfig _authConfig;
        private readonly IConfiguration _configuration;
        private readonly FirestoreService _firestoreService;
        private readonly SecretManagerService _secretManagerService;
        private readonly ILogger<AuthController> _logger;
        private bool _secretsInitialized = false;

        public AuthController(
            IConfiguration configuration,
            FirestoreService firestoreService,
            SecretManagerService secretManagerService,
            ILogger<AuthController> logger)
        {
            _configuration = configuration;
            _firestoreService = firestoreService;
            _secretManagerService = secretManagerService;
            _logger = logger;
            
            // Initialize with placeholder values - will be filled by InitializeSecretsAsync
            _authConfig = new GoogleAuthConfig
            {
                ClientId = "",
                ClientSecret = "",
                RedirectUri = _configuration["GoogleCloud:Auth:RedirectUri"] ?? ""
            };
        }

        private async Task InitializeSecretsAsync()
        {
            if (!_secretsInitialized)
            {
                try
                {
                    _logger.LogInformation("Loading OAuth secrets from Secret Manager");
                    
                    // Load secrets from Secret Manager (AA4.4.a requirement)
                    _authConfig.ClientId = await _secretManagerService.GetSecretAsync("oauth-client-id");
                    _authConfig.ClientSecret = await _secretManagerService.GetSecretAsync("oauth-client-secret");
                    
                    _secretsInitialized = true;
                    _logger.LogInformation("OAuth secrets loaded successfully from Secret Manager");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load OAuth secrets from Secret Manager");
                    
                    // Fall back to configuration values if available
                    _authConfig.ClientId = _configuration["GoogleCloud:Auth:ClientId"] ?? "";
                    _authConfig.ClientSecret = _configuration["GoogleCloud:Auth:ClientSecret"] ?? "";
                    
                    _logger.LogWarning("Using fallback OAuth credentials from configuration");
                }
            }
        }

        [HttpGet("login")]
        public async Task<IActionResult> Login()
        {
            await InitializeSecretsAsync();
            
            // Create authorization URL
            var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new ClientSecrets
                {
                    ClientId = _authConfig.ClientId,
                    ClientSecret = _authConfig.ClientSecret
                },
                Scopes = new[] { "email", "profile" },
            });

            var url = flow.CreateAuthorizationCodeRequest(_authConfig.RedirectUri).Build().ToString();
            return Redirect(url);
        }

        [HttpGet("callback")]
        public async Task<IActionResult> Callback([FromQuery] string code)
        {
            try
            {
                await InitializeSecretsAsync();
                
                // Exchange authorization code for tokens
                var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
                {
                    ClientSecrets = new ClientSecrets
                    {
                        ClientId = _authConfig.ClientId,
                        ClientSecret = _authConfig.ClientSecret
                    },
                    Scopes = new[] { "email", "profile" }
                });

                var token = await flow.ExchangeCodeForTokenAsync(
                    "", // Not using a user ID here
                    code,
                    _authConfig.RedirectUri,
                    CancellationToken.None);

                // Validate the token and get user info
                var payload = await GoogleJsonWebSignature.ValidateAsync(token.IdToken);
                
                // Create claims for authentication
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Email, payload.Email),
                    new Claim(ClaimTypes.Name, payload.Name ?? payload.Email),
                    new Claim("GoogleId", payload.Subject),
                    new Claim("Picture", payload.Picture ?? "")
                };

                // Save user to Firestore (default as regular user)
                var user = await _firestoreService.SaveUserAsync(payload.Email);
                
                // Add role claim
                claims.Add(new Claim(ClaimTypes.Role, user.Role.ToString()));

                // Create claims identity
                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                // Sign in the user
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    principal,
                    new AuthenticationProperties
                    {
                        IsPersistent = true,
                        ExpiresUtc = DateTime.UtcNow.AddDays(7)
                    });

                // Redirect to the home page
                return Redirect("/");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Authentication failed");
                return StatusCode(500, $"Authentication failed: {ex.Message}");
            }
        }

        [HttpGet("logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Redirect("/");
        }

        [HttpGet("user")]
        public IActionResult GetCurrentUser()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Json(new { isAuthenticated = false });
            }

            return Json(new
            {
                isAuthenticated = true,
                email = User.FindFirstValue(ClaimTypes.Email),
                name = User.FindFirstValue(ClaimTypes.Name),
                picture = User.FindFirstValue("Picture"),
                role = User.FindFirstValue(ClaimTypes.Role)
            });
        }
    }
}