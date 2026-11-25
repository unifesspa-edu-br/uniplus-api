namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Configuração EF Core de <see cref="CriterioDesempate"/> (Story #774) —
/// entidade filha do agregado <see cref="ProcessoSeletivo"/>.
/// </summary>
internal sealed class CriterioDesempateConfiguration : IEntityTypeConfiguration<CriterioDesempate>
{

    public void Configure(EntityTypeBuilder<CriterioDesempate> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("criterios_desempate");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Ordem).IsRequired();

        builder.OwnsOne(c => c.Regra, regra => regra.ConfigurarReferenciaRegra("regra"));
        builder.Navigation(c => c.Regra).IsRequired();

        // Parâmetros polimórficos (união fechada de 5 variantes, ADR-0058 §"Discriminated
        // union" — mesmo molde de PredicadoObrigatoriedade): serializados via
        // System.Text.Json com o discriminador "$tipo".
        //
        // Coluna "json" (NÃO "jsonb") de propósito: o desserializador polimórfico
        // do System.Text.Json exige o discriminador "$tipo" como a PRIMEIRA
        // propriedade do objeto. O tipo "jsonb" do Postgres normaliza/reordena as
        // chaves (por tamanho, depois alfabeticamente) na escrita — uma variante
        // com uma propriedade mais curta que "$tipo" (5 caracteres), poderia reordená-la para antes
        // de "$tipo", quebrando a leitura com NotSupportedException. O tipo "json"
        // preserva o texto exatamente como escrito (sem reordenar), evitando o
        // problema — sem custo aqui, já que esta coluna nunca é consultada via
        // operadores jsonb (containment/GIN).
        builder.Property(c => c.Parametros)
            .HasColumnName("parametros")
            .HasConversion(ParametrosConverter, ParametrosComparer)
            .HasColumnType("json")
            .IsRequired();

        // UNIQUE(processo_seletivo_id, ordem): a rejeição de ordem duplicada
        // em ProcessoSeletivo.DefinirCriteriosDesempate é check-then-act
        // não-atômico — duas escritas concorrentes carregando o mesmo agregado
        // tracked poderiam ambas passar a checagem em memória e inserir a
        // mesma ordem. A constraint do banco é a defesa
        // realmente atômica.
        builder.HasIndex(c => new { c.ProcessoSeletivoId, c.Ordem })
            .IsUnique()
            .HasDatabaseName("ux_criterios_desempate_processo_ordem");
    }

    private static readonly JsonSerializerOptions JsonOptions = OpcoesJsonDeDocumento.Criar(new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
        WriteIndented = false,
    });

    private static readonly ValueConverter<ParametrosCriterioDesempate, string> ParametrosConverter =
        new(
            parametros => JsonSerializer.Serialize(parametros, JsonOptions),
            json => JsonSerializer.Deserialize<ParametrosCriterioDesempate>(json, JsonOptions)!);

    private static readonly ValueComparer<ParametrosCriterioDesempate> ParametrosComparer =
        new(
            (a, b) => Serialize(a) == Serialize(b),
            v => v == null ? 0 : Serialize(v).GetHashCode(StringComparison.Ordinal),
            v => Deserialize(Serialize(v)));

    private static string Serialize(ParametrosCriterioDesempate? v) =>
        v is null ? string.Empty : JsonSerializer.Serialize(v, JsonOptions);

    private static ParametrosCriterioDesempate Deserialize(string json) =>
        JsonSerializer.Deserialize<ParametrosCriterioDesempate>(json, JsonOptions)!;
}
