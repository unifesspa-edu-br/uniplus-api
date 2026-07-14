namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Kernel.Results;
using Domain.ValueObjects;

/// <summary>
/// Define (ou remove) o bônus regional do processo (RN05, Story #774,
/// modelagem P-B §2.5). Passar <see langword="null"/> em
/// <see cref="RegraCodigo"/> remove o bônus — a ausência já é o toggle "sem
/// bônus" (INV-B5); não existe um "BONUS-NENHUM".
/// </summary>
public sealed record DefinirBonusRegionalCommand(
    Guid ProcessoSeletivoId,
    string? RegraCodigo,
    string? RegraVersao,
    decimal? Fator,
    decimal? Teto,
    string? MunicipioConvenio,
    string? BaseLegal,
    PrecondicaoIfMatch Precondicao) : ICommand<Result<MutacaoAceita>>;
