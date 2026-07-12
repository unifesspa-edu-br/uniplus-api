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
/// Exercita o <c>Down</c> da migration que elimina a tabela <c>editais</c>, contra Postgres
/// real: reverter não pode deixar a versão anterior da aplicação com certames impossíveis de
/// ler.
/// </summary>
/// <remarks>
/// <para>
/// Recriar a tabela vazia não desfaz a migration. O código anterior <b>lê</b> <c>editais</c>
/// para hidratar o snapshot vigente e para eleger o alvo da retificação; sem as linhas, todo
/// certame já publicado voltaria referenciando um ato inexistente, e as duas operações
/// falhariam. O rollback repovoa a tabela a partir do que a Seleção ainda possui — as versões
/// congeladas.
/// </para>
/// <para>
/// A reconstrução é possível justamente porque a story provou que o dado era redundante: o id
/// do Edital é o <c>ato_criador_id</c>; a natureza sai da RELAÇÃO (quem não emenda ninguém é
/// abertura — a bijeção que tornava o enum supérfluo); número, documento e motivo estão
/// congelados nos bytes canônicos. A exceção é a data documental, que migrou para o ato.
/// </para>
/// </remarks>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL fixo, escrito no próprio teste — não recebe entrada externa.")]
public sealed class RollbackRemoveEditalTests : IAsyncLifetime
{
    private const string MigrationQueEliminaOEdital = "20260712211117_RemoveEditalComoEntidadeDeSelecao";
    private const string MigrationAnterior = "20260712103053_RemoveIndiceDataPublicacaoUnica";

    private static readonly Guid ProcessoId = Guid.CreateVersion7();
    private static readonly Guid DocumentoId = Guid.CreateVersion7();
    private static readonly Guid AtoAberturaId = Guid.CreateVersion7();
    private static readonly Guid AtoRetificacaoId = Guid.CreateVersion7();

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("uniplus_test")
        .WithUsername("uniplus_test")
        .WithPassword("uniplus_test")
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync().ConfigureAwait(false);

    public async Task DisposeAsync() => await _postgres.DisposeAsync().ConfigureAwait(false);

    [Fact(DisplayName = "Reverter a migration repovoa editais a partir das versões — o código antigo volta a encontrar o que lê")]
    public async Task Down_RepovoaEditaisAPartirDasVersoes()
    {
        // Mundo DEPOIS da story: um certame publicado e retificado, com duas versões e nenhum
        // Edital — o ato vive em Publicações.
        await using (SelecaoDbContext contexto = CriarContexto())
        {
            await contexto.Database.MigrateAsync();
        }

        await SemearCertamePublicadoERetificadoAsync();

        // O operador reverte.
        await using (SelecaoDbContext contexto = CriarContexto())
        {
            IMigrator migrator = contexto.GetService<IMigrator>();
            await migrator.MigrateAsync(MigrationAnterior);
        }

        (await TabelaEditaisExisteAsync()).Should().BeTrue("o rollback recria a tabela do modelo antigo");

        IReadOnlyList<LinhaDeEdital> editais = await ListarEditaisAsync();

        editais.Should().HaveCount(2, "cada versão congelada tem um ato que a criou — e é ele que o Edital era");

        LinhaDeEdital abertura = editais.Single(e => e.Id == AtoAberturaId);
        abertura.Natureza.Should().Be(1, "quem não emenda ninguém é a abertura — a natureza sai da RELAÇÃO");
        abertura.EditalRetificadoId.Should().BeNull();
        abertura.MotivoRetificacao.Should().BeNull();
        abertura.Numero.Should().Be("001/2026", "o número estava congelado no bloco 'periodo'");
        abertura.DocumentoEditalId.Should().Be(DocumentoId, "e o documento, no bloco 'hashesEdital'");
        abertura.DataPublicacao.Should().NotBeNull(
            "nula, a hidratação do snapshot no código antigo devolveria 'ato não encontrado' e o "
            + "certame publicado ficaria ilegível");

        LinhaDeEdital retificacao = editais.Single(e => e.Id == AtoRetificacaoId);
        retificacao.Natureza.Should().Be(2, "quem emenda outro ato é retificação");
        retificacao.EditalRetificadoId.Should().Be(AtoAberturaId, "a cadeia é reconstruída do vínculo entre as versões");
        retificacao.MotivoRetificacao.Should().Be("Correção do prazo", "o motivo estava congelado no bloco 'retificacao'");

        // O contrato do modelo antigo volta íntegro: o CHECK de bijeção que o rollback recria
        // aceita as duas linhas. Se a natureza e o par (retificado, motivo) discordassem, o
        // próprio INSERT do Down teria falhado — é a bijeção provando, mais uma vez, que um
        // dos dois campos sempre foi supérfluo.
        (await ContarAsync("SELECT count(*) FROM selecao.editais WHERE (natureza = 1) <> (edital_retificado_id IS NULL)"))
            .Should().Be(0, "natureza e relação nunca discordam — é a bijeção que tornava o enum redundante");
    }

    [Fact(DisplayName = "Reverter e reaplicar é idempotente — o ciclo completo não deixa a base inconsistente")]
    public async Task DownEUpNovamente_NaoQuebraAMigracao()
    {
        await using (SelecaoDbContext contexto = CriarContexto())
        {
            await contexto.Database.MigrateAsync();
        }

        await SemearCertamePublicadoERetificadoAsync();

        await using (SelecaoDbContext contexto = CriarContexto())
        {
            IMigrator migrator = contexto.GetService<IMigrator>();
            await migrator.MigrateAsync(MigrationAnterior);
            await migrator.MigrateAsync(MigrationQueEliminaOEdital);
        }

        (await TabelaEditaisExisteAsync()).Should().BeFalse("reaplicada, a migration volta a eliminar a tabela");

        // E as versões — que são a evidência forense — atravessaram o ciclo intactas.
        await using SelecaoDbContext leitura = CriarContexto();
        List<Domain.Entities.VersaoConfiguracao> versoes = await leitura.VersoesConfiguracao
            .AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == ProcessoId)
            .OrderBy(v => v.NumeroVersao)
            .ToListAsync(CancellationToken.None);

        versoes.Should().HaveCount(2);
        versoes[0].AtoCriadorId.Should().Be(AtoAberturaId);
        versoes[1].AtoCriadorRetificaId.Should().Be(AtoAberturaId, "a cadeia sobrevive ao rollback e ao reapply");
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
    /// Semeia, em SQL cru, o certame como o modelo NOVO o grava: duas versões encadeadas, sem
    /// Edital algum — o ato é de Publicações. Os blocos do snapshot carregam o que o rollback
    /// vai precisar reconstruir.
    /// </summary>
    private async Task SemearCertamePublicadoERetificadoAsync()
    {
        await using NpgsqlConnection conexao = new(_postgres.GetConnectionString());
        await conexao.OpenAsync();

        await ExecutarAsync(conexao, $$"""
            INSERT INTO selecao.processos_seletivos (id, nome, tipo, status, created_at, is_deleted)
            VALUES ('{{ProcessoId}}', 'PS publicado', 1, 2, now(), false);

            INSERT INTO selecao.documentos_edital (
                id, processo_seletivo_id, object_key, status, expira_em, hash_sha256, confirmado_em, created_at)
            VALUES ('{{DocumentoId}}', '{{ProcessoId}}', 'editais/ato.pdf', 1, now() + interval '1 day',
                    repeat('b', 64), now(), now());

            INSERT INTO selecao.versoes_configuracao (
                id, processo_seletivo_id, numero_versao, vigente_a_partir_de, schema_version,
                algoritmo_hash, configuracao_congelada_canonica, configuracao_congelada,
                hash_configuracao, ato_criador_id, ato_criador_hash, ato_criador_retifica_id,
                ator_usuario_sub)
            VALUES
                ('{{Guid.CreateVersion7()}}', '{{ProcessoId}}', 1, now() - interval '2 hours', '1.0',
                 'canonical-json/sha256@v1', '\x7b7d'::bytea,
                 jsonb_build_object(
                     'periodo', jsonb_build_object('numero', '001/2026'),
                     'hashesEdital', jsonb_build_object('documentoEditalId', '{{DocumentoId}}')),
                 repeat('a', 64), '{{AtoAberturaId}}', repeat('b', 64), NULL, 'user-sub'),
                ('{{Guid.CreateVersion7()}}', '{{ProcessoId}}', 2, now() - interval '1 hour', '1.0',
                 'canonical-json/sha256@v1', '\x7b7d'::bytea,
                 jsonb_build_object(
                     'periodo', jsonb_build_object('numero', '001/2026'),
                     'hashesEdital', jsonb_build_object('documentoEditalId', '{{DocumentoId}}'),
                     'retificacao', jsonb_build_object('motivo', 'Correção do prazo')),
                 repeat('c', 64), '{{AtoRetificacaoId}}', repeat('d', 64), '{{AtoAberturaId}}', 'user-sub');
            """);
    }

    private async Task<IReadOnlyList<LinhaDeEdital>> ListarEditaisAsync()
    {
        await using NpgsqlConnection conexao = new(_postgres.GetConnectionString());
        await conexao.OpenAsync();

        await using NpgsqlCommand comando = new(
            """
            SELECT id, natureza, numero, data_publicacao, documento_edital_id,
                   edital_retificado_id, motivo_retificacao
            FROM selecao.editais
            """,
            conexao);

        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync();

        List<LinhaDeEdital> linhas = [];
        while (await leitor.ReadAsync())
        {
            linhas.Add(new LinhaDeEdital(
                leitor.GetGuid(0),
                leitor.GetInt32(1),
                await leitor.IsDBNullAsync(2) ? null : leitor.GetString(2),
                await leitor.IsDBNullAsync(3) ? null : await leitor.GetFieldValueAsync<DateTimeOffset>(3),
                leitor.GetGuid(4),
                await leitor.IsDBNullAsync(5) ? null : leitor.GetGuid(5),
                await leitor.IsDBNullAsync(6) ? null : leitor.GetString(6)));
        }

        return linhas;
    }

    private async Task<bool> TabelaEditaisExisteAsync() =>
        await ContarAsync(
            """
            SELECT count(*) FROM information_schema.tables
            WHERE table_schema = 'selecao' AND table_name = 'editais'
            """) > 0;

    private async Task<int> ContarAsync(string sql)
    {
        await using NpgsqlConnection conexao = new(_postgres.GetConnectionString());
        await conexao.OpenAsync();

        await using NpgsqlCommand comando = new(sql, conexao);
        return Convert.ToInt32(await comando.ExecuteScalarAsync(), provider: null);
    }

    private static async Task ExecutarAsync(NpgsqlConnection conexao, string sql)
    {
        await using NpgsqlCommand comando = new(sql, conexao);
        await comando.ExecuteNonQueryAsync();
    }

    private sealed record LinhaDeEdital(
        Guid Id,
        int Natureza,
        string? Numero,
        DateTimeOffset? DataPublicacao,
        Guid DocumentoEditalId,
        Guid? EditalRetificadoId,
        string? MotivoRetificacao);
}
