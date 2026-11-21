namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Declara se o processo aplica o bônus regional (Fator de Atenuação das Desigualdades de Acesso, UNI-REQ-0015, Story #774) e, quando aplica, a
/// configuração do bônus. <see cref="Aplica"/> falso dispensa a configuração; verdadeiro a exige.
/// </summary>
/// <param name="Aplica">Declaração obrigatória: o processo aplica ou não o bônus regional.</param>
/// <param name="Regra">A regra de bônus do catálogo de regras.</param>
/// <param name="Fator">O fator multiplicativo do bônus.</param>
/// <param name="Teto">O teto do acréscimo, quando há.</param>
/// <param name="FatoElegibilidadeCodigo">O fato booleano do processo que decide quem recebe o acréscimo (ADR-0142).</param>
/// <param name="BaseLegal">A base legal do bônus regional, cujo snapshot é congelado no processo.</param>
public sealed record DefinirBonusRegionalCommand(
    Guid ProcessoSeletivoId,
    bool Aplica,
    ReferenciaRegraInput? Regra,
    decimal? Fator,
    decimal? Teto,
    string? FatoElegibilidadeCodigo,
    BaseLegalDoBonusInput? BaseLegal) : ICommand<Result>
{
    /// <summary>
    /// Qualquer campo do bônus enviado: com <see cref="Aplica"/> falso ele contradiz a declaração, e descartá-lo em silêncio
    /// esconderia um payload mal formado.
    /// </summary>
    public bool TemConfiguracaoDoBonus =>
        Regra is not null || Fator is not null || Teto is not null || FatoElegibilidadeCodigo is not null || BaseLegal is not null;
}

/// <summary>A base legal do bônus regional referenciada pela identidade; a leitura devolve o instrumento e os municípios congelados.</summary>
/// <param name="Id">Identificador da base legal de bônus regional.</param>
public sealed record BaseLegalDoBonusInput(Guid Id);
