namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Declara se o processo aplica o bônus regional (RN05, Story #774) e, quando aplica, a
/// configuração do bônus. <see cref="Aplica"/> falso dispensa a configuração; verdadeiro a exige.
/// </summary>
/// <param name="Aplica">Declaração obrigatória: o processo aplica ou não o bônus regional.</param>
/// <param name="FatoElegibilidadeCodigo">O fato booleano do processo que decide quem recebe o acréscimo (ADR-0142).</param>
public sealed record DefinirBonusRegionalCommand(
    Guid ProcessoSeletivoId,
    bool Aplica,
    string? RegraCodigo,
    string? RegraVersao,
    decimal? Fator,
    decimal? Teto,
    string? FatoElegibilidadeCodigo,
    Guid? BaseLegalBonusRegionalId,
    PrecondicaoIfMatch Precondicao) : ICommand<Result<MutacaoAceita>>;
