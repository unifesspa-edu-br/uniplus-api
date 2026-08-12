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

        builder.ToTable("configuracoes_taxa_inscricao");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Cobra).IsRequired();
        builder.Property(c => c.Valor).HasPrecision(ConfiguracaoTaxaInscricao.ValorPrecisao, ConfiguracaoTaxaInscricao.ValorEscala);
        builder.Property(c => c.ConfirmacaoFundamentos).IsRequired();

        // fundamentos: lista de tokens (não ordinais de enum — nomes de coluna vendor-neutral,
        // mesmo raciocínio de ConfiguracaoDivulgacao.CamposPublicos). SEM DEFAULT: lista vazia é
        // estado que a factory produz explicitamente (Cobra sem fundamento), não um valor a
        // fabricar por ausência de linha.
        builder.Property(c => c.Fundamentos)
            .HasConversion(FundamentosConverter, FundamentosComparer)
            .HasColumnType("jsonb")
            .IsRequired();
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
