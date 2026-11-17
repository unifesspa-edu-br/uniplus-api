namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Rotinas;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// A execução única da rotina contra o PostgreSQL real: a segunda instância que pede a mesma rotina recebe nulo, o
/// descarte da execução a libera, e a conexão dedicada só continua aberta enquanto a execução é a da instância.
/// </summary>
[Collection(AutorizacaoEndpointCollection.Name)]
public sealed class TravaDaRotinaNoBancoTests
{
    private readonly AutorizacaoEndpointFixture _fixture;

    public TravaDaRotinaNoBancoTests(AutorizacaoEndpointFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "A segunda tentativa da mesma rotina recebe nulo enquanto a primeira execução não é descartada")]
    public async Task TentarIniciar_SegundaTentativaDaMesmaRotina_RecebeNulo()
    {
        string rotina = NomeDaRotina();
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        ITravaDaRotina execucaoUnica = escopo.ServiceProvider.GetRequiredService<ITravaDaRotina>();

        await using IAsyncDisposable? primeira = await execucaoUnica.TentarObterAsync(rotina, CancellationToken.None);
        IAsyncDisposable? segunda = await execucaoUnica.TentarObterAsync(rotina, CancellationToken.None);

        primeira.Should().NotBeNull();
        segunda.Should().BeNull();
    }

    [Fact(DisplayName = "Descartar a execução libera a rotina para a tentativa seguinte")]
    public async Task TentarIniciar_DepoisDoDescarte_ExecutaDeNovo()
    {
        string rotina = NomeDaRotina();
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        ITravaDaRotina execucaoUnica = escopo.ServiceProvider.GetRequiredService<ITravaDaRotina>();
        IAsyncDisposable? primeira = await execucaoUnica.TentarObterAsync(rotina, CancellationToken.None);
        await primeira!.DisposeAsync();

        await using IAsyncDisposable? seguinte = await execucaoUnica.TentarObterAsync(rotina, CancellationToken.None);

        seguinte.Should().NotBeNull();
    }

    [Fact(DisplayName = "Rotinas de nomes diferentes executam ao mesmo tempo")]
    public async Task TentarIniciar_RotinasDiferentes_ExecutamJuntas()
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        ITravaDaRotina execucaoUnica = escopo.ServiceProvider.GetRequiredService<ITravaDaRotina>();

        await using IAsyncDisposable? primeira = await execucaoUnica.TentarObterAsync(NomeDaRotina(), CancellationToken.None);
        await using IAsyncDisposable? segunda = await execucaoUnica.TentarObterAsync(NomeDaRotina(), CancellationToken.None);

        primeira.Should().NotBeNull();
        segunda.Should().NotBeNull();
    }

    [Fact(DisplayName = "A string de conexão inválida falha ao iniciar e não deixa conexão aberta")]
    public async Task TentarIniciar_StringDeConexaoInvalida_FalhaSemConexaoAberta()
    {
        string aplicacao = NomeDaAplicacao();
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        TravaDaRotinaNoBanco execucaoUnica = NoBanco(escopo, aplicacao, "banco_que_nao_existe");

        Func<Task> iniciar = () => execucaoUnica.TentarObterAsync(NomeDaRotina(), CancellationToken.None);

        await iniciar.Should().ThrowAsync<PostgresException>();
        (await SessoesAteAsync(aplicacao, 0)).Should().Be(0);
    }

    [Fact(DisplayName = "A execução negada descarta a conexão que usou para pedir")]
    public async Task TentarIniciar_ExecucaoNegada_DescartaAConexao()
    {
        string aplicacao = NomeDaAplicacao();
        string rotina = NomeDaRotina();
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        TravaDaRotinaNoBanco execucaoUnica = NoBanco(escopo, aplicacao);
        await using IAsyncDisposable? primeira = await execucaoUnica.TentarObterAsync(rotina, CancellationToken.None);

        IAsyncDisposable? negada = await execucaoUnica.TentarObterAsync(rotina, CancellationToken.None);

        negada.Should().BeNull();
        (await SessoesAteAsync(aplicacao, 1)).Should().Be(1);
    }

    [Fact(DisplayName = "A execução obtida mantém a conexão até o descarte e a fecha nele")]
    public async Task TentarIniciar_ExecucaoObtida_MantemAConexaoAteODescarte()
    {
        string aplicacao = NomeDaAplicacao();
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        TravaDaRotinaNoBanco execucaoUnica = NoBanco(escopo, aplicacao);
        IAsyncDisposable? execucao = await execucaoUnica.TentarObterAsync(NomeDaRotina(), CancellationToken.None);

        (await SessoesAteAsync(aplicacao, 1)).Should().Be(1);
        await execucao!.DisposeAsync();

        (await SessoesAteAsync(aplicacao, 0)).Should().Be(0);
    }

    private static string NomeDaRotina() => $"teste.execucao_unica.{Guid.NewGuid():N}";

    private static string NomeDaAplicacao() => $"teste-execucao-unica-{Guid.NewGuid():N}";

    // Sem pool e com nome de aplicação próprio: cada conexão da rotina aparece sozinha no banco e some quando é fechada.
    private static TravaDaRotinaNoBanco NoBanco(AsyncServiceScope escopo, string aplicacao, string? banco = null)
    {
        UniPlusDbContext contexto = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        NpgsqlConnectionStringBuilder cadeia = new(contexto.Database.GetConnectionString()) { Pooling = false, ApplicationName = aplicacao };
        if (banco is not null)
        {
            cadeia.Database = banco;
        }

        contexto.Database.SetConnectionString(cadeia.ConnectionString);

        return new TravaDaRotinaNoBanco(contexto);
    }

    private async Task<int> SessoesAteAsync(string aplicacao, int esperadas)
    {
        int sessoes = await SessoesAsync(aplicacao);
        for (int tentativa = 0; tentativa < 50 && sessoes != esperadas; tentativa++)
        {
            await Task.Delay(100);
            sessoes = await SessoesAsync(aplicacao);
        }

        return sessoes;
    }

    private async Task<int> SessoesAsync(string aplicacao)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext contexto = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        return await contexto.Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM pg_stat_activity WHERE application_name = {aplicacao}")
            .SingleAsync();
    }
}
