namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

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
        TiposDeDecisao = TiposDeDecisaoDeclarados.Canonicos(publicacoes.TiposDeDecisao);
        RegraDeHomologacao = publicacoes.RegraDeHomologacao;
        Produtos = [.. publicacoes.Produtos.Select(static (produto, posicao) => (posicao, produto.AtoCodigo, produto.Papel))];
    }

    public bool EmiteParecerIndividual { get; }

    public bool AdmiteRecurso { get; }

    public IReadOnlyList<string> TiposDeDecisao { get; }

    public RegraDeHomologacaoDasInscricoes? RegraDeHomologacao { get; }

    public IReadOnlyList<(int Posicao, string AtoCodigo, PapelProdutoFase? Papel)> Produtos { get; }

    public bool ProduzResultado => CicloRecursal.ProduzResultado(ParesDeAto);

    public bool PublicaResultadoPreliminar => CicloRecursal.PublicaComPapel(ParesDeAto, PapelProdutoFase.Preliminar);

    public bool PublicaResultadoDefinitivo => CicloRecursal.PublicaComPapel(ParesDeAto, PapelProdutoFase.Definitivo);

    public bool PublicaResultadoDefinitivoDe(string atoCodigo) => CicloRecursal.PublicaDefinitivoDe(ParesDeAto, atoCodigo);

    public IReadOnlyList<string> TiposComCicloAberto() => CicloRecursal.TiposComCicloAberto(ParesDeAto);

    public IReadOnlyList<string> TiposFechadosNaPropriaFase() => CicloRecursal.TiposFechadosNoProprioLugar(ParesDeAto);

    private IEnumerable<(string AtoCodigo, PapelProdutoFase? Papel)> ParesDeAto =>
        Produtos.Select(static produto => (produto.AtoCodigo, produto.Papel));
}
