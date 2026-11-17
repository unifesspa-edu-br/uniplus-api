namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Unidades;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;
using Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Infrastructure;

/// <summary>Grava Unidades na base da API de teste, como um cadastro anterior ao pedido.</summary>
internal static class UnidadesNoEndpointDeTeste
{
    public static async Task<Guid> SemearAsync(this OrganizacaoEndpointFixture fixture, CadastroDeUnidadeDeTeste cadastro)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        ArgumentNullException.ThrowIfNull(cadastro);

        Unidade unidade = cadastro.Criar();
        using IServiceScope escopo = fixture.Factory.Services.CreateScope();
        UniPlusDbContext contexto = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        contexto.Unidades.Add(unidade);
        await contexto.SaveChangesAsync();
        return unidade.Id;
    }
}
