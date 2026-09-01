namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using Npgsql;

using Testcontainers.PostgreSql;

using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// Prova que a migration que acrescenta o código do tipo de deficiência ao snapshot
/// da oferta <b>preserva</b> a configuração já feita, em vez de descartá-la.
/// </summary>
/// <remarks>
/// <para>O código não estava no snapshot, mas é derivável: a linha guarda o
/// identificador de origem, e o cadastro conserva suas linhas sob exclusão lógica.
/// Apagar as associações seria perder configuração de processo em rascunho que
/// pode ser reconstruída de onde ela veio.</para>
/// <para>O caso sem correspondência também é exercitado: origem apagada
/// fisicamente, fora do fluxo da aplicação, não tem de onde derivar o código, e a
/// linha sai em vez de ficar com valor inventado que nenhuma regra casaria.</para>
/// </remarks>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL fixo, escrito no próprio teste — o seed legado não recebe entrada externa.")]
[SuppressMessage(
    "Reliability",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Recursos liberados por IAsyncLifetime.DisposeAsync — xUnit invoca deterministicamente.")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class BackfillCodigoTipoDeficienciaTests : IAsyncLifetime
{
    private const string MigrationAnterior = "20260829024048_RemoveConfirmacaoFundamentosIsencao";

    private static readonly Guid TipoVivoId = new("88888888-8888-7888-8888-888888888801");
    private static readonly Guid TipoRemovidoId = new("88888888-8888-7888-8888-888888888802");
    private static readonly Guid TipoSemOrigemId = new("88888888-8888-7888-8888-888888888803");

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("uniplus_backfill_codigo_tipo_deficiencia")
        .WithUsername("uniplus_test")
        .WithPassword("uniplus_test")
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync().ConfigureAwait(false);

    public async Task DisposeAsync() => await _postgres.DisposeAsync().ConfigureAwait(false);

    [Fact(DisplayName = "Backfill traz o código do cadastro, inclusive do tipo removido, e descarta só o que não tem origem")]
    public async Task Migration_PreservaConfiguracaoDerivandoDoCadastro()
    {
        await using (SelecaoDbContext contexto = CriarContexto())
        {
            IMigrator migrator = contexto.GetService<IMigrator>();
            await migrator.MigrateAsync(MigrationAnterior);
        }

        await SemearEstadoLegadoAsync();

        await using (SelecaoDbContext contexto = CriarContexto())
        {
            Func<Task> migrar = async () => await contexto.Database.MigrateAsync();
            await migrar.Should().NotThrowAsync();
        }

        IReadOnlyDictionary<Guid, string> codigos = await LerCodigosAsync();

        codigos.Should().ContainKey(TipoVivoId)
            .WhoseValue.Should().Be("DEFICIENCIA_VISUAL");

        codigos.Should().ContainKey(TipoRemovidoId)
            .WhoseValue.Should().Be("TEA",
                "o cadastro conserva a linha sob exclusão lógica — remover o tipo depois de "
                + "configurado não apaga a configuração de quem já o escolheu");

        codigos.Should().NotContainKey(TipoSemOrigemId,
            "sem origem no cadastro não há de onde derivar o código, e um valor inventado "
            + "produziria snapshot que nenhuma regra casaria");
    }

    private SelecaoDbContext CriarContexto()
    {
        DbContextOptions<SelecaoDbContext> options = new DbContextOptionsBuilder<SelecaoDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options;

        return new SelecaoDbContext(options);
    }

    /// <summary>
    /// Reproduz o estado anterior à migration: o cadastro de Configuração com um tipo
    /// vivo e um sob exclusão lógica, e três linhas de oferta — uma para cada tipo, e
    /// uma órfã, cuja origem não existe.
    /// </summary>
    private async Task SemearEstadoLegadoAsync()
    {
        await using NpgsqlConnection conexao = new(_postgres.GetConnectionString());
        await conexao.OpenAsync();

        // O schema de Configuração é criado aqui com o mínimo que o backfill lê: cada
        // módulo migra o próprio schema, e esta suíte sobe apenas o de Seleção.
        await ExecutarAsync(conexao,
            """
            CREATE SCHEMA IF NOT EXISTS configuracao;
            CREATE TABLE IF NOT EXISTS configuracao.tipo_deficiencia (
                id uuid PRIMARY KEY,
                codigo varchar(50) NOT NULL,
                nome varchar(200) NOT NULL,
                is_deleted boolean NOT NULL DEFAULT false
            );
            """);

        await ExecutarAsync(conexao,
            $"""
            INSERT INTO configuracao.tipo_deficiencia (id, codigo, nome, is_deleted) VALUES
                ('{TipoVivoId}', 'DEFICIENCIA_VISUAL', 'Deficiência visual', false),
                ('{TipoRemovidoId}', 'TEA', 'Transtorno do espectro autista', true);
            """);

        // A oferta é filha de um processo, com chave estrangeira real — daí o processo
        // mínimo abaixo, com só o que a tabela exige. O backfill não olha para ele.
        Guid processoId = Guid.CreateVersion7();
        Guid ofertaId = Guid.CreateVersion7();
        await ExecutarAsync(conexao,
            $"""
            INSERT INTO selecao.processos_seletivos
                (id, created_at, is_deleted, tipo_processo_codigo, tipo_processo_nome, tipo_processo_origem_id)
            VALUES ('{processoId}', now(), false, 'PS_TESTE', 'Processo de teste', '{Guid.CreateVersion7()}');

            INSERT INTO selecao.ofertas_atendimento_especializado (id, processo_seletivo_id, created_at)
            VALUES ('{ofertaId}', '{processoId}', now());

            INSERT INTO selecao.ofertas_tipo_deficiencia
                (id, oferta_atendimento_especializado_id, tipo_deficiencia_origem_id, tipo_deficiencia_nome, created_at)
            VALUES
                ('{Guid.CreateVersion7()}', '{ofertaId}', '{TipoVivoId}', 'Deficiência visual', now()),
                ('{Guid.CreateVersion7()}', '{ofertaId}', '{TipoRemovidoId}', 'Transtorno do espectro autista', now()),
                ('{Guid.CreateVersion7()}', '{ofertaId}', '{TipoSemOrigemId}', 'Tipo sem origem', now());
            """);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> LerCodigosAsync()
    {
        await using NpgsqlConnection conexao = new(_postgres.GetConnectionString());
        await conexao.OpenAsync();
        await using NpgsqlCommand comando = new(
            "SELECT tipo_deficiencia_origem_id, tipo_deficiencia_codigo FROM selecao.ofertas_tipo_deficiencia",
            conexao);
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync();

        Dictionary<Guid, string> resultado = [];
        while (await leitor.ReadAsync())
        {
            resultado.Add(leitor.GetGuid(0), leitor.GetString(1));
        }

        return resultado;
    }

    private static async Task ExecutarAsync(NpgsqlConnection conexao, string sql)
    {
        await using NpgsqlCommand comando = new(sql, conexao);
        await comando.ExecuteNonQueryAsync();
    }
}
