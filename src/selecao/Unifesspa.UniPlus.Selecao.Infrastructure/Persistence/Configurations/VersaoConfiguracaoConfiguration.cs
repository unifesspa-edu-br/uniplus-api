namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Domain.Entities;

/// <summary>
/// Configuração EF Core da tabela append-only <c>versoes_configuracao</c>
/// (RN08, ADR-0104/0063/0100). Sem soft-delete, sem audit fields, sem updates
/// — qualquer mutação fora de <c>INSERT</c> é incidente operacional, e o banco
/// a bloqueia por trigger.
/// </summary>
/// <remarks>
/// <c>ato_criador_id</c> e <c>ato_criador_retifica_id</c> são referências
/// <b>por valor</b> (ADR-0061) — deliberadamente <b>sem</b> chave estrangeira,
/// nem para <c>editais</c> hoje, nem para <c>publicacoes.ato_normativo</c>
/// depois de #804. A garantia forense é local: <c>NOT NULL</c> mais
/// <c>UNIQUE</c> sobre o ato criador — mais forte que o antigo
/// <c>ux_snapshot_publicacao_edital_id</c>, porque impede também que um mesmo
/// ato crie duas versões.
/// </remarks>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class VersaoConfiguracaoConfiguration : IEntityTypeConfiguration<VersaoConfiguracao>
{
    private const int HashLength = 64;
    private const int SchemaVersionMaxLength = 20;
    private const int AlgoritmoHashMaxLength = 60;
    private const int AtorUsuarioSubMaxLength = 255;

    public void Configure(EntityTypeBuilder<VersaoConfiguracao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            "versoes_configuracao",
            t =>
            {
                t.HasCheckConstraint(
                    "ck_versoes_configuracao_numero_positivo",
                    "numero_versao > 0");

                // Contrato simétrico da abertura (ADR-0104): a versão 1 não
                // retifica ninguém; toda versão N > 1 retifica. Não há estado
                // ambíguo — nem versão 1 emendando um ato, nem sucessora órfã.
                t.HasCheckConstraint(
                    "ck_versoes_configuracao_contrato_abertura",
                    "(numero_versao = 1 AND ato_criador_retifica_id IS NULL) "
                    + "OR (numero_versao > 1 AND ato_criador_retifica_id IS NOT NULL)");

                // Um ato não retifica a si mesmo — a cadeia de atos é linear.
                t.HasCheckConstraint(
                    "ck_versoes_configuracao_nao_autorretifica",
                    "ato_criador_retifica_id IS NULL OR ato_criador_retifica_id <> ato_criador_id");

                // Formato dos hashes: SHA-256 hex minúsculo. Defesa em
                // profundidade contra insert cru (a factory já recusa).
                t.HasCheckConstraint(
                    "ck_versoes_configuracao_hash_configuracao",
                    "hash_configuracao ~ '^[0-9a-f]{64}$'");

                t.HasCheckConstraint(
                    "ck_versoes_configuracao_ato_criador_hash",
                    "ato_criador_hash ~ '^[0-9a-f]{64}$'");

                // Um Guid.Empty não referencia ato algum.
                t.HasCheckConstraint(
                    "ck_versoes_configuracao_ato_criador_nao_zero",
                    "ato_criador_id <> '00000000-0000-0000-0000-000000000000'");
            });

        builder.HasKey(v => v.Id);

        builder.Property(v => v.ProcessoSeletivoId).IsRequired();
        builder.Property(v => v.NumeroVersao).IsRequired();
        builder.Property(v => v.VigenteAPartirDe).IsRequired();

        builder.Property(v => v.SchemaVersion)
            .HasMaxLength(SchemaVersionMaxLength)
            .IsRequired();

        builder.Property(v => v.AlgoritmoHash)
            .HasMaxLength(AlgoritmoHashMaxLength)
            .IsRequired();

        // Bytes canônicos (ADR-0100 item 6) — base do hash; fonte única de verdade.
        builder.Property(v => v.ConfiguracaoCongeladaCanonica)
            .HasColumnType("bytea")
            .IsRequired();

        // Derivado por parsing UTF-8 dos bytes canônicos — só consulta SQL
        // (ADR-0100 item 7). O banco não re-serializa nem reordena chaves.
        builder.Property(v => v.ConfiguracaoCongelada)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(v => v.HashConfiguracao)
            .HasMaxLength(HashLength)
            .IsFixedLength()
            .IsRequired();

        builder.Property(v => v.AtoCriadorId).IsRequired();

        builder.Property(v => v.AtoCriadorHash)
            .HasMaxLength(HashLength)
            .IsFixedLength()
            .IsRequired();

        builder.Property(v => v.AtoCriadorRetificaId);

        builder.Property(v => v.AtorUsuarioSub)
            .HasMaxLength(AtorUsuarioSubMaxLength)
            .IsRequired();

        // Única FK da tabela — intra-módulo, para a raiz do certame. Sem nav
        // property: a forense não navega de volta (mesmo padrão de
        // ObrigatoriedadeLegalHistorico). Nome explícito porque o derivado pelo
        // EF estoura o limite de 63 chars do PostgreSQL.
        builder.HasOne<ProcessoSeletivo>()
            .WithMany()
            .HasForeignKey(v => v.ProcessoSeletivoId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_versoes_configuracao_processo");

        // Numeração monotônica por processo. Garantia dura contra a corrida
        // check-then-act: duas publicações concorrentes derivam o mesmo N+1 e a
        // segunda colide aqui (a contiguidade em si é imposta pelo trigger de
        // sucessão, que enxerga a tabela inteira).
        builder.HasIndex(v => new { v.ProcessoSeletivoId, v.NumeroVersao })
            .IsUnique()
            .HasDatabaseName("ux_versoes_configuracao_processo_numero");

        // Um ato cria no máximo uma versão (ADR-0104). Único GLOBALMENTE, não
        // por processo: o identificador do ato é único no registro que o emite.
        builder.HasIndex(v => v.AtoCriadorId)
            .IsUnique()
            .HasDatabaseName("ux_versoes_configuracao_ato_criador");

        // Suporta o seletor de vigência (#803): maior vigente_a_partir_de ≤
        // instante, desempatando por numero_versao. NÃO é único — dois atos
        // publicados no mesmo instante deixam de colidir (é o ponto da
        // ADR-0104), e o desempate cabe ao número da versão.
        builder.HasIndex(v => new { v.ProcessoSeletivoId, v.VigenteAPartirDe, v.NumeroVersao })
            .IsDescending(false, true, true)
            .HasDatabaseName("ix_versoes_configuracao_processo_vigencia");
    }
}
