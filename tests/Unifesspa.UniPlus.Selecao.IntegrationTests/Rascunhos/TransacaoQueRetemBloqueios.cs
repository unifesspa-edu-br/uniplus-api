namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Rascunhos;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

/// <summary>Uma transação aberta numa conexão própria, que retém os bloqueios que a suíte pede enquanto durar.</summary>
internal sealed class TransacaoQueRetemBloqueios : IAsyncDisposable
{
    private readonly IDbContextTransaction _transacao;

    private TransacaoQueRetemBloqueios(UniPlusDbContext contexto, IDbContextTransaction transacao)
    {
        Contexto = contexto;
        _transacao = transacao;
        Repositorio = RascunhosSemeadosParaTeste.Repositorio(contexto);
    }

    public UniPlusDbContext Contexto { get; }

    public RascunhoDeProcessoSeletivoRepository Repositorio { get; }

    public static async Task<TransacaoQueRetemBloqueios> IniciarAsync(ProcessoSeletivoDbFixture fixture)
    {
        UniPlusDbContext contexto = fixture.CreateDbContext();
        return new TransacaoQueRetemBloqueios(contexto, await contexto.Database.BeginTransactionAsync());
    }

    public Task ConfirmarAsync() => _transacao.CommitAsync();

    public Task DefinirLimiteDaEsperaAsync(string limite) =>
        Contexto.Database.ExecuteSqlInterpolatedAsync($"SELECT set_config('lock_timeout', {limite}, true)");

    public Task<string> LimiteDaEsperaAsync() =>
        Contexto.Database.SqlQuery<string>($"SELECT current_setting('lock_timeout') AS \"Value\"").SingleAsync();

    public async ValueTask DisposeAsync()
    {
        await _transacao.DisposeAsync();
        await Contexto.DisposeAsync();
    }
}
