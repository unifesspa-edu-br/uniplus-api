namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

/// <summary>
/// Configuração EF Core de <see cref="PrecedenciaFase"/> — a tabela
/// <c>precedencia_fase</c> (UNI-REQ-0064, story #851). CRUD-administrado (como
/// <c>FaseCanonica</c>) e <b>seed-governado</b> (como <c>RegraCatalogo</c>/
/// <c>FatoCandidato</c>): as seis arestas estruturais de §3.3 são materializadas
/// via <c>HasData</c> nesta migration, e o CRUD admin continua disponível para
/// acrescentar novas arestas.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class PrecedenciaFaseConfiguration : IEntityTypeConfiguration<PrecedenciaFase>
{
    private const int CodigoMaxLength = 60;
    private const int AuditUserMaxLength = 255;

    public void Configure(EntityTypeBuilder<PrecedenciaFase> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("precedencia_fase", ConfigurarChecks);

        builder.HasKey(p => p.Id);

        builder.Property(p => p.AntecessoraCodigo).HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(p => p.SucessoraCodigo).HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(p => p.PermiteSobreposicao).IsRequired().HasDefaultValue(false);

        // Auditoria (IAuditableEntity)
        builder.Property(p => p.CreatedBy).HasMaxLength(AuditUserMaxLength);
        builder.Property(p => p.UpdatedBy).HasMaxLength(AuditUserMaxLength);

        // Unicidade do par entre arestas vivas (índice parcial) — mesma aresta não
        // pode coexistir duas vezes; soft-delete libera o slot para recriação. A
        // guarda de duplicata do domínio (PrecedenciaFase.Criar) é a primeira linha
        // de defesa; este índice é a segunda, contra corrida/escrita direta.
        builder.HasIndex(p => new { p.AntecessoraCodigo, p.SucessoraCodigo })
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_precedencia_fase_par_vivo");

        builder.HasData(MaterializarSeed());
    }

    private static void ConfigurarChecks(TableBuilder<PrecedenciaFase> table)
    {
        // Formato fechado dos códigos — alinhado ao value object CodigoFase.
        table.HasCheckConstraint(
            "ck_precedencia_fase_antecessora_formato",
            "antecessora_codigo ~ '^[A-Z_]+$'");

        table.HasCheckConstraint(
            "ck_precedencia_fase_sucessora_formato",
            "sucessora_codigo ~ '^[A-Z_]+$'");

        // Domínio fechado das quatorze fases canônicas (defesa em profundidade).
        table.HasCheckConstraint(
            "ck_precedencia_fase_antecessora_canonica",
            $"antecessora_codigo IN ({TokensSql(FaseCanonicaCatalogo.Codigos)})");

        table.HasCheckConstraint(
            "ck_precedencia_fase_sucessora_canonica",
            $"sucessora_codigo IN ({TokensSql(FaseCanonicaCatalogo.Codigos)})");

        // Defesa em profundidade contra self-loop via insert cru (a guarda
        // primária é a factory PrecedenciaFase.Criar).
        table.HasCheckConstraint(
            "ck_precedencia_fase_sem_self_loop",
            "antecessora_codigo <> sucessora_codigo");
    }

    private static string TokensSql(IReadOnlyList<string> tokens) =>
        string.Join(", ", tokens.Select(token => $"'{token}'"));

    /// <summary>
    /// Projeta o seed (<see cref="PrecedenciaFaseSeed.Itens"/>) para linhas que o
    /// <c>HasData</c> congela como literais na migration. O instante-âncora é fixo
    /// (as linhas não passam pelo <c>AuditableInterceptor</c>/<c>SoftDeleteInterceptor</c>);
    /// qualquer mudança futura no seed exige uma nova migration (o EF detecta o
    /// diff), sem alterar as bases já migradas.
    /// </summary>
    private static IEnumerable<object> MaterializarSeed()
    {
        // Instante-âncora fixo do seed (HasData exige valor determinístico).
        DateTimeOffset seedCriadoEm = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        return PrecedenciaFaseSeed.Itens.Select(item => new
        {
            item.Id,
            AntecessoraCodigo = item.AntecessoraCodigo,
            SucessoraCodigo = item.SucessoraCodigo,
            item.PermiteSobreposicao,
            CreatedAt = seedCriadoEm,
            IsDeleted = false,
        });
    }
}
