namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Decisao;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// O catálogo de outra implantação: o da aplicação com a classe ou o escopo de uma permissão trocados, aplicado pela
/// mesma conciliação que as migrations executam. Reproduz uma implantação anterior ou posterior sem mexer no catálogo
/// real do host.
/// </summary>
internal static class CatalogoAlternativoDeTeste
{
    public static CatalogoDaAplicacao Com(
        CatalogoDaAplicacao real, string codigo, ClassificacaoDeProtecao classificacao, bool? deUnidade = null)
    {
        List<DefinicaoDePermissao> definicoes = [.. real.Definicoes.Select(d => d.Codigo == codigo ? Ajustada(d, classificacao, deUnidade) : d)];

        return new CatalogoDaAplicacao([new FonteFixa(definicoes)]);
    }

    /// <summary>Aplica o catálogo dado, como a etapa posterior das migrations de uma instância que o tenha.</summary>
    public static async Task ImplantarAsync(IServiceProvider servicos, CatalogoDaAplicacao catalogo, TimeProvider? relogio = null)
    {
        await using AsyncServiceScope escopo = servicos.CreateAsyncScope();
        ConciliacaoDoCatalogo conciliacao = new(
            escopo.ServiceProvider.GetRequiredService<AutorizacaoDbContext>(),
            catalogo,
            new CatalogoDePermissoesDaAplicacao(catalogo),
            relogio ?? escopo.ServiceProvider.GetRequiredService<TimeProvider>(),
            NullLogger<ConciliacaoDoCatalogo>.Instance);

        await conciliacao.ExecutarAsync(CancellationToken.None);
    }

    private static DefinicaoDePermissao Ajustada(DefinicaoDePermissao d, ClassificacaoDeProtecao classificacao, bool? deUnidade)
    {
        bool unidade = deUnidade ?? d.DeUnidade;
        PermissionRequirement requisito = PermissionRequirement.From(
            d.Codigo,
            classificacao,
            d.Requisito.BaseLegalPadrao,
            d.Requisito.RequerMfa,
            d.Requisito.RequerDuplaAprovacao,
            unidade ? [CamposDeContexto.UnidadeProprietariaId] : [],
            d.Requisito.VerificacoesDeContexto).Value!;

        return d with { Requisito = requisito };
    }

    private sealed class FonteFixa(IReadOnlyList<DefinicaoDePermissao> definicoes) : IFonteDeCatalogo
    {
        public Type TipoDoEnum => typeof(CatalogoAlternativoDeTeste);

        public IReadOnlyList<DefinicaoDePermissao> Definicoes() => definicoes;
    }
}
