namespace Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>Códigos de erro da precondição de concorrência (<c>If-Match</c>) conferida pelo agregado.</summary>
public static class PrecondicaoErrorCodes
{
    /// <summary>A mutação exige o <c>If-Match</c> e a requisição não o trouxe; responde 428.</summary>
    public const string Requerida = "Precondicao.Requerida";

    /// <summary>O <c>If-Match</c> não casa com o estado atual do recurso; responde 412.</summary>
    public const string Falhou = "Precondicao.Falhou";
}
