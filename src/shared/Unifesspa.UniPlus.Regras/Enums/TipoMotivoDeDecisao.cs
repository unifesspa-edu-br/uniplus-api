namespace Unifesspa.UniPlus.Regras.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// A decisão individual a que um motivo serve (UNI-REQ-0283). Vocabulário fechado: o tipo
/// separa as listas do cadastro único de motivos, e o código do motivo só é único dentro
/// dele. O recurso não é tipo próprio: o motivo do recurso é o da decisão recorrida. Gabarito da prova e
/// nota da etapa são decisões de avaliação, cada uma com os seus motivos.
/// </summary>
[DescricaoDoContrato("A decisão individual a que um motivo serve. O tipo separa as listas do cadastro de motivos, e o código do motivo só é único dentro dele. O recurso não é tipo próprio: o motivo do recurso é o da decisão recorrida.")]
public enum TipoMotivoDeDecisao
{
    [CodigoDeContrato("ISENCAO_DA_TAXA", "Isenção da taxa")]
    IsencaoDaTaxa = 1,

    [CodigoDeContrato("HOMOLOGACAO_DAS_INSCRICOES", "Homologação das inscrições")]
    HomologacaoDasInscricoes = 2,

    [CodigoDeContrato("ANALISE_DOCUMENTAL", "Análise documental")]
    AnaliseDocumental = 3,

    [CodigoDeContrato("HETEROIDENTIFICACAO", "Heteroidentificação")]
    Heteroidentificacao = 4,

    [CodigoDeContrato("AVALIACAO_BIOPSICOSSOCIAL", "Avaliação biopsicossocial")]
    AvaliacaoBiopsicossocial = 5,

    [CodigoDeContrato("CLASSIFICACAO", "Classificação")]
    Classificacao = 6,

    [CodigoDeContrato("GABARITO_DA_PROVA", "Gabarito da prova")]
    GabaritoDaProva = 7,

    [CodigoDeContrato("NOTA_DA_ETAPA", "Nota da etapa")]
    NotaDaEtapa = 8,
}
