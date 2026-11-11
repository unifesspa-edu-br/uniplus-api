namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// A decisão individual a que um motivo serve (UNI-REQ-0283). Vocabulário fechado: o tipo
/// separa as listas do cadastro único de motivos, e o código do motivo só é único dentro
/// dele. O recurso não é tipo próprio: o motivo do recurso é o da decisão recorrida.
/// </summary>
/// <remarks>
/// <see cref="Nenhum"/> é a sentinela de "não informado", e não um sétimo tipo — existe para
/// que a ausência ou o código desconhecido no wire cheguem ao domínio como ausência, em vez de
/// virar silenciosamente o primeiro valor do enum.
/// </remarks>
public enum TipoMotivoDeDecisao
{
    Nenhum = 0,
    IsencaoDaTaxa = 1,
    HomologacaoDasInscricoes = 2,
    AnaliseDocumental = 3,
    Heteroidentificacao = 4,
    AvaliacaoBiopsicossocial = 5,
    Classificacao = 6,
}
