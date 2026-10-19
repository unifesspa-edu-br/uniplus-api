namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>A identidade que o ato publicado de um item guarda, calculada sobre o cronograma gravado do processo.</summary>
internal static class IdentidadeDoItemNoBanco
{
    public static async Task<IdentidadeDoItemDoEdital> DoItemAsync(IServiceProvider services, Guid processoId, Guid produtoId)
    {
        await using AsyncServiceScope escopo = services.CreateAsyncScope();
        SelecaoDbContext db = escopo.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        List<FaseCronograma> fases = await db.FasesCronograma.AsNoTracking()
            .Include(f => f.Produtos).Where(f => f.ProcessoSeletivoId == processoId).ToListAsync();
        List<EtapaProcesso> etapas = await db.EtapasProcesso.AsNoTracking()
            .Include(e => e.Produtos).Where(e => e.ProcessoSeletivoId == processoId).ToListAsync();

        return IdentidadeDoItemDoEdital.Dos(fases, etapas)[produtoId];
    }
}
