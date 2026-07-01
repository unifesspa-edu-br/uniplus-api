namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class ModalidadeConfiguration : IEntityTypeConfiguration<Modalidade>
{
    private const int CodigoMaxLength = 60;
    private const int DescricaoMaxLength = 300;
    private const int EnumTokenMaxLength = 30;
    private const int CodigoReferenciaMaxLength = 60;
    private const int BaseLegalMaxLength = 500;
    private const int AuditUserMaxLength = 255;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
    };

    public void Configure(EntityTypeBuilder<Modalidade> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("modalidade", ConfigurarChecks);

        builder.HasKey(m => m.Id);

        // Codigo é value object imutável — persistido por valor como varchar via
        // CodigoModalidadeValueConverter (reidratação fail-fast). O CHECK de formato
        // e o índice único parcial abaixo protegem a coluna.
        builder.Property(m => m.Codigo)
            .HasConversion<CodigoModalidadeValueConverter>()
            .HasMaxLength(CodigoMaxLength)
            .IsRequired();

        builder.Property(m => m.Descricao).HasMaxLength(DescricaoMaxLength);

        builder.Property(m => m.NaturezaLegal)
            .HasConversion<NaturezaLegalValueConverter>()
            .HasMaxLength(EnumTokenMaxLength)
            .IsRequired();

        builder.Property(m => m.ComposicaoVagas)
            .HasConversion<ComposicaoVagasValueConverter>()
            .HasMaxLength(EnumTokenMaxLength)
            .IsRequired();

        builder.Property(m => m.ComposicaoOrigem).HasMaxLength(CodigoReferenciaMaxLength);

        // Enums nullable: o converter cobre o valor não-nulo; o EF encapsula null.
        builder.Property(m => m.RegraRemanejamento)
            .HasConversion(new RegraRemanejamentoValueConverter())
            .HasMaxLength(EnumTokenMaxLength);

        builder.Property(m => m.AcaoQuandoIndeferido)
            .HasConversion(new AcaoQuandoIndeferidoValueConverter())
            .HasMaxLength(EnumTokenMaxLength);

        // remanejamento_args: value object serializado como jsonb (chaves fixadas por
        // JsonPropertyName — destino/par/fallback). Default '{}' cobre inserts crus.
        builder.Property(m => m.RemanejamentoArgs)
            .HasConversion(RemanejamentoArgsConverter, RemanejamentoArgsComparer)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb")
            .IsRequired();

        // criterios_cumulativos: lista de strings serializada como jsonb. Default '[]'.
        builder.Property(m => m.CriteriosCumulativos)
            .HasConversion(CriteriosConverter, CriteriosComparer)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'::jsonb")
            .IsRequired();

        builder.Property(m => m.BaseLegal).HasMaxLength(BaseLegalMaxLength);

        // Auditoria (IAuditableEntity)
        builder.Property(m => m.CreatedBy).HasMaxLength(AuditUserMaxLength);
        builder.Property(m => m.UpdatedBy).HasMaxLength(AuditUserMaxLength);

        // Unicidade do código entre modalidades vivas (índice parcial) — uma
        // modalidade viva por código; soft-delete libera o slot para recriação.
        builder.HasIndex(m => m.Codigo)
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_modalidade_codigo_vivo");
    }

    private static void ConfigurarChecks(TableBuilder<Modalidade> table)
    {
        // Domínios fechados dos enums (defesa em profundidade contra inserts crus).
        table.HasCheckConstraint(
            "ck_modalidade_natureza_legal",
            $"natureza_legal IN ({TokensSql(NaturezasLegais.TokensCanonicos)})");

        table.HasCheckConstraint(
            "ck_modalidade_composicao_vagas",
            $"composicao_vagas IN ({TokensSql(ComposicoesVagas.TokensCanonicos)})");

        table.HasCheckConstraint(
            "ck_modalidade_regra_remanejamento",
            $"regra_remanejamento IS NULL OR regra_remanejamento IN ({TokensSql(RegrasRemanejamento.TokensCanonicos)})");

        table.HasCheckConstraint(
            "ck_modalidade_acao_quando_indeferido",
            $"acao_quando_indeferido IS NULL OR acao_quando_indeferido IN ({TokensSql(AcoesQuandoIndeferido.TokensCanonicos)})");

        // Formato fechado do código — alinhado ao value object CodigoModalidade.
        table.HasCheckConstraint(
            "ck_modalidade_codigo_formato",
            "codigo ~ '^[A-Z0-9_]+$'");

        // Equivalência exata RETIRA_DE ⟺ composicao_origem preenchida (invariante 4),
        // como igualdade de dois booleanos — null-safe (ambos os lados nunca são null).
        table.HasCheckConstraint(
            "ck_modalidade_retira_de_origem",
            "(composicao_vagas = 'RETIRA_DE') = (composicao_origem IS NOT NULL)");
    }

    private static string TokensSql(IReadOnlyList<string> tokens) =>
        string.Join(", ", tokens.Select(token => $"'{token}'"));

    // ── Conversores/comparadores jsonb ────────────────────────────────────────

    private static readonly ValueConverter<RemanejamentoArgs, string> RemanejamentoArgsConverter =
        new(
            args => JsonSerializer.Serialize(args, JsonOptions),
            json => JsonSerializer.Deserialize<RemanejamentoArgs>(json, JsonOptions) ?? RemanejamentoArgs.Vazio);

    private static readonly ValueComparer<RemanejamentoArgs> RemanejamentoArgsComparer =
        new(
            (a, b) => SerializeArgs(a) == SerializeArgs(b),
            v => v == null ? 0 : SerializeArgs(v).GetHashCode(StringComparison.Ordinal),
            v => DeserializeArgs(SerializeArgs(v)));

    private static string SerializeArgs(RemanejamentoArgs? v) =>
        v is null ? string.Empty : JsonSerializer.Serialize(v, JsonOptions);

    private static RemanejamentoArgs DeserializeArgs(string json) =>
        string.IsNullOrEmpty(json)
            ? RemanejamentoArgs.Vazio
            : JsonSerializer.Deserialize<RemanejamentoArgs>(json, JsonOptions) ?? RemanejamentoArgs.Vazio;

    private static readonly ValueConverter<IReadOnlyList<string>, string> CriteriosConverter =
        new(
            criterios => JsonSerializer.Serialize(criterios, JsonOptions),
            json => (IReadOnlyList<string>)DeserializeCriterios(json));

    private static readonly ValueComparer<IReadOnlyList<string>> CriteriosComparer =
        new(
            (a, b) => SerializeCriterios(a) == SerializeCriterios(b),
            v => v == null ? 0 : SerializeCriterios(v).GetHashCode(StringComparison.Ordinal),
            v => (IReadOnlyList<string>)DeserializeCriterios(SerializeCriterios(v)));

    private static string SerializeCriterios(IReadOnlyList<string>? v) =>
        v is null ? "[]" : JsonSerializer.Serialize(v, JsonOptions);

    private static List<string> DeserializeCriterios(string json) =>
        string.IsNullOrEmpty(json)
            ? []
            : JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];
}
