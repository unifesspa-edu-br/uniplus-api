namespace Unifesspa.UniPlus.Host.IntegrationTests.Email;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Infrastructure.Core.Email;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Observability;

using Wolverine;
using Wolverine.Runtime;
using Wolverine.Util;

/// <summary>
/// A amostragem da fila de e-mail contra o banco que o host criou: os nomes da tabela da fila e das mortas, a coluna de
/// inserção, as contagens e o que os gauges publicam. Cada teste conta numa cópia das duas tabelas, num esquema só dele,
/// com a mesma estrutura das reais: o consumidor da fila do host compartilhado segue funcionando, e as linhas do teste
/// não chegam a ele nem às outras classes da coleção.
/// </summary>
[Collection(MonolitoComMailpitCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class MonitoramentoDaFilaDeEmailNoHostTests : IAsyncLifetime
{
    private readonly MonolitoComMailpitFixture _fixture;

    public MonitoramentoDaFilaDeEmailNoHostTests(MonolitoComMailpitFixture fixture)
    {
        _fixture = fixture;
    }

    private IServiceProvider Servicos => _fixture.Factory.Services;

    private string StringDeConexao => Servicos.GetRequiredService<IConfiguration>().GetConnectionString("UniPlusDb")!;

    private TabelasDoEnvioDeEmail Tabelas => TabelasDoEnvioDeEmail.Do(Servicos.GetRequiredService<IWolverineRuntime>());

    private string EsquemaDoTeste { get; } = $"amostra_{Guid.NewGuid():N}";

    private TabelasDoEnvioDeEmail Copias => new($"\"{EsquemaDoTeste}\".\"fila\"", $"\"{EsquemaDoTeste}\".\"mortas\"");

    public Task InitializeAsync() => ExecutarAsync(
        $"CREATE SCHEMA \"{EsquemaDoTeste}\"; "
        + $"CREATE TABLE {Copias.Fila} (LIKE {Tabelas.Fila} INCLUDING ALL); "
        + $"CREATE TABLE {Copias.Mortas} (LIKE {Tabelas.Mortas} INCLUDING ALL)");

    public Task DisposeAsync() => ExecutarAsync($"DROP SCHEMA \"{EsquemaDoTeste}\" CASCADE");

    [Fact(DisplayName = "A tabela da fila e a das mortas existem no banco do host, com a coluna de inserção que a amostra lê")]
    public async Task Tabelas_ExistemComAColunaDeInsercao()
    {
        TabelasDoEnvioDeEmail tabelas = Tabelas;

        tabelas.Fila.Should().Be("\"wolverine_queues\".\"wolverine_queue_email_envio\"");
        tabelas.Mortas.Should().Be("\"wolverine\".\"wolverine_dead_letters\"");
        (await ColunasAsync("wolverine_queues", "wolverine_queue_email_envio")).Should().Contain("timestamp");
        (await ColunasAsync("wolverine", "wolverine_dead_letters")).Should().Contain("message_type");
    }

    [Fact(DisplayName = "A amostra conta as prontas, as atrasadas e a idade da mais antiga numa fila com a estrutura da real")]
    public async Task Amostra_ContaAsProntasEAsAtrasadas()
    {
        await InserirNaFilaAsync(minutosAtras: 30);
        await InserirNaFilaAsync(minutosAtras: 10);
        await InserirNaFilaAsync(minutosAtras: 1);

        AmostraDaFilaDeEmail amostra = await Leitura().LerAsync(CancellationToken.None);

        amostra.Prontas.Should().Be(3);
        amostra.Atrasadas.Should().Be(2, "só as inseridas há mais de 5 minutos");
        amostra.IdadeDaMaisAntiga.Should().BeCloseTo(TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(1));
    }

    [Fact(DisplayName = "A fila vazia dá amostra zerada")]
    public async Task Amostra_FilaVazia_Zerada()
    {
        AmostraDaFilaDeEmail amostra = await Leitura().LerAsync(CancellationToken.None);

        amostra.Prontas.Should().Be(0);
        amostra.Atrasadas.Should().Be(0);
        amostra.IdadeDaMaisAntiga.Should().Be(TimeSpan.Zero);
    }

    [Fact(DisplayName = "A amostra conta só as mortas de e-mail e publica o retrato nos gauges")]
    public async Task Amostra_ContaAsMortasDeEmailEPublicaNosGauges()
    {
        await InserirMortaAsync(typeof(EnviarEmail).ToMessageTypeName());
        await InserirMortaAsync("Outra.Mensagem");
        await InserirNaFilaAsync(minutosAtras: 20);
        using MetricasDaFilaDeEmail metricas = new(TimeProvider.System);
        using ColetorDeMedidas coletor = new(metricas.Medidor);

        await new AmostragemDaFilaDeEmail(Leitura(), metricas).ExecutarAsync(CancellationToken.None);

        coletor.Ultima("uniplus.email.mortas").Should().Be(1);
        coletor.Ultima("uniplus.email.fila.prontas").Should().Be(1);
        coletor.Ultima("uniplus.email.fila.atrasadas").Should().Be(1);
        coletor.Ultima("uniplus.email.fila.idade_mais_antiga").Should().BeInRange(19 * 60, 21 * 60);
    }

    [Fact(DisplayName = "A amostragem que falha deixa a idade da amostra crescer, e a que volta a funcionar a zera")]
    public async Task AmostragemComFalha_AIdadeDaAmostraCresce()
    {
        RelogioAvancavel relogio = new();
        using MetricasDaFilaDeEmail metricas = new(relogio);
        using ColetorDeMedidas coletor = new(metricas.Medidor);
        AmostragemDaFilaDeEmail comFalha = new(new LeituraDaFilaDeEmail("Host=localhost;Port=1;Timeout=1", Tabelas), metricas);

        Func<Task> amostragem = () => comFalha.ExecutarAsync(CancellationToken.None);
        await amostragem.Should().ThrowAsync<NpgsqlException>();
        relogio.Avancar(TimeSpan.FromMinutes(16));

        coletor.Ultima("uniplus.email.amostra.idade").Should().Be(16 * 60, "nenhuma amostra deu certo desde a subida");

        await new AmostragemDaFilaDeEmail(Leitura(), metricas).ExecutarAsync(CancellationToken.None);

        coletor.Ultima("uniplus.email.amostra.idade").Should().Be(0);
    }

    private LeituraDaFilaDeEmail Leitura() => new(StringDeConexao, Copias);

    private async Task<List<string>> ColunasAsync(string esquema, string tabela)
    {
        List<string> colunas = [];
        await using NpgsqlConnection conexao = new(StringDeConexao);
        await conexao.OpenAsync();
        await using NpgsqlCommand comando = new(
            "SELECT column_name FROM information_schema.columns WHERE table_schema = @esquema AND table_name = @tabela", conexao);
        comando.Parameters.AddWithValue("esquema", esquema);
        comando.Parameters.AddWithValue("tabela", tabela);
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync();
        while (await leitor.ReadAsync())
        {
            colunas.Add(leitor.GetString(0));
        }

        return colunas;
    }

    private Task InserirNaFilaAsync(int minutosAtras) => ExecutarAsync(
        $"INSERT INTO {Copias.Fila} (id, body, message_type, \"timestamp\") VALUES (gen_random_uuid(), '\\x00', 'teste', now()::timestamp - interval '{minutosAtras} minutes')");

    private Task InserirMortaAsync(string tipo) => ExecutarAsync(
        $"INSERT INTO {Copias.Mortas} (id, execution_time, body, message_type, received_at, source, exception_type, exception_message, sent_at, replayable) "
        + $"VALUES (gen_random_uuid(), now(), '\\x00', '{tipo}', 'teste://origem', 'teste', 'Teste', 'teste', now(), false)");

    private async Task ExecutarAsync(string comando)
    {
        await using NpgsqlConnection conexao = new(StringDeConexao);
        await conexao.OpenAsync();
        await using NpgsqlCommand sql = new(comando, conexao);
        await sql.ExecuteNonQueryAsync();
    }

    private sealed class RelogioAvancavel : TimeProvider
    {
        private DateTimeOffset _agora = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => _agora;

        public void Avancar(TimeSpan intervalo) => _agora += intervalo;
    }
}
