namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Os eixos que o administrador informa ao alterar a definição de um fato que cadastrou (ADR-0136).
/// O eixo que o tipo do fato não aceita vem <see langword="null"/>: o derivado por regra infere a
/// cardinalidade e a fonte do domínio, e o agregado infere o domínio, a cardinalidade, a fonte e o
/// escopo do fato de membro.
/// </summary>
public sealed record DefinicaoDoFato(
    DominioFato? Dominio,
    CardinalidadeFato? Cardinalidade,
    FonteValoresFato? FonteValores,
    FormatoTexto? Formato,
    string PontoResolucao,
    EscopoFato? Escopo,
    ClassificacaoDeProtecao ClassificacaoProtecao,
    string FinalidadeTratamento,
    HipoteseLegalTratamento HipoteseLegal);
