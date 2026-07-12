namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using Npgsql;

using Testcontainers.PostgreSql;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// Prova que a migration que promove a configuração congelada a agregado próprio
/// (ADR-0104) <b>transporta</b> os congelamentos já existentes, em vez de
/// descartá-los ao dropar a tabela antiga.
/// </summary>
/// <remarks>
/// <para>
/// Diferente das demais suítes de persistência, este teste sobe um banco próprio
/// e migra em DUAS etapas — até a migration anterior, semeia um certame já
/// publicado e retificado no modelo velho, e só então aplica a nova. É a única
/// forma de exercitar o backfill: a fixture compartilhada migra até o fim antes
/// de qualquer teste rodar, e nesse banco a tabela antiga nunca chega a existir
/// com linhas.
/// </para>
/// <para>
/// O que se assere é o que o novo modelo precisa para não perder prova: o id do
/// congelamento (que o <c>ProcessoPublicadoEvent</c> já publicou no Kafka e os
/// consumidores guardaram), os bytes canônicos, e a cadeia — a versão 2 tem de
/// retificar o ato criador da versão 1.
/// </para>
/// </remarks>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL fixo, escrito no próprio teste — o seed legado não recebe entrada externa.")]
[SuppressMessage(
    "Reliability",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Recursos liberados por IAsyncLifetime.DisposeAsync — xUnit invoca deterministicamente.")]
public sealed class BackfillVersaoConfiguracaoTests : IAsyncLifetime
{
    private const string MigrationAnterior = "20260708120000_AddIndiceRetificacaoUnica";

    private static readonly Guid ProcessoId = new("11111111-1111-7111-8111-111111111111");
    private static readonly Guid DocumentoId = new("22222222-2222-7222-8222-222222222222");
    private static readonly Guid EditalAberturaId = new("33333333-3333-7333-8333-333333333333");
    private static readonly Guid EditalRetificacaoId = new("44444444-4444-7444-8444-444444444444");
    private static readonly Guid SnapshotAberturaId = new("55555555-5555-7555-8555-555555555555");
    private static readonly Guid SnapshotRetificacaoId = new("66666666-6666-7666-8666-666666666666");

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("uniplus_backfill_tests")
        .WithUsername("uniplus_test")
        .WithPassword("uniplus_test")
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync().ConfigureAwait(false);

    public async Task DisposeAsync() => await _postgres.DisposeAsync().ConfigureAwait(false);

    [Fact(DisplayName = "A migration transporta os congelamentos do modelo antigo para as versões, preservando id, bytes e cadeia")]
    public async Task Migration_TransportaSnapshotsLegadosParaVersoes()
    {
        await using (SelecaoDbContext contextoLegado = CriarContexto())
        {
            // Estado do mundo ANTES desta story: um certame publicado e retificado,
            // com os dois congelamentos presos ao Edital por chave estrangeira.
            IMigrator migrator = contextoLegado.GetService<IMigrator>();
            await migrator.MigrateAsync(MigrationAnterior);
            await SemearCertamePublicadoERetificadoAsync();
        }

        await using (SelecaoDbContext contextoNovo = CriarContexto())
        {
            await contextoNovo.Database.MigrateAsync();
        }

        await using SelecaoDbContext leitura = CriarContexto();
        List<VersaoConfiguracao> versoes = await leitura.VersoesConfiguracao
            .AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == ProcessoId)
            .OrderBy(v => v.NumeroVersao)
            .ToListAsync(CancellationToken.None);

        versoes.Should().HaveCount(2, "cada congelamento do modelo antigo vira uma versão — nenhum se perde no caminho");

        VersaoConfiguracao versao1 = versoes[0];
        versao1.Id.Should().Be(
            SnapshotAberturaId,
            "o id do congelamento é a referência forense durável que o ProcessoPublicadoEvent já publicou — trocá-lo invalidaria o que os consumidores guardaram");
        versao1.NumeroVersao.Should().Be(1);
        versao1.AtoCriadorId.Should().Be(EditalAberturaId);
        versao1.AtoCriadorRetificaId.Should().BeNull("a abertura não retifica ato algum");
        versao1.ConfiguracaoCongeladaCanonica.Should().Equal([0x7b, 0x7d], "os bytes canônicos são a base do hash (ADR-0100)");

        VersaoConfiguracao versao2 = versoes[1];
        versao2.Id.Should().Be(SnapshotRetificacaoId);
        versao2.NumeroVersao.Should().Be(2, "a ordem da publicação legada vira o número da versão");
        versao2.AtoCriadorId.Should().Be(EditalRetificacaoId);
        versao2.AtoCriadorRetificaId.Should().Be(
            EditalAberturaId,
            "a cadeia é reconstruída do vínculo entre os Editais: a versão 2 retifica o ato criador da versão 1");
        versao2.VigenteAPartirDe.Should().BeAfter(
            versao1.VigenteAPartirDe,
            "a vigência herda a ordem da publicação legada e não regride");

        bool tabelaAntigaExiste = await TabelaAntigaExisteAsync();
        tabelaAntigaExiste.Should().BeFalse("a tabela antiga só é dropada DEPOIS de as suas linhas serem transportadas");
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
    /// Semeia, em SQL cru, o certame como o modelo ANTIGO o gravava: dois Editais
    /// encadeados (abertura → retificação) e um <c>snapshot_publicacao</c> preso a
    /// cada um. Não há entidade C# para isso — o tipo foi removido nesta story, e é
    /// justamente esse o ponto.
    /// </summary>
    private async Task SemearCertamePublicadoERetificadoAsync()
    {
        await using NpgsqlConnection conexao = new(_postgres.GetConnectionString());
        await conexao.OpenAsync();

        await ExecutarAsync(conexao, $$"""
            INSERT INTO selecao.processos_seletivos (id, nome, tipo, status, created_at, is_deleted)
            VALUES ('{{ProcessoId}}', 'PS legado', 1, 2, now(), false);

            INSERT INTO selecao.documentos_edital (
                id, processo_seletivo_id, object_key, status, expira_em, hash_sha256, confirmado_em, created_at)
            VALUES ('{{DocumentoId}}', '{{ProcessoId}}', 'editais/legado.pdf', 2, now() + interval '1 day',
                    repeat('b', 64), now(), now());

            INSERT INTO selecao.editais (
                id, processo_seletivo_id, natureza, numero, data_publicacao,
                documento_edital_id, edital_retificado_id, motivo_retificacao, created_at)
            VALUES
                ('{{EditalAberturaId}}', '{{ProcessoId}}', 1, '001/2026', now() - interval '2 hours',
                 '{{DocumentoId}}', NULL, NULL, now()),
                ('{{EditalRetificacaoId}}', '{{ProcessoId}}', 2, '001/2026', now() - interval '1 hour',
                 '{{DocumentoId}}', '{{EditalAberturaId}}', 'Correção de datas', now());

            INSERT INTO selecao.snapshot_publicacao (
                id, edital_id, schema_version, algoritmo_hash,
                configuracao_congelada_canonica, configuracao_congelada,
                hash_configuracao, hash_edital, ator_usuario_sub, data_publicacao)
            VALUES
                ('{{SnapshotAberturaId}}', '{{EditalAberturaId}}', '1.0', 'canonical-json/sha256@v1',
                 '\x7b7d'::bytea, '{}'::jsonb, repeat('a', 64), repeat('b', 64),
                 'legado-user', now() - interval '2 hours'),
                ('{{SnapshotRetificacaoId}}', '{{EditalRetificacaoId}}', '1.0', 'canonical-json/sha256@v1',
                 '\x7b7d'::bytea, '{}'::jsonb, repeat('c', 64), repeat('d', 64),
                 'legado-user', now() - interval '1 hour');
            """);
    }

    private async Task<bool> TabelaAntigaExisteAsync()
    {
        await using NpgsqlConnection conexao = new(_postgres.GetConnectionString());
        await conexao.OpenAsync();

        await using NpgsqlCommand comando = new(
            """
            SELECT count(*)
            FROM information_schema.tables
            WHERE table_schema = 'selecao' AND table_name = 'snapshot_publicacao'
            """,
            conexao);

        return Convert.ToInt32(await comando.ExecuteScalarAsync(), provider: null) > 0;
    }

    private static async Task ExecutarAsync(NpgsqlConnection conexao, string sql)
    {
        await using NpgsqlCommand comando = new(sql, conexao);
        await comando.ExecuteNonQueryAsync();
    }
}
