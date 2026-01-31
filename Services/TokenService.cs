using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using JwtAuthDemo.Configurations;
using JwtAuthDemo.Data;
using JwtAuthDemo.DTOs;
using JwtAuthDemo.Models;

namespace JwtAuthDemo.Services
{
    public class TokenService : ITokenService
    {
        private readonly ApplicationDbContext _context;
        private readonly JwtSettings _jwtSettings;
        private readonly ILogger<TokenService> _logger;

        public TokenService(
            ApplicationDbContext context,
            IOptions<JwtSettings> jwtSettings,
            ILogger<TokenService> logger)
        {
            _context = context;
            _jwtSettings = jwtSettings.Value;
            _logger = logger;
        }

        public string GenerateAccessToken(User user)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpiryMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string GenerateRefreshToken()
        {
            var randomNumber = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        public async Task<RefreshTokenResponse> CreateRefreshTokenAsync(int userId)
        {
            // Revoke existing active refresh tokens
            var existingTokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == userId && !rt.IsRevoked && rt.ExpiryDate > DateTime.UtcNow)
                .ToListAsync();

            foreach (var token in existingTokens)
            {
                token.IsRevoked = true;
                token.RevokedReason = "Replaced by new token";
            }

            // Create new refresh token
            var refreshToken = new RefreshToken
            {
                Token = GenerateRefreshToken(),
                UserId = userId,
                ExpiryDate = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays),
                Created = DateTime.UtcNow,
                IsRevoked = false
            };

            await _context.RefreshTokens.AddAsync(refreshToken);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created new refresh token for user {UserId}", userId);

            return new RefreshTokenResponse
            {
                RefreshToken = refreshToken.Token,
                RefreshTokenExpiry = refreshToken.ExpiryDate
            };
        }

        public async Task<TokenRefreshResult> RefreshAccessTokenAsync(string refreshToken)
        {
            var storedToken = await _context.RefreshTokens
                .Include(rt => rt.User)
                .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

            if (storedToken == null)
            {
                _logger.LogWarning("Refresh token not found: {Token}", refreshToken);
                return new TokenRefreshResult
                {
                    Success = false,
                    Error = "Invalid refresh token"
                };
            }

            if (storedToken.IsRevoked)
            {
                _logger.LogWarning("Attempted to use revoked token for user {UserId}", storedToken.UserId);
                await RevokeRefreshTokenAsync(storedToken.UserId);
                
                return new TokenRefreshResult
                {
                    Success = false,
                    Error = "Refresh token has been revoked. Please login again."
                };
            }

            if (storedToken.ExpiryDate < DateTime.UtcNow)
            {
                storedToken.IsRevoked = true;
                storedToken.RevokedReason = "Token expired";
                await _context.SaveChangesAsync();

                return new TokenRefreshResult
                {
                    Success = false,
                    Error = "Refresh token has expired. Please login again."
                };
            }

            // Generate new tokens
            var newAccessToken = GenerateAccessToken(storedToken.User);
            var accessTokenExpiry = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpiryMinutes);

            // TOKEN ROTATION
            var newRefreshTokenResponse = await CreateRefreshTokenAsync(storedToken.UserId);

            // Revoke old token
            storedToken.IsRevoked = true;
            storedToken.RevokedReason = "Replaced by new token (rotation)";
            await _context.SaveChangesAsync();

            _logger.LogInformation("Refreshed tokens for user {UserId}", storedToken.UserId);

            return new TokenRefreshResult
            {
                Success = true,
                AccessToken = newAccessToken,
                RefreshToken = newRefreshTokenResponse.RefreshToken,
                AccessTokenExpiry = accessTokenExpiry,
                RefreshTokenExpiry = newRefreshTokenResponse.RefreshTokenExpiry
            };
        }

        public async Task<bool> RevokeRefreshTokenAsync(int userId, string? refreshToken = null)
        {
            if (!string.IsNullOrEmpty(refreshToken))
            {
                var token = await _context.RefreshTokens
                    .FirstOrDefaultAsync(rt => rt.Token == refreshToken && rt.UserId == userId);

                if (token != null)
                {
                    token.IsRevoked = true;
                    token.RevokedReason = "Manually revoked";
                    await _context.SaveChangesAsync();
                    return true;
                }
                return false;
            }

            var userTokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == userId && !rt.IsRevoked)
                .ToListAsync();

            foreach (var token in userTokens)
            {
                token.IsRevoked = true;
                token.RevokedReason = "User logout / Security revocation";
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Revoked all tokens for user {UserId}", userId);
            return true;
        }

        public async Task CleanupExpiredTokensAsync()
        {
            var expiredTokens = await _context.RefreshTokens
                .Where(rt => rt.ExpiryDate < DateTime.UtcNow || rt.IsRevoked)
                .Where(rt => rt.Created < DateTime.UtcNow.AddDays(-30))
                .ToListAsync();

            _context.RefreshTokens.RemoveRange(expiredTokens);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Cleaned up {Count} expired tokens", expiredTokens.Count);
        }
    }
}