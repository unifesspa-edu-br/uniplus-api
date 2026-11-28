namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Declara se o processo aplica o Fator de Atenuação das Desigualdades de Acesso (FADA, UNI-REQ-0015) e, quando aplica, a
/// configuração do FADA. <see cref="Aplica"/> falso dispensa a configuração; verdadeiro a exige.
/// </summary>
/// <param name="Aplica">Declaração obrigatória: o processo aplica ou não o FADA; ausente ou nulo é recusado (422).</param>
/// <param name="Regra">A regra do FADA do catálogo de regras.</param>
/// <param name="Fator">O multiplicador aplicado à nota.</param>
/// <param name="Teto">O teto do acréscimo, quando há.</param>
/// <param name="FatoElegibilidadeCodigo">O fato booleano do processo que decide quem recebe o acréscimo (ADR-0142).</param>
/// <param name="BaseLegal">A base legal do FADA, cujo snapshot é congelado no processo.</param>
public sealed record DefinirFadaCommand(
    Guid ProcessoSeletivoId,
    bool? Aplica,
    ReferenciaRegraInput? Regra,
    decimal? Fator,
    decimal? Teto,
    string? FatoElegibilidadeCodigo,
    BaseLegalDoFadaInput? BaseLegal) : ICommand<Result>
{
    /// <summary>
    /// Qualquer campo do FADA enviado: com <see cref="Aplica"/> falso ele contradiz a declaração, e descartá-lo em silêncio
    /// esconderia um payload mal formado.
    /// </summary>
    public bool TemConfiguracaoDoFada =>
        Regra is not null || Fator is not null || Teto is not null || FatoElegibilidadeCodigo is not null || BaseLegal is not null;
}

/// <summary>A base legal do FADA referenciada pela identidade; a leitura devolve o instrumento e os municípios congelados.</summary>
/// <param name="Id">Identificador da base legal do FADA.</param>
public sealed record BaseLegalDoFadaInput(Guid Id);
