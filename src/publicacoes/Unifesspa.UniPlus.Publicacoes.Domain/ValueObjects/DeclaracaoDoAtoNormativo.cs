namespace Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

/// <summary>
/// O que o pedido de registro declara do ato antes de o tipo ser resolvido no catálogo: a numeração, o
/// código do tipo, a data que o documento declara, o hash do documento, o assinante, a versão do objeto, a retificação, os vínculos e os
/// anexos. Os textos podem chegar nulos, e o domínio os trata como vazios.
/// </summary>
public sealed record DeclaracaoDoAtoNormativo
{
    public required NumeracaoDoAto Numeracao { get; init; }

    public required string? TipoCodigo { get; init; }

    /// <summary>A data que o documento declara; ausente é recusada como obrigatória.</summary>
    public required DateOnly? DataDoAto { get; init; }

    public required string? DocumentoHash { get; init; }

    public required string? Assinante { get; init; }

    public required Guid? VersaoDoObjetoId { get; init; }

    public required string? VersaoDoObjetoHash { get; init; }

    public required RelacaoDeRetificacao Retificacao { get; init; }

    /// <summary>Os vínculos na ordem do pedido; um elemento nulo é recusado na posição dele.</summary>
    public required IReadOnlyList<EntidadeVinculada?> Vinculos { get; init; }

    /// <summary>Os anexos na ordem do pedido; um elemento nulo é recusado na posição dele.</summary>
    public required IReadOnlyList<(string? Nome, string? TipoMidia, string? Hash)?> Anexos { get; init; }
}
