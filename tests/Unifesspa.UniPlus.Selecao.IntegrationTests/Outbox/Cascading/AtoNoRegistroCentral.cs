namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;

/// <summary>
/// A leitura do <see cref="AtoNormativo"/> que a publicação registrou em Publicações: o ato nasce na mesma transação que
/// grava a versão e o registro do ato do processo, então a leitura não espera nada, e a ausência é falha do teste.
/// </summary>
internal static class AtoNoRegistroCentral
{
    /// <param name="api">A factory da coleção: cada leitura abre o próprio escopo, sem reaproveitar um contexto rastreado.</param>
    /// <param name="atoId">O id do ato, decidido pela Seleção.</param>
    public static async Task<AtoNormativo> LerAsync(MonolitoApiFactory api, Guid atoId)
    {
        ArgumentNullException.ThrowIfNull(api);

        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        AtoNormativo? ato = await db.Set<AtoNormativo>().AsNoTracking().FirstOrDefaultAsync(a => a.Id == atoId);

        ato.Should().NotBeNull($"o ato {atoId} é registrado na mesma transação que a publicação");
        return ato!;
    }
}
