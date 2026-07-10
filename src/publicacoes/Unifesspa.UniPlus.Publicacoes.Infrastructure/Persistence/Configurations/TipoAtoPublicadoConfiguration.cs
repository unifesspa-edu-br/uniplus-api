namespace Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Publicacoes.Domain.Entities;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class TipoAtoPublicadoConfiguration : IEntityTypeConfiguration<TipoAtoPublicado>
{
    private const int CodigoMaxLength = 60;
    private const int NomeMaxLength = 200;
    private const int BaseLegalMaxLength = 500;

    public void Configure(EntityTypeBuilder<TipoAtoPublicado> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            "tipo_ato_publicado",
            t =>
            {
                // Fim da vigência é exclusivo: uma janela cujo fim iguala o início
                // não contém dia algum, e o daterange correspondente seria vazio —
                // vazio não intercepta nada, e escaparia da exclusion constraint.
                t.HasCheckConstraint(
                    "ck_tipo_ato_publicado_vigencia",
                    "vigencia_fim IS NULL OR vigencia_fim > vigencia_inicio");

                // Defesa em profundidade do formato do código contra inserts crus
                // (o guard de domínio já recusa). Caixa alta e underscore interno.
                t.HasCheckConstraint(
                    "ck_tipo_ato_publicado_codigo_formato",
                    "codigo ~ '^[A-Z]+(_[A-Z]+)*$'");
            });

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Codigo).HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(t => t.Nome).HasMaxLength(NomeMaxLength).IsRequired();

        builder.Property(t => t.CongelaConfiguracao).IsRequired();
        builder.Property(t => t.UnicoPorObjeto).IsRequired();
        builder.Property(t => t.EfeitoIrreversivel).IsRequired();

        builder.Property(t => t.VigenciaInicio).IsRequired();
        builder.Property(t => t.VigenciaFim);

        builder.Property(t => t.BaseLegal).HasMaxLength(BaseLegalMaxLength);

        // Auditoria (IAuditableEntity)
        builder.Property(t => t.CreatedBy).HasMaxLength(255);
        builder.Property(t => t.UpdatedBy).HasMaxLength(255);

        // Lookup por código entre versões vivas. Não é único: o mesmo código tem
        // uma versão por janela de vigência. A unicidade que importa — nenhuma
        // sobreposição entre janelas vivas do mesmo código — é da exclusion
        // constraint GIST criada na migration, e o índice GIST dela não serve a
        // este acesso por igualdade.
        builder.HasIndex(t => t.Codigo)
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_tipo_ato_publicado_codigo_vivo");
    }
}
