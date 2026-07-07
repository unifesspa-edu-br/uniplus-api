namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Configuração EF Core de <see cref="RegraEliminacao"/> (Story #775,
/// modelagem P-B §2.5) — entidade filha de
/// <see cref="ConfiguracaoClassificacao"/>, cardinalidade múltipla.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class RegraEliminacaoConfiguration : IEntityTypeConfiguration<RegraEliminacao>
{
    private const int RegraCodigoMaxLength = 128;
    private const int RegraVersaoMaxLength = 16;
    private const int HashLength = 64;

    public void Configure(EntityTypeBuilder<RegraEliminacao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("regras_eliminacao");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.OwnsOne(r => r.Regra, regra =>
        {
            regra.Property(x => x.Codigo).HasColumnName("regra_codigo").HasMaxLength(RegraCodigoMaxLength).IsRequired();
            regra.Property(x => x.Versao).HasColumnName("regra_versao").HasMaxLength(RegraVersaoMaxLength).IsRequired();
            regra.Property(x => x.Hash).HasColumnName("regra_hash").HasMaxLength(HashLength).IsFixedLength().IsRequired();
        });
        builder.Navigation(r => r.Regra).IsRequired();

        // Args polimórficos (união fechada de 3 variantes) — mesmo padrão de
        // CriterioDesempateConfiguration (Story #774): coluna "json" (NÃO
        // "jsonb"), porque o desserializador polimórfico do System.Text.Json
        // exige o discriminador "$tipo" como primeira propriedade, e o
        // "jsonb" do Postgres reordena chaves por tamanho na escrita.
        builder.Property(r => r.Args)
            .HasConversion(ArgsConverter, ArgsComparer)
            .HasColumnType("json")
            .IsRequired();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    private static readonly ValueConverter<ArgsRegraEliminacao, string> ArgsConverter =
        new(
            args => JsonSerializer.Serialize(args, JsonOptions),
            json => JsonSerializer.Deserialize<ArgsRegraEliminacao>(json, JsonOptions)!);

    private static readonly ValueComparer<ArgsRegraEliminacao> ArgsComparer =
        new(
            (a, b) => Serialize(a) == Serialize(b),
            v => v == null ? 0 : Serialize(v).GetHashCode(StringComparison.Ordinal),
            v => Deserialize(Serialize(v)));

    private static string Serialize(ArgsRegraEliminacao? v) =>
        v is null ? string.Empty : JsonSerializer.Serialize(v, JsonOptions);

    private static ArgsRegraEliminacao Deserialize(string json) =>
        JsonSerializer.Deserialize<ArgsRegraEliminacao>(json, JsonOptions)!;
}
