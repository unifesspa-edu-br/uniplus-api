namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Corpo da alteração da definição de um fato do administrador. O eixo que o tipo do fato não
/// aceita não se informa. Por tipo: o fato declarado envia domínio, cardinalidade, fonte dos valores
/// (categórico), formato (texto) e escopo; o derivado por regra envia domínio (booleano ou
/// categórico) e escopo, e não envia cardinalidade, fonte nem formato; o agregado não envia domínio,
/// cardinalidade, fonte, formato nem escopo. Em todo tipo se enviam o ponto de resolução, a
/// classificação de proteção, a finalidade e a hipótese legal.
/// </summary>
public sealed record DefinicaoDoFatoInput(
    string PontoResolucao,
    string ClassificacaoProtecao,
    string FinalidadeTratamento,
    string HipoteseLegal,
    [property: AceitaCodigosDe<TipoDoDado>] string? Dominio = null,
    [property: AceitaCodigosDe<QuantidadeDeValores>] string? Cardinalidade = null,
    [property: AceitaCodigosDe<FonteValoresFato>] string? FonteValores = null,
    string? Formato = null,
    string? Escopo = null);
