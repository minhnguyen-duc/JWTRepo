# JWT Authentication Demo - ASP.NET Core

Complete implementation of JWT Authentication with Refresh Token Rotation.

## 🎯 Features

✅ **JWT Access Token** (15 minutes expiry)  
✅ **Refresh Token** (7 days expiry)  
✅ **Token Rotation** (security best practice)  
✅ **Role-based Authorization** (Admin, User)  
✅ **Secure Password Hashing** (BCrypt)  
✅ **Token Revocation** (logout functionality)  
✅ **Entity Framework Core** (Code First)  
✅ **Swagger UI** (API documentation)  

---

## 📋 Prerequisites

- .NET 8.0 SDK
-  Postgresql (V18.1.2)
- Visual Studio 2022 or VS Code

---

## 🚀 Setup Instructions

### 1. Clone/Download the Project

```bash
cd JwtAuthDemo
```

### 2. Restore NuGet Packages

```bash
dotnet restore
```

### 3. Update Connection String (if needed)

Edit `appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=JwtAuthDb;Username=postgres;Password=admin123"
}
```

### 4. Apply Database Migrations

```bash
# Create initial migration
dotnet ef migrations add InitialCreate

# Update database
dotnet ef database update
```

### 5. Run the Application

```bash
dotnet run
```

The API will be available at:
- HTTPS: `https://localhost:7XXX`
- HTTP: `http://localhost:5XXX`
- Swagger UI: `https://localhost:7XXX/` (root path)

---

## 🔐 Default Test Users

After migration, these users are automatically seeded:

| Username | Password    | Role  |
|----------|-------------|-------|
| admin    | password123 | Admin |
| user     | password123 | User  |

---

## 📡 API Endpoints

### 🔓 Public Endpoints (No Authentication Required)

#### 1. **Login**
```http
POST /api/auth/login
Content-Type: application/json

{
  "username": "admin",
  "password": "password123"
}
```

**Response:**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "YWJjZGVmZ2hpamtsbW5vcHFyc3R1dnd4eXo=",
  "accessTokenExpiry": "2026-01-28T10:15:00Z",
  "refreshTokenExpiry": "2026-02-04T09:00:00Z",
  "username": "admin",
  "role": "Admin"
}
```

#### 2. **Register** (Create New User)
```http
POST /api/auth/register
Content-Type: application/json

{
  "username": "newuser",
  "password": "mypassword123",
  "role": "User"
}
```

#### 3. **Refresh Token** (Get New Access Token)
```http
POST /api/auth/refresh
Content-Type: application/json

{
  "refreshToken": "YWJjZGVmZ2hpamtsbW5vcHFyc3R1dnd4eXo="
}
```

**Response:**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "bmV3LXJlZnJlc2gtdG9rZW4taGVyZQ==",
  "accessTokenExpiry": "2026-01-28T10:30:00Z",
  "refreshTokenExpiry": "2026-02-04T09:15:00Z"
}
```

---

### 🔒 Protected Endpoints (Requires Access Token)

#### 4. **Get Current User Info**
```http
GET /api/auth/me
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

**Response:**
```json
{
  "id": 1,
  "username": "admin",
  "role": "Admin",
  "createdAt": "2026-01-27T09:00:00Z"
}
```

#### 5. **Logout** (Revoke Refresh Token)
```http
POST /api/auth/logout
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
Content-Type: application/json

{
  "refreshToken": "YWJjZGVmZ2hpamtsbW5vcHFyc3R1dnd4eXo="
}
```

---

### 👑 Admin-Only Endpoints

#### 6. **Get All Users** (Admin Role Required)
```http
GET /api/auth/admin/users
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

**Response:**
```json
[
  {
    "id": 1,
    "username": "admin",
    "role": "Admin",
    "createdAt": "2026-01-27T09:00:00Z"
  },
  {
    "id": 2,
    "username": "user",
    "role": "User",
    "createdAt": "2026-01-27T09:00:00Z"
  }
]
```

---

## 🔄 Token Refresh Flow (Automatic)

```
User Login
    ↓
Receive: Access Token (15 min) + Refresh Token (7 days)
    ↓
Use Access Token for API calls
    ↓
Access Token Expires (after 15 min)
    ↓
API returns 401 Unauthorized
    ↓
Client automatically calls /refresh endpoint
    ↓
Sends: Old Refresh Token
    ↓
Receives: New Access Token + New Refresh Token
    ↓
Old Refresh Token is REVOKED (Token Rotation)
    ↓
Continue using new tokens
```

---

## 🛡️ Security Features

### 1. **Token Rotation**
Every time a refresh token is used, it's immediately revoked and a new one is issued.

### 2. **Stolen Token Detection**
If a revoked refresh token is used, all tokens for that user are revoked (possible security breach).

### 3. **Password Hashing**
Passwords are hashed using BCrypt (salted + cost factor 10).

### 4. **Short-lived Access Tokens**
Access tokens expire in 15 minutes to minimize risk if stolen.

### 5. **Secure Token Storage**
Refresh tokens are stored in database with:
- User association
- Expiry date
- Revocation status
- Creation timestamp

---

## 🧪 Testing with Postman/cURL

### Example: Complete Flow

```bash
# 1. Login
curl -X POST https://localhost:7XXX/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"password123"}'

# 2. Use Access Token
curl -X GET https://localhost:7XXX/api/auth/me \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"

# 3. Refresh Token (after 15 minutes)
curl -X POST https://localhost:7XXX/api/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{"refreshToken":"YOUR_REFRESH_TOKEN"}'

# 4. Logout
curl -X POST https://localhost:7XXX/api/auth/logout \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"refreshToken":"YOUR_REFRESH_TOKEN"}'
```

---

## 📁 Project Structure

```
JwtAuthDemo/
├── Controllers/
│   └── AuthController.cs          # Login, Refresh, Logout endpoints
├── Models/
│   ├── User.cs                    # User entity
│   └── RefreshToken.cs            # Refresh token entity
├── DTOs/
│   └── AuthDTOs.cs                # Request/Response models
├── Services/
│   ├── ITokenService.cs           # Token service interface
│   └── TokenService.cs            # JWT generation & validation
├── Data/
│   └── ApplicationDbContext.cs    # EF Core DbContext
├── Configurations/
│   └── JwtSettings.cs             # JWT settings model
├── appsettings.json               # Configuration
└── Program.cs                     # Application setup
```

---

## ⚙️ Configuration Options

Edit `appsettings.json`:

```json
"JwtSettings": {
  "Secret": "Your-256-bit-secret-key-here",
  "Issuer": "YourAppName",
  "Audience": "YourAppUsers",
  "AccessTokenExpiryMinutes": 15,    // Change access token lifetime
  "RefreshTokenExpiryDays": 7        // Change refresh token lifetime
}
```

---

## 🐛 Common Issues

### Issue: Database Connection Failed
**Solution:** Update connection string in `appsettings.json`

### Issue: Migrations Not Applied
**Solution:** Run `dotnet ef database update`

### Issue: 401 Unauthorized
**Solution:** Check if token is expired or invalid

### Issue: 403 Forbidden
**Solution:** User doesn't have required role (e.g., Admin)

---

## 📚 Learn More

- [JWT Official Website](https://jwt.io/)
- [ASP.NET Core Authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/)
- [OAuth 2.0 Refresh Tokens](https://oauth.net/2/refresh-tokens/)

---

## 📝 License

This project is for educational purposes.

---

## 👨‍💻 Author

Implementation based on industry best practices for JWT authentication with refresh token rotation.
