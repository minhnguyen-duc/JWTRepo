using JwtAuthDemo.Models;
using JwtAuthDemo.DTOs;

namespace JwtAuthDemo.Services
{
    public interface ITokenService
    {
        /// <summary>
        /// Generate JWT access token for a user
        /// </summary>
        string GenerateAccessToken(User user);

        /// <summary>
        /// Generate a random refresh token
        /// </summary>
        string GenerateRefreshToken();

        /// <summary>
        /// Create and save refresh token to database
        /// </summary>
        Task<RefreshTokenResponse> CreateRefreshTokenAsync(int userId);

        /// <summary>
        /// Refresh access token using refresh token
        /// </summary>
        Task<TokenRefreshResult> RefreshAccessTokenAsync(string refreshToken);

        /// <summary>
        /// Revoke refresh token(s) for a user
        /// </summary>
        Task<bool> RevokeRefreshTokenAsync(int userId, string? refreshToken = null);

        /// <summary>
        /// Clean up expired refresh tokens from database
        /// </summary>
        Task CleanupExpiredTokensAsync();
    }
}