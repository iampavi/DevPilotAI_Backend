namespace DevPilotAI.Shared.Common;

public class ApiResponse
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
    public IEnumerable<string> Errors { get; set; } = Array.Empty<string>();

    public static ApiResponse Success(string message = "") => new()
    {
        IsSuccess = true,
        Message = message
    };

    public static ApiResponse Failure(string message, IEnumerable<string> errors) => new()
    {
        IsSuccess = false,
        Message = message,
        Errors = errors
    };

    public static ApiResponse Failure(string message, string error) => new()
    {
        IsSuccess = false,
        Message = message,
        Errors = new[] { error }
    };
}

public class ApiResponse<T> : ApiResponse
{
    public T? Data { get; set; }

    public static ApiResponse<T> Success(T data, string message = "") => new()
    {
        IsSuccess = true,
        Message = message,
        Data = data
    };

    public new static ApiResponse<T> Failure(string message, IEnumerable<string> errors) => new()
    {
        IsSuccess = false,
        Message = message,
        Errors = errors
    };

    public new static ApiResponse<T> Failure(string message, string error) => new()
    {
        IsSuccess = false,
        Message = message,
        Errors = new[] { error }
    };
}
