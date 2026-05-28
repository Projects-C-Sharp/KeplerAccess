using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AccessKepler.Models;
using Microsoft.AspNetCore.Mvc;

namespace AccessKepler.Controllers;

public class HomeController : Controller
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HomeController> _logger;

    // Roles que tienen permiso para acceder a esta app
    private static readonly HashSet<string> AllowedRoles =
        new(StringComparer.OrdinalIgnoreCase) { "Admin", "Scanner" };

    public HomeController(IHttpClientFactory httpClientFactory, ILogger<HomeController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger            = logger;
    }

    // ─── GET / ────────────────────────────────────────────────────────────────

    public IActionResult Index()
    {
        var token = Request.Cookies["access_token"];
        if (!string.IsNullOrEmpty(token))
            return RedirectToAction("Scanner");

        return View("Login");
    }

    // ─── POST /login ──────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login([FromForm] LoginRequest model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Error = "Por favor completa todos los campos.";
            return View("Login");
        }

        try
        {
            var client  = _httpClientFactory.CreateClient("CentralApi");
            var payload = JsonSerializer.Serialize(new { email = model.Email, password = model.Password });
            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            var response     = await client.PostAsync("/api/auth/login", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            // La API devuelve 401 con texto plano en credenciales inválidas
            if (!response.IsSuccessStatusCode)
            {
                ViewBag.Error = "Credenciales inválidas. Verifica tu correo y contraseña.";
                return View("Login");
            }

            var loginResponse = JsonSerializer.Deserialize<ApiLoginResponse>(responseBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (string.IsNullOrEmpty(loginResponse?.AccessToken))
            {
                ViewBag.Error = "No se pudo obtener el token de acceso.";
                return View("Login");
            }

            // ── Decodificar JWT para extraer rol y nombre ──────────────────
            var handler = new JwtSecurityTokenHandler();
            JwtSecurityToken jwt;
            try
            {
                jwt = handler.ReadJwtToken(loginResponse.AccessToken);
            }
            catch
            {
                ViewBag.Error = "Token inválido recibido del servidor.";
                return View("Login");
            }

            // Verificar rol: solo Admin y Scanner pueden entrar
            var roles = jwt.Claims
                .Where(c => c.Type == ClaimTypes.Role || c.Type == "role")
                .Select(c => c.Value)
                .ToList();

            var hasAccess = roles.Any(r => AllowedRoles.Contains(r));
            if (!hasAccess)
            {
                ViewBag.Error = "Acceso denegado. Solo personal autorizado (Admin / Scanner) puede ingresar.";
                return View("Login");
            }

            // Obtener nombre del usuario desde el token
            var employeeName =
                jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name || c.Type == "name")?.Value
                ?? jwt.Claims.FirstOrDefault(c => c.Type == "unique_name")?.Value
                ?? model.Email;

            // Persistir tokens en cookies seguras
            var cookieOptions = new CookieOptions
            {
                HttpOnly  = true,
                Secure    = true,
                SameSite  = SameSiteMode.Strict,
                Expires   = DateTimeOffset.UtcNow.AddHours(12)
            };

            Response.Cookies.Append("access_token",   loginResponse.AccessToken,  cookieOptions);

            if (!string.IsNullOrEmpty(loginResponse.RefreshToken))
                Response.Cookies.Append("refresh_token", loginResponse.RefreshToken, cookieOptions);

            // El nombre lo expone sin HttpOnly para que el JS lo lea
            Response.Cookies.Append("employee_name", employeeName, new CookieOptions
            {
                HttpOnly = false,
                Secure   = true,
                SameSite = SameSiteMode.Strict,
                Expires  = DateTimeOffset.UtcNow.AddHours(12)
            });

            // Guardar rol (sin HttpOnly, el JS puede necesitarlo)
            var primaryRole = roles.FirstOrDefault(r => AllowedRoles.Contains(r)) ?? roles.FirstOrDefault() ?? "Scanner";
            Response.Cookies.Append("employee_role", primaryRole, new CookieOptions
            {
                HttpOnly = false,
                Secure   = true,
                SameSite = SameSiteMode.Strict,
                Expires  = DateTimeOffset.UtcNow.AddHours(12)
            });

            return RedirectToAction("Scanner");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login error");
            ViewBag.Error = "Error de conexión con el servidor. Intenta de nuevo.";
            return View("Login");
        }
    }

    // ─── GET /scanner ─────────────────────────────────────────────────────────

    public IActionResult Scanner()
    {
        var token = Request.Cookies["access_token"];
        if (string.IsNullOrEmpty(token))
            return RedirectToAction("Index");

        return View("Scanner");
    }

    // ─── POST /api/validate-qr  (llamado desde el JS del scanner) ─────────────

    [HttpPost]
    public async Task<IActionResult> ValidateQr([FromBody] ValidateQrRequest request)
    {
        var token = Request.Cookies["access_token"];
        if (string.IsNullOrEmpty(token))
            return Unauthorized(new { message = "Sesión expirada." });

        if (string.IsNullOrWhiteSpace(request?.QrCode))
            return BadRequest(new { message = "Código QR vacío." });

        try
        {
            var client = _httpClientFactory.CreateClient("CentralApi");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            // Mapear al contrato real de la API
            var apiRequest = new ValidateTicketRequest
            {
                QRCode     = request.QrCode,
                DeviceInfo = request.DeviceInfo ?? "AccessKepler-PWA"
            };

            var json    = JsonSerializer.Serialize(apiRequest,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response     = await client.PostAsync("/api/scanner/validate", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Scanner API response [{Status}]: {Body}",
                (int)response.StatusCode, responseBody);

            // La API envuelve la respuesta en ApiResponse<ValidateTicketResult>
            ValidateTicketResult? result = null;

            try
            {
                var apiResp = JsonSerializer.Deserialize<ApiValidateResponse>(responseBody,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                result = apiResp?.Data;
            }
            catch
            {
                // Intentar deserializar directo si no viene envuelto
                try
                {
                    result = JsonSerializer.Deserialize<ValidateTicketResult>(responseBody,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch { /* ignorar */ }
            }

            if (result == null)
            {
                return Ok(new ValidateTicketResponse
                {
                    IsValid    = false,
                    Status     = "error",
                    Message    = "Respuesta inesperada del servidor.",
                    AlertLevel = AlertLevel.Warning
                });
            }

            // Construir respuesta normalizada para el frontend
            var frontendResponse = BuildFrontendResponse(result);
            return Ok(frontendResponse);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "API connection error during QR validation");
            return StatusCode(503, new ValidateTicketResponse
            {
                IsValid    = false,
                Status     = "error",
                Message    = "Sin conexión con el servidor. Verifica tu red.",
                AlertLevel = AlertLevel.Warning
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during QR validation");
            return StatusCode(500, new ValidateTicketResponse
            {
                IsValid    = false,
                Status     = "error",
                Message    = "Error interno. Contacta soporte.",
                AlertLevel = AlertLevel.Danger
            });
        }
    }

    // ─── GET /api/stats ───────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Stats()
    {
        var token = Request.Cookies["access_token"];
        if (string.IsNullOrEmpty(token))
            return Unauthorized();

        try
        {
            var client = _httpClientFactory.CreateClient("CentralApi");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync("/api/scanner/history");
            var body     = await response.Content.ReadAsStringAsync();
            return Content(body, "application/json");
        }
        catch
        {
            return Ok(new ScanStats());
        }
    }

    // ─── POST /logout ─────────────────────────────────────────────────────────

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        var token = Request.Cookies["access_token"];

        // Intentar invalidar el token en la API (best-effort)
        if (!string.IsNullOrEmpty(token))
        {
            try
            {
                var client = _httpClientFactory.CreateClient("CentralApi");
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
                await client.PostAsync("/api/auth/logout", null);
            }
            catch { /* ignorar si falla */ }
        }

        Response.Cookies.Delete("access_token");
        Response.Cookies.Delete("refresh_token");
        Response.Cookies.Delete("employee_name");
        Response.Cookies.Delete("employee_role");

        return RedirectToAction("Index");
    }

    // ─── Error ────────────────────────────────────────────────────────────────

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel
        {
            RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static ValidateTicketResponse BuildFrontendResponse(ValidateTicketResult result)
    {
        TicketInfo? ticketInfo = null;

        if (result.Ticket != null)
        {
            var dt = result.Ticket.ShowtimeStart;
            ticketInfo = new TicketInfo
            {
                TicketId   = result.Ticket.TicketId.ToString(),
                EventName  = result.Ticket.EventName,
                EventDate  = dt.ToString("dd/MM/yyyy"),
                EventTime  = dt.ToString("HH:mm"),
                HolderName = result.Ticket.HolderEmail,
                Seat       = result.Ticket.SeatLabel,
                VenueName  = result.Ticket.VenueName,
                TicketType = "General"
            };
        }

        if (result.IsValid)
            return new ValidateTicketResponse
            {
                IsValid    = true,
                Status     = "valid",
                Message    = result.Message,
                Ticket     = ticketInfo,
                AlertLevel = AlertLevel.Success
            };

        // Determinar si es "ya usado" o simplemente "inválido"
        var status = result.Ticket?.WasAlreadyUsed == true ? "already_used" : "invalid";

        return new ValidateTicketResponse
        {
            IsValid    = false,
            Status     = status,
            Message    = result.Message,
            Ticket     = ticketInfo,
            AlertLevel = status == "already_used" ? AlertLevel.Warning : AlertLevel.Danger
        };
    }
}

// Request que llega desde el JS del scanner
public class ValidateQrRequest
{
    public string? QrCode     { get; set; }
    public string? DeviceInfo { get; set; }
}
