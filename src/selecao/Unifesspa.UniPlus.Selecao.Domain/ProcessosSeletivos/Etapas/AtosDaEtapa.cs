namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>O que a etapa declara publicar e decidir, como valores: cada produto é o par de ato e papel, com a posição em que foi declarado.</summary>
public sealed record AtosDaEtapa
{
    internal AtosDaEtapa(PublicacoesDaEtapa publicacoes)
    {
        EmiteParecerIndividual = publicacoes.EmiteParecerIndividual;
        TiposDeDecisao = TiposDeDecisaoDeclarados.Canonicos(publicacoes.TiposDeDecisao);
        TemRecursos = publicacoes.Recursos.Count > 0;
        TemRecursoEmAto = publicacoes.Recursos.Any(static recurso => recurso.Ancora == AncoraDoRecurso.AtoPublicado);
        Produtos = [.. publicacoes.Produtos.Select(static (produto, posicao) => (posicao, produto.AtoCodigo, produto.Papel))];
    }

    public bool EmiteParecerIndividual { get; }

    public IReadOnlyList<string> TiposDeDecisao { get; }

    public bool TemRecursos { get; }

    public bool TemRecursoEmAto { get; }

    public IReadOnlyList<(int Posicao, string AtoCodigo, PapelProdutoFase? Papel)> Produtos { get; }

    public bool TemProdutos => Produtos.Count > 0;

    public IReadOnlyList<string> TiposComCicloAberto() =>
        CicloRecursal.TiposComCicloAberto(Produtos.Select(static produto => (produto.AtoCodigo, produto.Papel)));
}
