namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;

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
    string? Dominio = null,
    string? Cardinalidade = null,
    string? FonteValores = null,
    string? Formato = null,
    string? Escopo = null);
