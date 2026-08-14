namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Redeclara o município cujo calendário rege a contagem dos prazos do certame
/// (UNI-REQ-0111). Ao contrário da referência temporal de fatos, não há remoção por
/// ausência: a localidade existe desde a criação do processo e só pode ser trocada por
/// outra — um processo sem ela não é estado representável.
/// </summary>
public sealed record DefinirLocalidadeCommand(
    Guid ProcessoSeletivoId,
    string? CodigoIbge,
    string? Nome,
    string? Uf,
    PrecondicaoIfMatch Precondicao) : ICommand<Result<MutacaoAceita>>
{
    /// <summary>
    /// Nenhum dos três campos foi informado — distinto de trio informado e incoerente,
    /// que o Kernel nomeia por causa.
    /// </summary>
    public bool LocalidadeNaoDeclarada =>
        string.IsNullOrWhiteSpace(CodigoIbge)
        && string.IsNullOrWhiteSpace(Nome)
        && string.IsNullOrWhiteSpace(Uf);
}
