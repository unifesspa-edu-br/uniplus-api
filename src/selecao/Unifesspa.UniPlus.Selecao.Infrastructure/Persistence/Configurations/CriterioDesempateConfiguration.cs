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
/// Configuração EF Core de <see cref="CriterioDesempate"/> (Story #774,
/// modelagem P-B §2.6) — entidade filha do agregado
/// <see cref="ProcessoSeletivo"/>.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class CriterioDesempateConfiguration : IEntityTypeConfiguration<CriterioDesempate>
{
    private const int RegraCodigoMaxLength = 128;
    private const int RegraVersaoMaxLength = 16;
    private const int HashLength = 64;

    public void Configure(EntityTypeBuilder<CriterioDesempate> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("criterios_desempate");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Ordem).IsRequired();

        builder.OwnsOne(c => c.Regra, regra =>
        {
            regra.Property(r => r.Codigo).HasColumnName("regra_codigo").HasMaxLength(RegraCodigoMaxLength).IsRequired();
            regra.Property(r => r.Versao).HasColumnName("regra_versao").HasMaxLength(RegraVersaoMaxLength).IsRequired();
            regra.Property(r => r.Hash).HasColumnName("regra_hash").HasMaxLength(HashLength).IsFixedLength().IsRequired();
        });
        builder.Navigation(c => c.Regra).IsRequired();

        // Args polimórficos (união fechada de 4 variantes, ADR-0058 §"Discriminated
        // union" — mesmo molde de PredicadoObrigatoriedade): serializados via
        // System.Text.Json com o discriminador "$tipo".
        //
        // Coluna "json" (NÃO "jsonb") de propósito: o desserializador polimórfico
        // do System.Text.Json exige o discriminador "$tipo" como a PRIMEIRA
        // propriedade do objeto. O tipo "jsonb" do Postgres normaliza/reordena as
        // chaves (por tamanho, depois alfabeticamente) na escrita — uma variante
        // com uma propriedade mais curta que "$tipo" (5 chars), como
        // ArgsDesempatePredicadoFato.Fato (4 chars), reordenaria "fato" para antes
        // de "$tipo", quebrando a leitura com NotSupportedException. O tipo "json"
        // preserva o texto exatamente como escrito (sem reordenar), evitando o
        // problema — sem custo aqui, já que esta coluna nunca é consultada via
        // operadores jsonb (containment/GIN).
        builder.Property(c => c.Args)
            .HasConversion(ArgsConverter, ArgsComparer)
            .HasColumnType("json")
            .IsRequired();

        // UNIQUE(processo_seletivo_id, ordem): a rejeição de ordem duplicada
        // em ProcessoSeletivo.DefinirCriteriosDesempate é check-then-act
        // não-atômico — duas escritas concorrentes carregando o mesmo agregado
        // tracked poderiam ambas passar a checagem em memória e inserir a
        // mesma ordem (achado Codex). A constraint do banco é a defesa
        // realmente atômica.
        builder.HasIndex(c => new { c.ProcessoSeletivoId, c.Ordem })
            .IsUnique()
            .HasDatabaseName("ux_criterios_desempate_processo_ordem");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    private static readonly ValueConverter<ArgsCriterioDesempate, string> ArgsConverter =
        new(
            args => JsonSerializer.Serialize(args, JsonOptions),
            json => JsonSerializer.Deserialize<ArgsCriterioDesempate>(json, JsonOptions)!);

    private static readonly ValueComparer<ArgsCriterioDesempate> ArgsComparer =
        new(
            (a, b) => Serialize(a) == Serialize(b),
            v => v == null ? 0 : Serialize(v).GetHashCode(StringComparison.Ordinal),
            v => Deserialize(Serialize(v)));

    private static string Serialize(ArgsCriterioDesempate? v) =>
        v is null ? string.Empty : JsonSerializer.Serialize(v, JsonOptions);

    private static ArgsCriterioDesempate Deserialize(string json) =>
        JsonSerializer.Deserialize<ArgsCriterioDesempate>(json, JsonOptions)!;
}
