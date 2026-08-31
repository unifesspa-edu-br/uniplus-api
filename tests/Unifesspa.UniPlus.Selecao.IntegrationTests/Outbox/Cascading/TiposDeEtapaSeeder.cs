namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Semeia no banco de teste os tipos de etapa que as regras de obrigatoriedade legal
/// referenciam.
/// </summary>
/// <remarks>
/// O catálogo de tipos de etapa é cadastro do usuário — nenhuma migração o semeia, e o
/// banco efêmero dos testes nasce sem ele. Uma regra legal que referencia código ausente
/// é recusada como inavaliável antes de a conformidade ser avaliada, então o cenário que
/// quer provar a REPROVAÇÃO por conformidade precisa do tipo vivo no cadastro.
/// </remarks>
internal static class TiposDeEtapaSeeder
{
    public static async Task SemearAsync(IServiceProvider services, string codigo)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        ConfiguracaoDbContext db = scope.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();

        if (await db.TiposEtapa.AnyAsync(t => t.Codigo == codigo).ConfigureAwait(false))
        {
            return;
        }

        Result<TipoEtapa> tipo = TipoEtapa.Criar(codigo, "Entrevista", null);
        if (tipo.IsFailure)
        {
            throw new InvalidOperationException(
                $"Tipo de etapa '{codigo}' recusado pelo domínio: {tipo.Error?.Message}");
        }

        db.TiposEtapa.Add(tipo.Value!);
        await db.SaveChangesAsync().ConfigureAwait(false);
    }
}
