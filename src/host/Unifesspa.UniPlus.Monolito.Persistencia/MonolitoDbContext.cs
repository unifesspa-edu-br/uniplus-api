namespace Unifesspa.UniPlus.Monolito.Persistencia;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Discentes.Infrastructure.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Ingresso.Infrastructure.Persistence;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// O contexto único dos módulos internos do monólito (ADR-0170). A lista de contribuições é explícita e completa, e não vem
/// do DI: o EF guarda o modelo em cache por tipo de contexto, e uma composição parcial pegaria o modelo errado.
/// </summary>
public sealed class MonolitoDbContext : UniPlusDbContext
{
    /// <summary>Os módulos na ordem em que o modelo é montado e em que os interceptadores deles rodam.</summary>
    internal static IReadOnlyList<IContribuicaoDoModulo> ContribuicoesDoMonolito { get; } =
    [
        new ConfiguracaoContribuicao(),
        new OrganizacaoInstitucionalContribuicao(),
        new AutorizacaoContribuicao(),
        new SelecaoContribuicao(),
        new IngressoContribuicao(),
        new PublicacoesContribuicao(),
        new DiscentesContribuicao(),
    ];

    public MonolitoDbContext(DbContextOptions<MonolitoDbContext> options)
        : base(options)
    {
    }

    protected override IReadOnlyList<IContribuicaoDoModulo> Contribuicoes => ContribuicoesDoMonolito;
}
