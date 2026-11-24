namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Os eixos que o administrador informa ao alterar a definição de um fato que cadastrou (ADR-0136).
/// O eixo que o tipo do fato não aceita vem ausente: o derivado por regra infere a
/// cardinalidade e a fonte do domínio, e o agregado infere o domínio, a cardinalidade, a fonte e o
/// escopo do fato de membro. O código que não é do vocabulário vem como desconhecido, e quem confere
/// a definição recusa cada eixo com o código de erro do próprio campo.
/// </summary>
public sealed record DefinicaoDoFato(
    CodigoInformado<TipoDoDado> Dominio,
    CodigoInformado<QuantidadeDeValores> Cardinalidade,
    CodigoInformado<FonteValoresFato> FonteValores,
    CodigoInformado<FormatoTexto> Formato,
    string PontoResolucao,
    CodigoInformado<EscopoFato> Escopo,
    CodigoInformado<ClassificacaoDeProtecao> ClassificacaoProtecao,
    string FinalidadeTratamento,
    CodigoInformado<HipoteseLegalTratamento> HipoteseLegal);
