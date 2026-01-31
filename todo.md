Implement JWT 
-- STEP 

1. Setup the Project
Create a new ASP.NET Core Web API project:

dotnet new webapi -n JwtAuthDemo
cd JwtAuthDemo

2. Install neccessary package 
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer

3.  Create a Basic User Model


public class User
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    public string Username { get; set; }
    public string Password { get; set; } // Never store passwords like this in production!
    public string Role { get; set; }
}
4. We set Users in DbContext
 public virtual DbSet<User> Users { get; set; }

and Create Dto 

public class LoginRequest
{
    public string Username { get; set; }
    public string Password { get; set; }
}

5. Add Token Generation Logic
Create a service to generate JWTs:

public class TokenService
{
    private readonly IConfiguration _config;
    private readonly string _secretKey;
    private readonly int _accessTokenExpiryMinutes;

    public TokenService(IConfiguration config)
    {
        _config = config;
        _secretKey = configuration["ApiSettings:Secret"];
        _accessTokenExpiryMinutes = configuration.GetValue<int>("ApiSettings:AccessTokenExpiryMinutes", 60);
    }

    public string GenerateToken(User user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["ApiSettings:SecretKey"]));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _config["ApiSettings:SecretKey"],
            audience: _config["ApiSettings:SecretKey"],
            claims: claims,
            expires: DateTime.Now.AddMinutes(_accessTokenExpiryMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

6. Add Jwt config to appsettings.json:


"ApiSettings": {
  "Secret": "a-very-long-secret-key-for-hmac-sha256-that-is-32-bytes-long",
  "Issuer": "yourdomain.com",
  "Audience": "yourdomain.com"
}

7. Configure JWT Authentication in Program.cs

Add this in Program.cs:

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["ApiSettings:Issuer"],
            ValidAudience = builder.Configuration["ApiSettings:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<TokenService>();
And don’t forget to enable authentication/authorization in your Program.cs:

app.UseAuthentication();
app.UseAuthorization();

8. Add a Login Endpoint

In your controller:

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly TokenService _tokenService;
    private readonly DbContext _context;

    public AuthController(TokenService tokenService, DbContext context)
    {
        _tokenService = tokenService;
        _context = context;
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        var user = _context.Users.SingleOrDefault(u => 
            u.Username == request.Username && 
            u.Password == request.Password);

        if (user == null)
            return Unauthorized("Invalid credentials");

        var token = _tokenService.GenerateToken(user);

        return Ok(new { token });
    }
}

Make sure your request includes the JWT in the Authorization header:




Timing: Minutes:  0 - USER LOGIN
═══════════════════════════════════════════════════════════════════

┌──────────┐                                    ┌──────────┐
│  Client  │         POST /login                │  Server  │
│          │─────────────────────────────────>  │          │
│          │   {username, password}             │          │
│          │                                    │          │
│          │  <─────────────────────────────────│          │
│          │         Response:                  │          │
│          │   {                                │          │
│          │     accessToken: "eyJhb...",       │          │
│          │     refreshToken: "abc123...",     │          │
│          │     expiresIn: 900  (15 phút)      │          │
│          │   }                                │          │
└──────────┘                                    └──────────┘

Client store:
├─ accessToken: "eyJhb..." (exp: 15 mins)
└─ refreshToken: "abc123..." (exp: 7 days)

Database stores:
┌─────────────────────────────────────────┐
│ RefreshTokens Table                     │
├─────────────────────────────────────────┤
│ Token: "abc123..."                      │
│ UserId: 1                               │
│ ExpiryDate: 2026-02-03 (after 7 days)    │
│ Created: 2026-01-27                     │
│ IsRevoked: false                        │
└─────────────────────────────────────────┘


Timing: Minutes  1-14 -  ACCESS TOKEN WORKS AS NORMAL
═══════════════════════════════════════════════════════════════════

┌──────────┐                                    ┌──────────┐
│  Client  │      GET /api/user/profile         │  Server  │
│          │─────────────────────────────────>  │          │
│          │  Header:                           │          │
│          │   Authorization: Bearer eyJhb...   │          │
│          │                                    │  ✓ Verify│
│          │                                    │  ✓ Valid │
│          │  <─────────────────────────────────│  ✓ Not   │
│          │         200 OK                     │  expired │
│          │    {user data...}                  │          │
└──────────┘                                    └──────────┘



Timing: Minutes 16 - ACCESS TOKEN HAS EXPIRED
═══════════════════════════════════════════════════════════════════

┌──────────┐                                    ┌──────────┐
│  Client  │      GET /api/user/profile         │  Server  │
│          │─────────────────────────────────>  │          │
│          │  Header:                           │          │
│          │   Authorization: Bearer eyJhb...   │  ✓ Verify│
│          │                                    │  ✗ Token │
│          │  <─────────────────────────────────│  EXPIRED!│
│          │         401 Unauthorized           │          │
│          │    {                               │          │
│          │      error: "Token expired"        │          │
│          │    }                               │          │
└──────────┘                                    └──────────┘
      │
      │ Client received  401, check error
      │
      ▼
┌─────────────────────────────────────┐
│ Client automatically call /refresh  │
│                                     │
└─────────────────────────────────────┘


 REFRESH - ROTATION TOKEN 
═══════════════════════════════════════════════════════════════════

┌──────────┐                                    ┌──────────┐
│  Client  │      POST /refresh                 │  Server  │
│          │─────────────────────────────────>  │          │
│          │   Body:                            │          │
│          │   {                                │          │
│          │     refreshToken: "abc123..."      │          │
│          │   }                                │          │
│          │                                    │          │
│          │                                    └──────────┘
│          │                                         │
│          │                                         │
│          │                    ┌────────────────────▼──────────┐
│          │                    │ Step 1: Find in  DATABASE     │
│          │                    │                                │
│          │                    │ SELECT * FROM RefreshTokens   │
│          │                    │ WHERE Token = "abc123..."     │
│          │                    │   AND IsRevoked = false       │
│          │                    │                                │
│          │                    │ Result:                      │
│          │                    │ ✓ Token founded            │
│          │                    │ ✓ Revoked : false              │
│          │                    │ ✓  Expired : false (còn 6d 23h)   │
│          │                    └────────────────────┬──────────┘
│          │                                         │
│          │                    ┌────────────────────▼──────────┐
│          │                    │ Step 2: CREATE NEW ACCESS TOKEN   │
│          │                    │                                │
│          │                    │ newAccessToken = JWT.create({ │
│          │                    │   userId: 1,                  │
│          │                    │   exp: now + 15 MINS          │
│          │                    │ })                            │
│          │                    │                                │
│          │                    │ Result: "eyJNEW..."           │
│          │                    └────────────────────┬──────────┘
│          │                                         │
│          │                    ┌────────────────────▼──────────┐
│          │                    │ Step 3: CREATE NEW REFRESH TOKEN 
│          │                    │ (ROTATION!)                   │
│          │                    │                                │
│          │                    │ newRefreshToken = random();   │
│          │                    │ // "xyz789..."                │
│          │                    │                                │
│          │                    │ INSERT INTO RefreshTokens:    │
│          │                    │ {                             │
│          │                    │   Token: "xyz789...",         │
│          │                    │   UserId: 1,                  │
│          │                    │   ExpiryDate: now + 7 days,   │
│          │                    │   Created: now,               │
│          │                    │   IsRevoked: false            │
│          │                    │ }                             │
│          │                    └────────────────────┬──────────┘
│          │                                         │
│          │                    ┌────────────────────▼──────────┐
│          │                    │ Step  4: DISABLED REFRESH TOKEN │
│          │                    │ (REVOKE!)                     │
│          │                    │                                │
│          │                    │ UPDATE RefreshTokens          │
│          │                    │ SET IsRevoked = true          │
│          │                    │ WHERE Token = "abc123..."     │
│          │                    │                                │
│          │                    │ Refresh Token "abc123..."      │
│          │                    │ ✗ Disabled       │
│          │                    └────────────────────┬──────────┘
│          │                                         │
│          │                                    ┌────▼──────┐
│          │  <─────────────────────────────────│  Server   │
│          │         Response:                  │           │
│          │   {                                └───────────┘
│          │     accessToken: "eyJNEW...",      
│          │     refreshToken: "xyz789...",     
│          │     expiresIn: 900                 
│          │   }                                
└──────────┘                                    

Client update:
├─ old accessToken: "eyJhb..." → DELETE
├─ new accessToken: "eyJNEW..." (exp: 15 mins) → Store
├─ old refreshToken: "abc123..." → DELETE
└─ new refreshToken: "xyz789..." (exp: 7 days) → Store


Database AFTER TOKEN ROTATION:
┌──────────────────────────────────────────────┐
│ RefreshTokens Table                          │
├──────────────────────────────────────────────┤
│ Token: "abc123..." ← OLD TOKEN              │
│ UserId: 1                                    │
│ ExpiryDate: 2026-02-03                       │
│ Created: 2026-01-27                          │
│ IsRevoked: TRUE  ← DISABLED! ✗      │
├──────────────────────────────────────────────┤
│ Token: "xyz789..." ← NEW TOKEN               │
│ UserId: 1                                    │
│ ExpiryDate: 2026-02-03                       │
│ Created: 2026-01-27 16:00                    │
│ IsRevoked: FALSE ← ACTIVE ✓         │
└──────────────────────────────────────────────┘


CALL API WITH NEW ACCESS TOKEN
═══════════════════════════════════════════════════════════════════

┌──────────┐                                    ┌──────────┐
│  Client  │   GET /api/user/profile (retry)    │  Server  │
│          │─────────────────────────────────>  │          │
│          │  Header:                           │          │
│          │   Authorization: Bearer eyJNEW...  │  ✓ Verify│
│          │                                    │  ✓ Valid │
│          │  <─────────────────────────────────│  ✓ Fresh!│
│          │         200 OK                     │          │
│          │    {user data...}                  │          │
└──────────┘                                    └──────────┘

