namespace Warehouse.Api.Domain;

public enum WriteOffReason
{
    /// <summary>Продажа покупателю.</summary>
    Sale = 1,

    /// <summary>Списание по истечении срока годности.</summary>
    Expiry = 2,

    /// <summary>Порча, бой, недостача.</summary>
    Damage = 3
}
