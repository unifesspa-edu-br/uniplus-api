namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Enums;

using Kernel.Results;

/// <summary>O desfecho da conferência de um produto declarado por etapa ou por fase.</summary>
internal abstract record ProdutoConferido
{
    /// <summary>O produto é coerente com o catálogo e entra no dono que o declarou.</summary>
    internal sealed record Aceito(string AtoCodigo, PapelDaPublicacaoPrevista? Papel) : ProdutoConferido;

    /// <summary>O produto é recusado, e a recusa se soma às da coleção.</summary>
    internal sealed record Recusado(FieldError Recusa) : ProdutoConferido;
}
