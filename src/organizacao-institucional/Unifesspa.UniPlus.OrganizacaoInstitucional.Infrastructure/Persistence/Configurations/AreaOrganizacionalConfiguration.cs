namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class AreaOrganizacionalConfiguration : IEntityTypeConfiguration<AreaOrganizacional>
{
    public void Configure(EntityTypeBuilder<AreaOrganizacional> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("areas_organizacionais");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Codigo)
            .HasConversion(new AreaCodigoValueConverter())
            .HasMaxLength(32)
            .IsRequired();
        builder.HasIndex(a => a.Codigo)
            .IsUnique()
            .HasDatabaseName("ix_areas_organizacionais_codigo");

        builder.Property(a => a.Nome).HasMaxLength(120).IsRequired();
        builder.Property(a => a.Tipo).HasConversion<int>().IsRequired();
        builder.Property(a => a.Descricao).HasMaxLength(500).IsRequired();
        builder.Property(a => a.AdrReferenceCode).HasMaxLength(200).IsRequired();

        // Audit (IAuditableEntity) — preenchido pelo AuditableInterceptor a partir
        // do IUserContext. EntityBase.CreatedAt/UpdatedAt mantidos pela EntityBase.
        builder.Property(a => a.CreatedBy).HasMaxLength(255);
        builder.Property(a => a.UpdatedBy).HasMaxLength(255);

        // Soft delete query filter (ADR — pattern Uni+). Reader e listagens
        // públicas operam apenas sobre não-deletadas; auditoria histórica
        // bypassa o filter via IgnoreQueryFilters.
        builder.HasQueryFilter(a => !a.IsDeleted);
    }
}
