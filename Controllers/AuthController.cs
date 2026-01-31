using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using JwtAuthDemo.Data;
using JwtAuthDemo.DTOs;
using JwtAuthDemo.Models;
using JwtAuthDemo.Services;

namespace JwtAuthDemo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ITokenService _tokenService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            ApplicationDbContext context,
            ITokenService tokenService,
            ILogger<AuthController> logger)
        {
            _context = context;
            _tokenService = tokenService;
            _logger = logger;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            //Validation

            if (!ModelState.IsValid)
            {
                return BadRequest(new { message = "Invalid request", errors = ModelState });
            }
            // Fetch DB  and compare user

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == request.Username);

            if (user == null)
            {
                _logger.LogWarning("Login attempt with invalid username: {Username}", request.Username);
                return Unauthorized(new { message = "Invalid username or password" });
            }
            // Compare password

            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
            {
                _logger.LogWarning("Login attempt with invalid password for user: {Username}", request.Username);
                return Unauthorized(new { message = "Invalid username or password" });
            }

            var accessToken = _tokenService.GenerateAccessToken(user);
            var accessTokenExpiry = DateTime.UtcNow.AddMinutes(15);
            var refreshTokenResponse = await _tokenService.CreateRefreshTokenAsync(user.Id);

            _logger.LogInformation("User {Username} logged in successfully", user.Username);

            return Ok(new LoginResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshTokenResponse.RefreshToken,
                AccessTokenExpiry = accessTokenExpiry,
                RefreshTokenExpiry = refreshTokenResponse.RefreshTokenExpiry,
                Username = user.Username,
                Role = user.Role
            });
        }

        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            if (string.IsNullOrEmpty(request.RefreshToken))
            {
                return BadRequest(new { message = "Refresh token is required" });
            }

            var result = await _tokenService.RefreshAccessTokenAsync(request.RefreshToken);

            if (!result.Success)
            {
                return Unauthorized(new { message = result.Error });
            }

            return Ok(new RefreshTokenResponse
            {
                AccessToken = result.AccessToken!,
                RefreshToken = result.RefreshToken!,
                AccessTokenExpiry = result.AccessTokenExpiry!.Value,
                RefreshTokenExpiry = result.RefreshTokenExpiry!.Value
            });
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout([FromBody] RevokeTokenRequest? request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { message = "Invalid user token" });
            }

            var success = await _tokenService.RevokeRefreshTokenAsync(userId, request?.RefreshToken);

            if (!success)
            {
                return NotFound(new { message = "Token not found" });
            }

            _logger.LogInformation("User {UserId} logged out", userId);
            return Ok(new { message = "Logged out successfully" });
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { message = "Invalid request", errors = ModelState });
            }

            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == request.Username);

            if (existingUser != null)
            {
                return Conflict(new { message = "Username already exists" });
            }

            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

            var newUser = new User
            {
                Username = request.Username,
                Password = hashedPassword,
                Role = request.Role ?? "User",
                CreatedAt = DateTime.UtcNow
            };

            await _context.Users.AddAsync(newUser);
            await _context.SaveChangesAsync();

            _logger.LogInformation("New user registered: {Username}", newUser.Username);

            return Ok(new
            {
                message = "User registered successfully",
                userId = newUser.Id,
                username = newUser.Username
            });
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { message = "Invalid token" });
            }

            var user = await _context.Users.FindAsync(userId);

            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            return Ok(new
            {
                id = user.Id,
                username = user.Username,
                role = user.Role,
                createdAt = user.CreatedAt
            });
        }

        [HttpGet("admin/users")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _context.Users
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.Role,
                    u.CreatedAt
                })
                .ToListAsync();

            return Ok(users);
        }
    }
}