namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Seed;

/// <summary>
/// Configuração EF Core da entidade <see cref="RegraCatalogo"/> — a biblioteca
/// <c>rol_de_regras</c> (Story #772).
/// </summary>
/// <remarks>
/// <para>
/// A tabela é <strong>append-only e seed-governada</strong>: não há CRUD de
/// administrador, a versão de uma regra é imutável e a única escrita é o seed.
/// Por isso a entidade deriva de <c>EntityBase</c> puro (sem soft-delete) e o
/// índice <c>UNIQUE (codigo, versao)</c> é <strong>total</strong> (não parcial)
/// — é justamente o que habilita <c>v1</c>/<c>v2</c> coexistentes referenciáveis.
/// O append-only é imposto por convenção (ausência de API de mutação; leitura
/// via <c>IRegraCatalogoReader</c>) e por fitness test, na linha do ADR-0063 —
/// não por gatilho de banco (o repositório não usa gatilhos).
/// </para>
/// <para>
/// <c>esquema_args</c> (schema dos argumentos) e <c>invariantes</c> são
/// colunas <c>jsonb</c>; o <c>hash</c> content-addressable da definição é
/// computado no domínio (<c>HashCanonicalComputer.ComputeRegraCatalogo</c>).
/// </para>
/// </remarks>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class RegraCatalogoConfiguration : IEntityTypeConfiguration<RegraCatalogo>
{
    private const int CodigoMaxLength = 128;
    private const int VersaoMaxLength = 16;
    private const int TipoMaxLength = 48;
    private const int BaseLegalMaxLength = 500;
    private const int HashLength = 64;

    public void Configure(EntityTypeBuilder<RegraCatalogo> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("rol_de_regras");
        builder.HasKey(r => r.Id);

        // Chave Guid v7 gerada no domínio (EntityBase): ValueGeneratedNever
        // força o EF a tratar a chave como fornecida pela aplicação (convenção
        // do repo — evita o heurístico Add-vs-Update sobre chave não-default).
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Codigo).HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(r => r.Versao).HasMaxLength(VersaoMaxLength).IsRequired();

        builder.Property(r => r.Tipo)
            .HasConversion(TipoConverter)
            .HasMaxLength(TipoMaxLength)
            .IsRequired();

        builder.Property(r => r.EsquemaArgs)
            .HasConversion(JsonElementConverter, JsonElementComparer)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(r => r.Invariantes)
            .HasConversion(JsonElementConverter, JsonElementComparer)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(r => r.BaseLegal).HasMaxLength(BaseLegalMaxLength).IsRequired();

        builder.Property(r => r.Hash)
            .HasMaxLength(HashLength)
            .IsFixedLength()
            .IsRequired();

        // UNIQUE total (codigo, versao): uma versão de regra é única e imutável;
        // v1/v2 coexistem como linhas distintas referenciáveis pelo snapshot.
        builder.HasIndex(r => new { r.Codigo, r.Versao })
            .IsUnique()
            .HasDatabaseName("ux_rol_de_regras_codigo_versao");

        // Resolve a referência da configuração pelo hash content-addressable.
        builder.HasIndex(r => r.Hash)
            .HasDatabaseName("ix_rol_de_regras_hash");

        SemearRegrasV1(builder);
    }

    /// <summary>
    /// Seed das regras <c>v1</c> via <see cref="RelationalEntityTypeBuilderExtensions"/>
    /// (<c>HasData</c>): o EF congela as linhas como literais na migration no
    /// momento do scaffold — a migration é um snapshot imutável, e qualquer
    /// mudança futura em <see cref="RegraCatalogoSeed.Itens"/> exige uma nova
    /// migration (o EF detecta o diff), sem alterar as bases já migradas. O
    /// <c>hash</c> é derivado da mesma lógica do domínio, então a linha semeada
    /// é content-addressable por construção.
    /// </summary>
    private static void SemearRegrasV1(EntityTypeBuilder<RegraCatalogo> builder)
    {
        // Instante-âncora fixo do seed (HasData exige valor determinístico;
        // as linhas não passam pelo AuditableInterceptor).
        DateTimeOffset seedCriadoEm = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        builder.HasData(RegraCatalogoSeed.Itens.Select(item => new
        {
            item.Id,
            item.Codigo,
            item.Versao,
            item.Tipo,
            EsquemaArgs = Parse(item.EsquemaArgsJson),
            Invariantes = Parse(item.InvariantesJson),
            item.BaseLegal,
            Hash = item.ComputarHash(),
            CreatedAt = seedCriadoEm,
        }));
    }

    /// <summary>Mapeia <see cref="TipoRegra"/> ↔ o código canônico snake_case (fonte única em <see cref="TipoRegraCodigo"/>).</summary>
    private static readonly ValueConverter<TipoRegra, string> TipoConverter =
        new(tipo => tipo.ToCodigo(), codigo => TipoRegraCodigo.FromCodigo(codigo));

    /// <summary>
    /// Serializa o payload jsonb (<c>esquema_args</c>/<c>invariantes</c>) pelo
    /// texto bruto do <see cref="JsonElement"/> e o reidrata desanexado do
    /// <see cref="JsonDocument"/> de origem (<see cref="JsonElement.Clone"/>).
    /// </summary>
    private static readonly ValueConverter<JsonElement, string> JsonElementConverter =
        new(element => element.GetRawText(), json => Parse(json));

    /// <summary>
    /// ValueComparer por texto bruto — suficiente para o change-tracker de uma
    /// entidade append-only (nunca mutada após o seed).
    /// </summary>
    private static readonly ValueComparer<JsonElement> JsonElementComparer =
        new(
            (a, b) => a.GetRawText() == b.GetRawText(),
            v => v.GetRawText().GetHashCode(StringComparison.Ordinal),
            v => Parse(v.GetRawText()));

    private static JsonElement Parse(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
