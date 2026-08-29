namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Configuração EF Core de <see cref="ConfiguracaoTaxaInscricao"/> (issue #1112) — entidade 1:1
/// do agregado <see cref="ProcessoSeletivo"/>.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class ConfiguracaoTaxaInscricaoConfiguration : IEntityTypeConfiguration<ConfiguracaoTaxaInscricao>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public void Configure(EntityTypeBuilder<ConfiguracaoTaxaInscricao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("configuracoes_taxa_inscricao", t => t.HasComment(
            "Configuração de taxa de inscrição e fundamentos de isenção do processo seletivo " +
            "(issue #1112) — entidade dependente 1:1 de processos_seletivos."));
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .ValueGeneratedNever()
            .HasComment("Identificador interno (UUIDv7) — não confundir com o Id do processo seletivo, a FK.");

        builder.Property(c => c.ProcessoSeletivoId)
            .HasComment("Id do processo seletivo dono desta configuração (FK 1:1, cascade delete).");

        builder.Property(c => c.CreatedAt)
            .HasComment("Instante de criação do registro (auditoria, carimbado pelo AuditableInterceptor).");
        builder.Property(c => c.UpdatedAt)
            .HasComment("Instante da última atualização do registro (auditoria, carimbado pelo AuditableInterceptor).");

        builder.Property(c => c.Cobra)
            .IsRequired()
            .HasComment("Declaração explícita de cobrança de taxa — nunca inferida pela ausência da linha (CA-01).");
        builder.Property(c => c.Valor)
            .HasPrecision(ConfiguracaoTaxaInscricao.ValorPrecisao, ConfiguracaoTaxaInscricao.ValorEscala)
            .HasComment("Valor da taxa em reais, positivo quando cobra=true; sempre nulo quando cobra=false (CA-02/CA-03).");

        // fundamentos: lista de tokens (não ordinais de enum — nomes de coluna vendor-neutral,
        // mesmo raciocínio de ConfiguracaoDivulgacao.CamposPublicos). SEM DEFAULT: quem não cobra
        // grava a lista vazia explicitamente, e não há valor a fabricar por ausência de linha.
        builder.Property(c => c.Fundamentos)
            .HasConversion(FundamentosConverter, FundamentosComparer)
            .HasColumnType("jsonb")
            .IsRequired()
            .HasComment("Fundamentos de isenção referenciados (tokens de FundamentoIsencaoCodigo), deduplicados e em ordem canônica; vazio somente quando cobra=false (issue #1310).");
    }

    private static readonly ValueConverter<IReadOnlyList<FundamentoIsencao>, string> FundamentosConverter =
        new(
            fundamentos => SerializeFundamentos(fundamentos),
            json => (IReadOnlyList<FundamentoIsencao>)DeserializeFundamentos(json));

    private static readonly ValueComparer<IReadOnlyList<FundamentoIsencao>> FundamentosComparer =
        new(
            (a, b) => SerializeFundamentos(a) == SerializeFundamentos(b),
            v => v == null ? 0 : SerializeFundamentos(v).GetHashCode(StringComparison.Ordinal),
            v => (IReadOnlyList<FundamentoIsencao>)DeserializeFundamentos(SerializeFundamentos(v)));

    private static string SerializeFundamentos(IReadOnlyList<FundamentoIsencao>? fundamentos) =>
        fundamentos is null
            ? "[]"
            : JsonSerializer.Serialize(fundamentos.Select(static f => f.ToCodigo()), JsonOptions);

    private static List<FundamentoIsencao> DeserializeFundamentos(string json) =>
        string.IsNullOrEmpty(json)
            ? []
            : [.. (JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? []).Select(static c => FundamentoIsencaoCodigo.FromCodigo(c))];
}
