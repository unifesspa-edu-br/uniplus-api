namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;

using Kernel.Results;

/// <summary>O desfecho da conferência de um produto declarado pela fase.</summary>
internal abstract record ProdutoConferido
{
    /// <summary>O produto é coerente com o catálogo e entra na fase.</summary>
    internal sealed record Aceito(ProdutoDaFase Produto) : ProdutoConferido;

    /// <summary>O produto é recusado, e a recusa se soma às da coleção.</summary>
    internal sealed record Recusado(FieldError Recusa) : ProdutoConferido;
}
