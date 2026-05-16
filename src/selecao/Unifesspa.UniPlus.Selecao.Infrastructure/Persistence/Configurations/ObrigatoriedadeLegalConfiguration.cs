namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Configurations;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Configuração EF Core da entidade <see cref="ObrigatoriedadeLegal"/> +
/// junction <c>obrigatoriedade_legal_areas_de_interesse</c> (Story #460).
/// Primeira aplicação do template <see cref="AreaVisibilityConfiguration{T}"/>
/// em Selecao (ADR-0060).
/// </summary>
/// <remarks>
/// <para>
/// A propriedade in-memory <see cref="ObrigatoriedadeLegal.AreasDeInteresse"/>
/// é deliberadamente ignorada do model EF: a verdade temporal mora na
/// junction, configurada pela base via <c>ConfigureAreaVisibility</c>. A
/// reconciliação set ↔ junction é responsabilidade do repositório admin
/// (#461).
/// </para>
/// <para>
/// <see cref="ObrigatoriedadeLegal.Hash"/> entra como índice UNIQUE PARCIAL
/// (apenas para linhas não soft-deleted) — Postgres consegue expressar via
/// <c>HasFilter</c>, garantindo CA-02 sem impedir reuso de hash quando uma
/// regra anterior foi soft-deleted.
/// </para>
/// </remarks>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class ObrigatoriedadeLegalConfiguration()
    : AreaVisibilityConfiguration<ObrigatoriedadeLegal>("obrigatoriedade_legal")
{
    private const int TipoEditalCodigoMaxLength = 64;
    private const int RegraCodigoMaxLength = 128;
    private const int CategoriaMaxLength = 32;
    private const int DescricaoHumanaMaxLength = 1000;
    private const int BaseLegalMaxLength = 500;
    private const int AtoNormativoUrlMaxLength = 1000;
    private const int PortariaInternaCodigoMaxLength = 128;
    private const int HashLength = 64;
    private const int ProprietarioMaxLength = 32;
    private const int AuditUserMaxLength = 255;

    public override void Configure(EntityTypeBuilder<ObrigatoriedadeLegal> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("obrigatoriedades_legais");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.TipoEditalCodigo)
            .HasMaxLength(TipoEditalCodigoMaxLength)
            .IsRequired();

        builder.Property(o => o.Categoria)
            .HasConversion<string>()
            .HasMaxLength(CategoriaMaxLength)
            .IsRequired();

        builder.Property(o => o.RegraCodigo)
            .HasMaxLength(RegraCodigoMaxLength)
            .IsRequired();

        builder.Property(o => o.Predicado)
            .HasConversion(PredicadoConverter, PredicadoComparer)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(o => o.DescricaoHumana)
            .HasMaxLength(DescricaoHumanaMaxLength)
            .IsRequired();

        builder.Property(o => o.BaseLegal)
            .HasMaxLength(BaseLegalMaxLength)
            .IsRequired();

        builder.Property(o => o.AtoNormativoUrl)
            .HasMaxLength(AtoNormativoUrlMaxLength);

        builder.Property(o => o.PortariaInternaCodigo)
            .HasMaxLength(PortariaInternaCodigoMaxLength);

        builder.Property(o => o.VigenciaInicio).IsRequired();
        builder.Property(o => o.VigenciaFim);

        builder.Property(o => o.Hash)
            .HasMaxLength(HashLength)
            .IsFixedLength()
            .IsRequired();

        // Nullable struct: o EF aplica o ValueConverter da forma não-nullável
        // (AreaCodigoValueConverter) transparentemente — null em Proprietario
        // mapeia para NULL na coluna sem precisar de adaptação manual.
        builder.Property(o => o.Proprietario)
            .HasConversion<AreaCodigoValueConverter>()
            .HasMaxLength(ProprietarioMaxLength);

        // Audit (IAuditableEntity) — preenchido pelo AuditableInterceptor.
        builder.Property(o => o.CreatedBy).HasMaxLength(AuditUserMaxLength);
        builder.Property(o => o.UpdatedBy).HasMaxLength(AuditUserMaxLength);

        // Soft delete (EntityBase) — DeletedBy guarda o sub do JWT do admin
        // responsável pela desativação. Sem MaxLength explícito o EF emite
        // `text` para a coluna; alinhamos com os demais campos de audit
        // (255 chars) para coerência de schema e suporte a índice parcial
        // futuro se preciso.
        builder.Property(o => o.DeletedBy).HasMaxLength(AuditUserMaxLength);

        // AreasDeInteresse vive na junction temporal (ADR-0060). O field
        // backing in-memory é ignorado no model — invariante 1 do ADR-0057
        // é responsabilidade do domínio na hora da escrita.
        builder.Ignore(o => o.AreasDeInteresse);

        builder.HasQueryFilter(o => !o.IsDeleted);

        // CA-02 — UNIQUE parcial sobre Hash. Soft-deletes não disputam o slot,
        // permitindo recriar uma regra com hash idêntico após a desativação
        // da anterior (cenário de versionamento canônico do ADR-0058).
        builder.HasIndex(o => o.Hash)
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ux_obrigatoriedades_legais_hash_ativos");

        // UNIQUE parcial sobre RegraCodigo entre regras ativas (Codex P1 de
        // #461). O ExisteRegraCodigoAtivoAsync no handler é check-then-act
        // não-atômico — duas escritas concorrentes com o mesmo RegraCodigo
        // poderiam ambas passar a checagem e commitar, criando o cenário
        // ambíguo de "duas regras vigentes com mesmo código simbólico"
        // mesmo que seus hashes canônicos divirjam por um campo qualquer
        // (vigência, base legal). Constraint do banco é a única defesa
        // realmente atômica; o ExisteRegraCodigoAtivoAsync vira fast path
        // pra emitir 409 ProblemDetails antes do INSERT em casos não-race.
        builder.HasIndex(o => o.RegraCodigo)
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ux_obrigatoriedades_legais_regra_codigo_ativos");

        ConfigureAreaVisibility(builder);
    }

    /// <summary>
    /// Serializa polimorficamente o predicado para JSON (atributos
    /// <c>[JsonPolymorphic]</c>/<c>[JsonDerivedType]</c> em
    /// <see cref="PredicadoObrigatoriedade"/>) usando as
    /// <see cref="HashCanonicalComputer.CanonicalOptions"/> — wire format
    /// idêntico ao do hash canônico e do snapshot do interceptor. Mantém
    /// round-trip estável entre persistência, hash, histórico e exposição
    /// HTTP futura (#461).
    /// </summary>
    private static readonly ValueConverter<PredicadoObrigatoriedade, string> PredicadoConverter =
        new(
            predicado => JsonSerializer.Serialize(predicado, HashCanonicalComputer.CanonicalOptions),
            json => JsonSerializer.Deserialize<PredicadoObrigatoriedade>(json, HashCanonicalComputer.CanonicalOptions)!);

    /// <summary>
    /// ValueComparer baseado em round-trip JSON com
    /// <see cref="HashCanonicalComputer.CanonicalOptions"/> — alinhamento
    /// com o hash garante que mudanças semânticas (incluindo enums futuros
    /// dentro do predicado) sejam detectadas pelo change-tracker
    /// exatamente como invalidam o hash.
    /// </summary>
    private static readonly ValueComparer<PredicadoObrigatoriedade> PredicadoComparer =
        new(
            (a, b) => Serialize(a) == Serialize(b),
            v => v == null ? 0 : Serialize(v).GetHashCode(StringComparison.Ordinal),
            v => Deserialize(Serialize(v)));

    private static string Serialize(PredicadoObrigatoriedade? v) =>
        v is null ? string.Empty : JsonSerializer.Serialize(v, HashCanonicalComputer.CanonicalOptions);

    private static PredicadoObrigatoriedade Deserialize(string json) =>
        JsonSerializer.Deserialize<PredicadoObrigatoriedade>(json, HashCanonicalComputer.CanonicalOptions)!;
}
