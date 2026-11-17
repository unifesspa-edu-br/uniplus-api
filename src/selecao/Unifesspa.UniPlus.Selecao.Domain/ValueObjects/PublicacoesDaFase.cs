namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Entities;

using ProcessosSeletivos.HomologacaoDasInscricoes;

/// <summary>
/// O que a fase publica, o parecer individual que essas publicações sustentam e o prazo de recurso
/// ancorado num dos produtos. É entrada de fábrica de vida curta, superficialmente imutável: carrega
/// a coleção somente leitura e as instâncias de <see cref="ProdutoDaFase"/> e
/// <see cref="RegraRecursoFase"/> que a fase passa a possuir, e nunca é guardada pela entidade.
/// </summary>
/// <param name="TiposDeDecisao">
/// Os tipos de decisão que a fase declara produzir, como chegaram; a fábrica confere e a entidade
/// guarda a cópia canônica. Ausente ou vazio: a fase não declara nenhum.
/// </param>
/// <param name="RegraDeHomologacao">
/// A regra que a fase declara para homologar as inscrições; ausente quando a fase não homologa. Já validada
/// como valor, o resultado que ela aponta é conferido contra os produtos da fase por <c>ConferenciaDaRegraDeHomologacao</c>.
/// </param>
public sealed record PublicacoesDaFase(
    IReadOnlyList<ProdutoDaFase> Produtos,
    bool EmiteParecerIndividual,
    RegraRecursoFase? RegraRecurso,
    IReadOnlyList<string?>? TiposDeDecisao = null,
    RegraDeHomologacaoDasInscricoes? RegraDeHomologacao = null);
