namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Enums;

[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class TipoBancaConfiguration : IEntityTypeConfiguration<TipoBanca>
{
    private const int CodigoMaxLength = 60;
    private const int NomeMaxLength = 200;
    private const int FaseTipicaMaxLength = 60;
    private const int DescricaoMaxLength = 300;
    private const int AuditUserMaxLength = 255;

    public void Configure(EntityTypeBuilder<TipoBanca> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("tipos_de_banca", ConfigurarChecks);

        builder.HasKey(b => b.Id);

        // Codigo é o tipo do vocabulário, imutável, gravado pelo código de contrato do enum
        // (a convenção do modelo instala o conversor). O CHECK do vocabulário e o índice
        // único parcial protegem a coluna.
        builder.Property(b => b.Codigo)
            .HasMaxLength(CodigoMaxLength)
            .IsRequired();

        builder.Property(b => b.Nome).HasMaxLength(NomeMaxLength).IsRequired();

        // fase_tipica: rótulo de texto orientativo (NÃO é FK para fases_canonicas).
        builder.Property(b => b.FaseTipica).HasMaxLength(FaseTipicaMaxLength);

        builder.Property(b => b.Descricao).HasMaxLength(DescricaoMaxLength);

        // Auditoria (IAuditableEntity)
        builder.Property(b => b.CreatedBy).HasMaxLength(AuditUserMaxLength);
        builder.Property(b => b.UpdatedBy).HasMaxLength(AuditUserMaxLength);

        // Unicidade do código entre tipos de banca vivos (índice parcial).
        builder.HasIndex(b => b.Codigo)
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName(RestricoesDoBanco.TipoDeBancaCodigoVivo);
    }

    private static void ConfigurarChecks(TableBuilder<TipoBanca> table)
    {
        // Domínio fechado do vocabulário de tipos de banca (defesa em profundidade).
        table.HasCheckConstraint(
            "ck_tipos_de_banca_codigo_canonico",
            $"codigo IN ({TokensSql(CatalogoDe<TipoDeBanca>.Todos())})");
    }

    private static string TokensSql(IReadOnlyList<CodigoDeclarado> codigos) =>
        string.Join(", ", codigos.Select(declarado => $"'{declarado.Codigo}'"));
}
