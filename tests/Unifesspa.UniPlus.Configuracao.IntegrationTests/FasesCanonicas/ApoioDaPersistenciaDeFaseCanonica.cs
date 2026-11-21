namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.FasesCanonicas;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// As etapas que os testes de persistência da fase canônica repetem contra o banco: gravar, ler de novo,
/// excluir e consultar pelo leitor entre módulos. Cada etapa usa o seu próprio contexto, como o sistema faz.
/// </summary>
internal sealed class ApoioDaPersistenciaDeFaseCanonica
{
    private readonly ConfiguracaoDbFixture _fixture;

    internal ApoioDaPersistenciaDeFaseCanonica(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    internal async Task GravarAsync(string usuario, params FaseCanonica[] fases)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(usuario);
        ctx.FasesCanonicas.AddRange(fases);
        await ctx.SaveChangesAsync();
    }

    internal async Task<FaseCanonica> GravarELerDeNovoAsync(string usuario, FaseCanonica fase)
    {
        await GravarAsync(usuario, fase);

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        return await ctx.FasesCanonicas.SingleAsync(f => f.Id == fase.Id);
    }

    internal async Task ExcluirAsync(string usuario, Guid id)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(usuario);
        FaseCanonica fase = await ctx.FasesCanonicas.SingleAsync(f => f.Id == id);
        ctx.FasesCanonicas.Remove(fase);
        await ctx.SaveChangesAsync();
    }

    internal async Task<FaseCanonica> LerExcluidaAsync(Guid id)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        return await ctx.FasesCanonicas.IgnoreQueryFilters().SingleAsync(f => f.Id == id);
    }

    internal async Task<FaseCanonicaView?> ObterPeloLeitorAsync(Guid id)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        return await new FaseCanonicaReader(ctx).ObterPorIdAsync(id);
    }

    internal async Task<IReadOnlyList<FaseCanonicaView>> ListarVivasPeloLeitorAsync()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        return await new FaseCanonicaReader(ctx).ListarVivosAsync();
    }

    /// <summary>
    /// Libera o slot de um código ocupado pelo seed das quatorze fases, usando o mesmo
    /// soft-delete que o sistema usa — o índice único é parcial
    /// (<c>WHERE is_deleted = false</c>), então a linha semeada sai do caminho sem ser
    /// destruída.
    /// </summary>
    /// <remarks>
    /// Desde que <c>FaseCanonica</c> passou a nascer semeada, todo código do vocabulário
    /// já tem linha viva ao preparar o banco de teste. Testes que exercitam a criação
    /// precisam do slot livre; usar o soft-delete em vez de apagar a linha mantém o teste
    /// sobre o comportamento real do cadastro.
    /// </remarks>
    internal async Task LiberarSlotDoSeedAsync(params string[] codigos)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        // SQL direto em vez de LINQ: `Codigo` e value object com converter, e um
        // `Contains` sobre ele nao traduz para SQL. O filtro pelo prefixo do id
        // restringe o apagamento as linhas do seed.
        const string comando =
            "DELETE FROM configuracao.fases_canonicas " +
            "WHERE id::text LIKE 'f45e0000-0000-7000-8000-%' AND codigo = ANY(@p0)";

        await ctx.Database.ExecuteSqlRawAsync(comando, new NpgsqlParameter("p0", codigos));
    }
}
