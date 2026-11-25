namespace Unifesspa.UniPlus.Host.IntegrationTests.Transacao;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>Semeia, no catálogo de Publicações, o tipo de ato que a porta de escrita confere antes de registrar.</summary>
internal static class CatalogoDeAtosDeTeste
{
    internal static async Task SemearTipoAsync(IServiceProvider servicos, string codigo, bool unicoPorObjeto)
    {
        await using AsyncServiceScope escopo = servicos.CreateAsyncScope();
        UniPlusDbContext contexto = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        contexto.Set<TipoAtoPublicado>().Add((new TipoAtoPublicadoDeTeste
        {
            Codigo = codigo,
            Nome = "Tipo de ato de teste do registro",
            UnicoPorObjeto = unicoPorObjeto,
            VigenciaInicio = new DateOnly(2026, 1, 1)
        }).Criar().Value!);
        await contexto.SaveChangesAsync();
    }
}
