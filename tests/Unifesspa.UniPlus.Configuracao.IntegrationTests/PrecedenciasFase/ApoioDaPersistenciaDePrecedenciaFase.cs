namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.PrecedenciasFase;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// As etapas que os testes de persistência da precedência entre fases repetem contra o banco: gravar,
/// ler de novo, excluir e consultar pelo leitor entre módulos. Cada etapa usa o seu próprio contexto,
/// como o sistema faz.
/// </summary>
internal sealed class ApoioDaPersistenciaDePrecedenciaFase
{
    private readonly ConfiguracaoDbFixture _fixture;

    internal ApoioDaPersistenciaDePrecedenciaFase(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    internal async Task GravarAsync(string usuario, PrecedenciaFase aresta)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(usuario);
        ctx.PrecedenciasFase.Add(aresta);
        await ctx.SaveChangesAsync();
    }

    internal async Task<PrecedenciaFase> GravarELerDeNovoAsync(string usuario, PrecedenciaFase aresta)
    {
        await GravarAsync(usuario, aresta);

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        return await ctx.PrecedenciasFase.SingleAsync(p => p.Id == aresta.Id);
    }

    internal async Task ExcluirAsync(string usuario, Guid id)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(usuario);
        PrecedenciaFase aresta = await ctx.PrecedenciasFase.SingleAsync(p => p.Id == id);
        ctx.PrecedenciasFase.Remove(aresta);
        await ctx.SaveChangesAsync();
    }

    internal async Task<PrecedenciaFase> LerExcluidaAsync(Guid id)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        return await ctx.PrecedenciasFase.IgnoreQueryFilters().SingleAsync(p => p.Id == id);
    }

    internal async Task<PrecedenciaFaseView?> ObterPeloLeitorAsync(Guid id)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        return await new PrecedenciaFaseReader(ctx).ObterPorIdAsync(id);
    }
}
