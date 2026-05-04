namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

/// <summary>Resultado da decodificação de um cursor opaco.</summary>
public enum CursorDecodeStatus
{
    /// <summary>Cursor decodificado com sucesso.</summary>
    Success = 0,

    /// <summary>Cursor adulterado, malformado, ou cifrado para outro recurso/chave.</summary>
    Invalid = 1,

    /// <summary>Cursor decifrado com sucesso, mas <see cref="CursorPayload.ExpiresAt"/> já passou.</summary>
    Expired = 2,
}
