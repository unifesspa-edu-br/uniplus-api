namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence;

/// <summary>
/// Semeia no banco de teste os tipos de ato que a publicação e a retificação usam.
/// </summary>
/// <remarks>
/// O catálogo real é semeado por fora (<c>tools/seeds/run.sh</c>, via Newman) e não
/// alcança o banco efêmero dos testes. Sem estes dois tipos, toda publicação continuaria
/// respondendo 204 — a publicação não depende de Publicações aceitar o ato (ADR-0108) —,
/// mas o ato correspondente cairia na dead letter, e a suíte esconderia isso.
/// </remarks>
internal static class TiposDeAtoSeeder
{
    public static async Task SemearAsync(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        PublicacoesDbContext db = scope.ServiceProvider.GetRequiredService<PublicacoesDbContext>();

        await GarantirAsync(db, DadosDoAtoDeTeste.TipoAbertura, "Edital de abertura", unicoPorObjeto: true);
        await GarantirAsync(db, DadosDoAtoDeTeste.TipoRetificacao, "Edital de retificação", unicoPorObjeto: false);

        await db.SaveChangesAsync();
    }

    private static async Task GarantirAsync(PublicacoesDbContext db, string codigo, string nome, bool unicoPorObjeto)
    {
        if (await db.Set<TipoAtoPublicado>().AnyAsync(t => t.Codigo == codigo))
        {
            return;
        }

        TipoAtoPublicado tipo = TipoAtoPublicado.Criar(
            codigo,
            nome,
            congelaConfiguracao: true,
            unicoPorObjeto,
            efeitoIrreversivel: false,
            new DateOnly(2020, 1, 1),
            vigenciaFim: null,
            baseLegal: null).Value!;

        await db.Set<TipoAtoPublicado>().AddAsync(tipo);
    }
}
