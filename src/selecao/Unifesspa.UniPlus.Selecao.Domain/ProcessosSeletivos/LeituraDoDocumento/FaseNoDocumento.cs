namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Uma fase como o documento a traz. A posição é a da fase na lista enviada, e a ordem dela é essa posição mais 1: o documento não
/// tem <c>ordem</c>. A chave é a que o autor escolheu e as outras partes citam.
/// </summary>
public sealed record FaseNoDocumento(
    int PosicaoNoDocumento,
    string Chave,
    ReferenciasDaFaseNoDocumento Referencias,
    JanelaDeTempo Janela,
    PublicacoesNoDocumento Publicacoes,
    IReadOnlyList<BancaNoDocumento> Bancas,
    RegraDeRecursoNoDocumento? RegraDeRecurso)
{
    /// <summary>A fase acontece no mesmo momento da fase que a antecede na lista; o documento sem o campo vale sem a marca.</summary>
    public bool NoMesmoMomentoDaAnterior { get; init; }
}

/// <summary>O que a fase cita por código: a fase canônica e a unidade que responde por ela.</summary>
public sealed record ReferenciasDaFaseNoDocumento(string FaseCanonicaCodigo, string? UnidadeResponsavelCodigo);

/// <summary>O que a fase publica e decide: os atos, o parecer individual, os tipos de decisão e a regra de homologação das inscrições.</summary>
public sealed record PublicacoesNoDocumento(
    IReadOnlyList<ProdutoNoDocumento> Produtos,
    bool EmiteParecerIndividual,
    IReadOnlyList<string?>? TiposDeDecisao,
    RegraDeHomologacaoNoDocumento? RegraDeHomologacao);

/// <summary>Um ato que a fase publica, com a posição dele na lista de produtos enviada; o papel ausente é nulo.</summary>
public sealed record ProdutoNoDocumento(int PosicaoNoDocumento, string AtoCodigo, PapelProdutoFase? Papel);
