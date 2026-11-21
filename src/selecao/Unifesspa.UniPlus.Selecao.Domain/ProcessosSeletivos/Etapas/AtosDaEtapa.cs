namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;

using Unifesspa.UniPlus.Regras.Enums;
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
        Recursos = [.. publicacoes.Recursos.Select(RecursoDeclarado.Da)];
        TemRecursoEmAto = publicacoes.Recursos.Any(static recurso => recurso.Ancora == AncoraDoRecurso.AtoPublicado);
        Produtos = [.. publicacoes.Produtos.Select(static (produto, posicao) => (posicao, produto.AtoCodigo, produto.Papel))];
    }

    public bool EmiteParecerIndividual { get; }

    public IReadOnlyList<TipoMotivoDeDecisao> TiposDeDecisao { get; }

    public bool TemRecursos { get; }

    /// <summary>Os recursos que a etapa abre, na ordem em que o campo <c>recursos[j]</c> os aponta.</summary>
    public IReadOnlyList<RecursoDeclarado> Recursos { get; }

    /// <summary>Se algum recurso da etapa admite instância superior.</summary>
    public bool TemRecursoComInstanciaSuperior => Recursos.Any(static recurso => recurso.AdmiteInstanciaSuperior);

    public bool TemRecursoEmAto { get; }

    public IReadOnlyList<(int Posicao, string AtoCodigo, PapelProdutoFase? Papel)> Produtos { get; }

    public bool TemProdutos => Produtos.Count > 0;

    public IReadOnlyList<string> TiposComCicloAberto() =>
        CicloRecursal.TiposComCicloAberto(Produtos.Select(static produto => (produto.AtoCodigo, produto.Papel)));
}
