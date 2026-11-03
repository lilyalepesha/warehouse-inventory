namespace Warehouse.Api.Domain;

/// <summary>Ошибка бизнес-правила, которую нужно вернуть клиенту как 4xx.</summary>
public class DomainException : Exception
{
    public int StatusCode { get; }

    public DomainException(string message, int statusCode = StatusCodes.Status400BadRequest)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
