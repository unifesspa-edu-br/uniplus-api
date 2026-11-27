namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;

/// <summary>
/// Semeia e relê o cadastro do banco do host de teste, cada operação num contexto próprio, como uma
/// requisição faria. A Instituição é singleton: o teste que a semeia a remove antes e depois.
/// </summary>
internal static class CadastroDeTeste
{
    public static async Task<Unidade> SemearUnidadeAsync(
        this OrganizacaoEndpointFixture fixture, string? sigla = null, TipoUnidade tipo = TipoUnidade.Centro)
    {
        Unidade unidade = NovaUnidade(sigla, tipo);

        using IServiceScope escopo = fixture.Factory.Services.CreateScope();
        UniPlusDbContext contexto = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        contexto.Unidades.Add(unidade);
        await contexto.SaveChangesAsync();
        return unidade;
    }

    public static async Task<Unidade> ObterUnidadeAsync(this OrganizacaoEndpointFixture fixture, Guid id)
    {
        using IServiceScope escopo = fixture.Factory.Services.CreateScope();
        UniPlusDbContext contexto = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        return await contexto.Unidades.AsNoTracking().SingleAsync(unidade => unidade.Id == id);
    }

    public static async Task<Instituicao> SemearInstituicaoAsync(this OrganizacaoEndpointFixture fixture)
    {
        Instituicao instituicao = Instituicao.Criar(
            new IdentificacaoRegulatoriaDaInstituicao("9001", "Instituição de teste", "INST", "Universidade", "Pública Federal"),
            CidadeDaSede.Nenhuma,
            null,
            null).Value!;

        using IServiceScope escopo = fixture.Factory.Services.CreateScope();
        UniPlusDbContext contexto = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        contexto.Instituicoes.Add(instituicao);
        await contexto.SaveChangesAsync();
        return instituicao;
    }

    public static async Task<Instituicao> ObterInstituicaoAsync(this OrganizacaoEndpointFixture fixture, Guid id)
    {
        using IServiceScope escopo = fixture.Factory.Services.CreateScope();
        UniPlusDbContext contexto = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        return await contexto.Instituicoes.AsNoTracking().SingleAsync(instituicao => instituicao.Id == id);
    }

    public static async Task RemoverInstituicoesAsync(this OrganizacaoEndpointFixture fixture)
    {
        using IServiceScope escopo = fixture.Factory.Services.CreateScope();
        UniPlusDbContext contexto = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        await contexto.Database.ExecuteSqlRawAsync("DELETE FROM organizacao.instituicoes");
    }

    /// <summary>Apaga as unidades cuja sigla começa pela marca e os identificadores delas.</summary>
    public static async Task RemoverUnidadesDaMarcaAsync(this OrganizacaoEndpointFixture fixture, string marca)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marca);

        using IServiceScope escopo = fixture.Factory.Services.CreateScope();
        UniPlusDbContext contexto = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        await contexto.Database.ExecuteSqlAsync(
            $"DELETE FROM organizacao.identificadores_da_unidade WHERE unidade_id IN (SELECT id FROM organizacao.unidades WHERE starts_with(sigla, {marca}))");
        await contexto.Database.ExecuteSqlAsync($"DELETE FROM organizacao.unidades WHERE starts_with(sigla, {marca})");
    }

    private static Unidade NovaUnidade(string? sigla, TipoUnidade tipo)
    {
        string sufixo = Guid.NewGuid().ToString("N")[..8];
        return Unidade.Criar(
            "Unidade de teste",
            new IdentificadoresDaUnidade($"u-{sufixo}", sigla ?? sufixo, sufixo, null),
            CidadeDaUnidade.Nenhuma,
            new VigenciaDaUnidade(new DateOnly(2026, 1, 1), null),
            null,
            tipo,
            false).Value!;
    }
}
