namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Semeia no banco de teste os tipos de ato que a publicação, a retificação, o aviso e o resultado usam.
/// </summary>
/// <remarks>
/// O catálogo real é semeado por fora (<c>tools/seeds/run.sh</c>, via Newman) e não
/// alcança o banco efêmero dos testes. Sem estes tipos, toda publicação continuaria
/// respondendo 204 — a publicação não depende de Publicações aceitar o ato (ADR-0108) —,
/// mas o ato correspondente cairia na dead letter, e a suíte esconderia isso.
/// </remarks>
internal static class TiposDeAtoSeeder
{
    public static async Task SemearAsync(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        await GarantirAsync(db, DadosDoAtoDeTeste.TipoAbertura, "Edital de abertura", unicoPorObjeto: true);
        await GarantirAsync(db, DadosDoAtoDeTeste.TipoRetificacao, "Edital de retificação", unicoPorObjeto: false);
        await GarantirAsync(db, DadosDoAtoDeTeste.TipoAviso, "Aviso", unicoPorObjeto: false, congelaConfiguracao: false, publicavelComoAvulso: true);
        await GarantirAsync(db, DadosDoAtoDeTeste.TipoAvulsoUnico, "Comunicado único", unicoPorObjeto: true, congelaConfiguracao: false, publicavelComoAvulso: true);
        await GarantirAsync(db, DadosDoAtoDeTeste.TipoResultado, "Resultado da homologação", unicoPorObjeto: false, congelaConfiguracao: false, ehResultado: true);
        await GarantirAsync(db, DadosDoAtoDeTeste.TipoResultadoDaClassificacao, "Resultado final", unicoPorObjeto: false, congelaConfiguracao: false, ehResultado: true);
        await GarantirAsync(db, DadosDoAtoDeTeste.TipoResultadoDoRecurso, "Resultado do recurso", unicoPorObjeto: false, congelaConfiguracao: false, ehResultado: true);

        await db.SaveChangesAsync();
    }

    /// <summary>Semeia um tipo avulso ao catálogo, com a classe de congelamento e a unicidade que o cenário precisa.</summary>
    public static async Task SemearTipoAsync(IServiceProvider services, string codigo, bool unicoPorObjeto, bool congela = true)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        await GarantirAsync(db, codigo, codigo, unicoPorObjeto, congela);
        await db.SaveChangesAsync();
    }

    /// <summary>Semeia tipos de código próprio do cenário, sem congelamento nem unicidade por objeto.</summary>
    public static async Task SemearTiposAsync(
        IServiceProvider services, IEnumerable<(string Codigo, string Nome, bool EhResultado)> tipos)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(tipos);

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        foreach ((string codigo, string nome, bool ehResultado) in tipos)
        {
            await GarantirAsync(db, codigo, nome, unicoPorObjeto: false, congelaConfiguracao: false, ehResultado: ehResultado);
        }

        await db.SaveChangesAsync();
    }

    private static async Task GarantirAsync(UniPlusDbContext db, string codigo, string nome, bool unicoPorObjeto, bool congelaConfiguracao = true, bool publicavelComoAvulso = false, bool ehResultado = false)
    {
        if (await db.Set<TipoAtoPublicado>().AnyAsync(t => t.Codigo == codigo))
        {
            return;
        }

        await db.Set<TipoAtoPublicado>().AddAsync(
            NovoTipo(codigo, nome, unicoPorObjeto, congelaConfiguracao, publicavelComoAvulso, ehResultado));
    }

    private static TipoAtoPublicado NovoTipo(
        string codigo, string nome, bool unicoPorObjeto, bool congelaConfiguracao, bool publicavelComoAvulso, bool ehResultado) =>
        (new TipoAtoPublicadoDeTeste
        {
            Codigo = codigo,
            Nome = nome,
            CongelaConfiguracao = congelaConfiguracao,
            UnicoPorObjeto = unicoPorObjeto,
            EhResultado = ehResultado,
            VigenciaInicio = new DateOnly(2020, 1, 1),
            PublicavelComoAvulso = publicavelComoAvulso
        }).Criar().Value!;
}
