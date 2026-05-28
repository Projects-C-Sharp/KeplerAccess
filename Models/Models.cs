using System.Text.Json.Serialization;

namespace AccessKepler.Models;

// ─── Login ────────────────────────────────────────────────────────────────────

public class LoginRequest
{
    public string Email    { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Respuesta real de POST /api/auth/login en la API central.
/// </summary>
public class ApiLoginResponse
{
    [JsonPropertyName("accessToken")]
    public string? AccessToken  { get; set; }

    [JsonPropertyName("refreshToken")]
    public string? RefreshToken { get; set; }

    // La API devuelve un string plano (no JSON) en caso de error.
    // Lo capturamos por separado en el controlador.
}

// ─── Scanner ──────────────────────────────────────────────────────────────────

/// <summary>
/// Body que envía AccessKepler a POST /api/scanner/validate.
/// </summary>
public class ValidateTicketRequest
{
    [JsonPropertyName("qRCode")]
    public string QRCode     { get; set; } = string.Empty;

    [JsonPropertyName("deviceInfo")]
    public string DeviceInfo { get; set; } = string.Empty;
}

/// <summary>
/// Respuesta de /api/scanner/validate envuelta en ApiResponse.
/// </summary>
public class ApiValidateResponse
{
    [JsonPropertyName("data")]
    public ValidateTicketResult? Data { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

public class ValidateTicketResult
{
    [JsonPropertyName("isValid")]
    public bool IsValid { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("ticket")]
    public TicketDetailDto? Ticket { get; set; }
}

public class TicketDetailDto
{
    [JsonPropertyName("ticketId")]
    public int TicketId { get; set; }

    [JsonPropertyName("holderEmail")]
    public string HolderEmail { get; set; } = string.Empty;

    [JsonPropertyName("eventName")]
    public string EventName { get; set; } = string.Empty;

    [JsonPropertyName("venueName")]
    public string VenueName { get; set; } = string.Empty;

    [JsonPropertyName("showtimeStart")]
    public DateTime ShowtimeStart { get; set; }

    [JsonPropertyName("seatLabel")]
    public string SeatLabel { get; set; } = string.Empty;

    [JsonPropertyName("wasAlreadyUsed")]
    public bool WasAlreadyUsed { get; set; }

    [JsonPropertyName("usedAt")]
    public DateTime? UsedAt { get; set; }
}

/// <summary>
/// Respuesta normalizada que se envía al frontend JS.
/// </summary>
public class ValidateTicketResponse
{
    [JsonPropertyName("isValid")]
    public bool IsValid { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty; // "valid" | "already_used" | "invalid" | "error"

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("ticket")]
    public TicketInfo? Ticket { get; set; }

    [JsonPropertyName("alertLevel")]
    public AlertLevel AlertLevel { get; set; }
}

public class TicketInfo
{
    [JsonPropertyName("ticketId")]
    public string TicketId { get; set; } = string.Empty;

    [JsonPropertyName("eventName")]
    public string EventName { get; set; } = string.Empty;

    [JsonPropertyName("eventDate")]
    public string EventDate { get; set; } = string.Empty;

    [JsonPropertyName("eventTime")]
    public string EventTime { get; set; } = string.Empty;

    [JsonPropertyName("holderName")]
    public string HolderName { get; set; } = string.Empty;

    [JsonPropertyName("seat")]
    public string Seat { get; set; } = string.Empty;

    [JsonPropertyName("venueName")]
    public string VenueName { get; set; } = string.Empty;

    [JsonPropertyName("ticketType")]
    public string TicketType { get; set; } = string.Empty;
}

public enum AlertLevel
{
    Success = 0,
    Warning = 1,
    Danger  = 2
}

// ─── Stats ────────────────────────────────────────────────────────────────────

public class ScanStats
{
    public int    TotalScanned     { get; set; }
    public int    ValidScans       { get; set; }
    public int    RejectedScans    { get; set; }
    public int    FraudAttempts    { get; set; }
    public string EventName        { get; set; } = string.Empty;
    public int    EventCapacity    { get; set; }
    public int    CurrentAttendees { get; set; }
}

// ─── Misc ─────────────────────────────────────────────────────────────────────

public class ErrorViewModel
{
    public string? RequestId { get; set; }
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
