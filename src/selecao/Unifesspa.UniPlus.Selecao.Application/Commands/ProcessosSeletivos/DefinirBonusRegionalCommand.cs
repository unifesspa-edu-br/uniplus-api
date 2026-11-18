namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Declara se o processo aplica o bônus regional (Fator de Atenuação das Desigualdades de Acesso, UNI-REQ-0015, Story #774) e, quando aplica, a
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
    Guid? BaseLegalBonusRegionalId) : ICommand<Result>
{
    /// <summary>
    /// Qualquer campo do bônus enviado: com <see cref="Aplica"/> falso ele contradiz a declaração, e descartá-lo em silêncio
    /// esconderia um payload mal formado.
    /// </summary>
    public bool TemConfiguracaoDoBonus =>
        RegraCodigo is not null || RegraVersao is not null || Fator is not null
        || Teto is not null || FatoElegibilidadeCodigo is not null || BaseLegalBonusRegionalId is not null;
}
