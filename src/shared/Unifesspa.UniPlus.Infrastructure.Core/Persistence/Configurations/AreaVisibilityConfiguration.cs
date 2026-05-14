namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence.Configurations;

using System.Text.RegularExpressions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.Entities;

/// <summary>
/// Base templatada das configurações EF Core de entidades área-scoped
/// (ADR-0060). Mapeia, junto da entidade, a sua junction table
/// <c>{prefixo}_areas_de_interesse</c> de <c>AreasDeInteresse</c> com validade
/// temporal: PK composta, FK <c>ON DELETE RESTRICT</c> e índice parcial dos
/// vínculos vigentes.
/// </summary>
/// <remarks>
/// <para>
/// O exclusion constraint GIST que impede janelas sobrepostas NÃO é
/// expressável no fluent API do EF — é emitido via SQL bruto na migration da
/// entidade (ver <see cref="JunctionTableMigrationHelper"/>). Esta classe
/// configura tudo que o modelo EF consegue expressar.
/// </para>
/// <para>
/// Uso canônico — a config concreta herda a base, passa o prefixo singular da
/// tabela ao construtor e invoca <see cref="ConfigureAreaVisibility"/>:
/// </para>
/// <code>
/// public sealed class ModalidadeConfiguration()
///     : AreaVisibilityConfiguration&lt;Modalidade&gt;("modalidade")
/// {
///     public override void Configure(EntityTypeBuilder&lt;Modalidade&gt; builder)
///     {
///         builder.ToTable("modalidades");
///         // ... colunas específicas da Modalidade
///         ConfigureAreaVisibility(builder);
///     }
/// }
/// </code>
/// <para>
/// <c>ApplyConfigurationsFromAssembly</c> aplica a config concreta para os
/// DOIS tipos que ela configura: a entidade área-scoped
/// (<typeparamref name="TParent"/>) e o
/// <see cref="AreaDeInteresseBinding{TParent}"/> da junction.
/// </para>
/// </remarks>
/// <typeparam name="TParent">A entidade área-scoped.</typeparam>
public abstract partial class AreaVisibilityConfiguration<TParent>
    : IEntityTypeConfiguration<TParent>,
      IEntityTypeConfiguration<AreaDeInteresseBinding<TParent>>
    where TParent : EntityBase, IAreaScopedEntity
{
    private const int AdicionadoPorMaxLength = 255;

    // O nome de constraint derivado mais longo é
    // `excl_{prefixo}_areas_de_interesse_overlap` (32 chars de fixo). O limite
    // de identificador do PostgreSQL é 63 chars — truncamento silencioso
    // arriscaria colisão de nomes. 31 mantém todos os identificadores derivados
    // sob o limite com folga (prefixos de catálogo reais têm ~8-21 chars).
    private const int MaxPrefixoLength = 31;

    private readonly string _junctionTable;
    private readonly string _parentForeignKeyColumn;

    /// <param name="prefixoTabelaPai">
    /// Prefixo singular snake_case da tabela da entidade (ex.: <c>modalidade</c>
    /// para a tabela <c>modalidades</c>). Deriva a junction table
    /// <c>{prefixo}_areas_de_interesse</c> e a coluna FK <c>{prefixo}_id</c>.
    /// Deve ser snake_case minúsculo (iniciando por letra) e ter no máximo
    /// <c>31</c> caracteres.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Quando <paramref name="prefixoTabelaPai"/> é vazio, excede 31 caracteres
    /// ou não é snake_case minúsculo — os identificadores SQL derivados (tabela,
    /// coluna FK, constraint GIST, índice) seriam inválidos ou longos demais.
    /// </exception>
    protected AreaVisibilityConfiguration(string prefixoTabelaPai)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefixoTabelaPai);

        if (prefixoTabelaPai.Length > MaxPrefixoLength)
        {
            throw new ArgumentException(
                $"prefixoTabelaPai '{prefixoTabelaPai}' excede {MaxPrefixoLength} caracteres — "
                + "a constraint GIST derivada estouraria o limite de 63 chars de identificador do PostgreSQL.",
                nameof(prefixoTabelaPai));
        }

        if (!PrefixoSnakeCase().IsMatch(prefixoTabelaPai))
        {
            throw new ArgumentException(
                $"prefixoTabelaPai '{prefixoTabelaPai}' deve ser snake_case minúsculo "
                + "(letra minúscula inicial, depois letras minúsculas, dígitos ou underscore).",
                nameof(prefixoTabelaPai));
        }

        _junctionTable = $"{prefixoTabelaPai}_areas_de_interesse";
        _parentForeignKeyColumn = $"{prefixoTabelaPai}_id";
    }

    // snake_case minúsculo, iniciando por letra — garante que tabela, coluna FK,
    // constraint e índice derivados sejam identificadores PostgreSQL válidos.
    [GeneratedRegex("^[a-z][a-z0-9_]*$")]
    private static partial Regex PrefixoSnakeCase();

    /// <summary>Nome da junction table (ex.: <c>modalidade_areas_de_interesse</c>).</summary>
    public string JunctionTable => _junctionTable;

    /// <summary>Nome da coluna FK para a entidade pai (ex.: <c>modalidade_id</c>).</summary>
    public string ParentForeignKeyColumn => _parentForeignKeyColumn;

    /// <summary>
    /// Configuração da entidade área-scoped — implementada pela config concreta,
    /// que deve invocar <see cref="ConfigureAreaVisibility"/>.
    /// </summary>
    public abstract void Configure(EntityTypeBuilder<TParent> builder);

    /// <summary>
    /// Declara, no lado da entidade pai, o relacionamento com a junction table:
    /// FK para <see cref="AreaDeInteresseBinding{TParent}.ParentId"/> com
    /// <c>ON DELETE RESTRICT</c> (ADR-0060). Invocado pela config concreta
    /// dentro do seu <see cref="Configure(EntityTypeBuilder{TParent})"/>.
    /// </summary>
    protected void ConfigureAreaVisibility(EntityTypeBuilder<TParent> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder
            .HasMany<AreaDeInteresseBinding<TParent>>()
            .WithOne()
            .HasForeignKey(binding => binding.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    /// <summary>
    /// Configuração da junction table — aplicada pela base. Mapeada como
    /// implementação explícita de interface porque a config concreta só
    /// sobrescreve <see cref="Configure(EntityTypeBuilder{TParent})"/>.
    /// </summary>
    void IEntityTypeConfiguration<AreaDeInteresseBinding<TParent>>.Configure(
        EntityTypeBuilder<AreaDeInteresseBinding<TParent>> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(_junctionTable);

        builder.HasKey(binding => new { binding.ParentId, binding.AreaCodigo, binding.ValidoDe });

        builder.Property(binding => binding.ParentId).HasColumnName(_parentForeignKeyColumn);
        builder.Property(binding => binding.AreaCodigo).HasColumnName("area_codigo");
        builder.Property(binding => binding.ValidoDe).HasColumnName("valid_from");
        builder.Property(binding => binding.ValidoAte).HasColumnName("valid_to");
        builder.Property(binding => binding.AdicionadoPor)
            .HasColumnName("added_by")
            .HasMaxLength(AdicionadoPorMaxLength)
            .IsRequired();

        // Índice parcial dos vínculos vigentes — hot path da query de
        // visibilidade (ADR-0060). O exclusion constraint GIST de não
        // sobreposição é emitido à parte via JunctionTableMigrationHelper.
        builder
            .HasIndex(binding => new { binding.ParentId, binding.AreaCodigo })
            .HasFilter("valid_to IS NULL")
            .HasDatabaseName($"ix_{_junctionTable}_vigentes");
    }
}
