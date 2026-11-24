namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>O que a fase declara publicar e decidir, como valores: cada produto é o par de ato e papel, com a posição em que foi declarado.</summary>
public sealed record AtosDaFase
{
    internal AtosDaFase(PublicacoesDaFase publicacoes)
    {
        EmiteParecerIndividual = publicacoes.EmiteParecerIndividual;
        AdmiteRecurso = publicacoes.RegraRecurso is not null;
        Recurso = publicacoes.RegraRecurso is { } regra ? RecursoDeclarado.Da(regra) : null;
        TiposDeDecisao = TiposDeDecisaoDeclarados.Canonicos(publicacoes.TiposDeDecisao);
        RegraDeHomologacao = publicacoes.RegraDeHomologacao;
        Produtos = [.. publicacoes.Produtos.Select(static (produto, posicao) => (posicao, produto.AtoCodigo, produto.Papel))];
        AtoDaAncoraDoRecurso = publicacoes.Produtos
            .FirstOrDefault(produto => produto.Id == publicacoes.RegraRecurso?.ProdutoAncoraId && produto.Papel == PapelDaPublicacaoPrevista.Preliminar)?.AtoCodigo;
    }

    public bool EmiteParecerIndividual { get; }

    public bool AdmiteRecurso { get; }

    /// <summary>O recurso que a fase admite; ausente quando a fase não admite recurso.</summary>
    public RecursoDeclarado? Recurso { get; }

    /// <summary>Se o recurso da fase admite instância superior; a fase sem recurso não admite.</summary>
    public bool AdmiteInstanciaSuperior => Recurso is { AdmiteInstanciaSuperior: true };

    public IReadOnlyList<TipoMotivoDeDecisao> TiposDeDecisao { get; }

    public RegraDeHomologacaoDasInscricoes? RegraDeHomologacao { get; }

    public IReadOnlyList<(int Posicao, string AtoCodigo, PapelDaPublicacaoPrevista? Papel)> Produtos { get; }

    /// <summary>O tipo de ato do produto preliminar em que a regra de recurso ancora o prazo; ausente quando a fase não admite recurso ou a âncora não é um preliminar da fase.</summary>
    public string? AtoDaAncoraDoRecurso { get; }

    public bool ProduzResultado => CicloRecursal.ProduzResultado(ParesDeAto);

    public bool PublicaResultadoPreliminar => CicloRecursal.PublicaComPapel(ParesDeAto, PapelDaPublicacaoPrevista.Preliminar);

    public bool PublicaResultadoDefinitivo => CicloRecursal.PublicaComPapel(ParesDeAto, PapelDaPublicacaoPrevista.Definitivo);

    /// <summary>Se a fase publica o tipo de ato com algum papel: é produto da fase, e não só um ato que ela cita.</summary>
    public bool PublicaComPapel(string atoCodigo) =>
        Produtos.Any(produto => produto.Papel is not null && string.Equals(produto.AtoCodigo, atoCodigo, StringComparison.Ordinal));

    public IReadOnlyList<string> TiposComCicloAberto() => CicloRecursal.TiposComCicloAberto(ParesDeAto);

    public IReadOnlyList<string> TiposFechadosNaPropriaFase() => CicloRecursal.TiposFechadosNoProprioLugar(ParesDeAto);

    private IEnumerable<(string AtoCodigo, PapelDaPublicacaoPrevista? Papel)> ParesDeAto =>
        Produtos.Select(static produto => (produto.AtoCodigo, produto.Papel));
}
