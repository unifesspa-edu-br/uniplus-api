namespace Unifesspa.UniPlus.Host.IntegrationTests.Transacao;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;

/// <summary>Semeia, no catálogo de Publicações, o tipo de ato que a porta de escrita confere antes de registrar.</summary>
internal static class CatalogoDeAtosDeTeste
{
    internal static async Task SemearTipoAsync(IServiceProvider servicos, string codigo, bool unicoPorObjeto)
    {
        await using AsyncServiceScope escopo = servicos.CreateAsyncScope();
        UniPlusDbContext contexto = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        contexto.Set<TipoAtoPublicado>().Add(TipoAtoPublicado.Criar(
            codigo, "Tipo de ato de teste do registro", congelaConfiguracao: false, unicoPorObjeto, efeitoIrreversivel: false,
            ehResultado: false, new DateOnly(2026, 1, 1), vigenciaFim: null, baseLegal: null).Value!);
        await contexto.SaveChangesAsync();
    }
}
