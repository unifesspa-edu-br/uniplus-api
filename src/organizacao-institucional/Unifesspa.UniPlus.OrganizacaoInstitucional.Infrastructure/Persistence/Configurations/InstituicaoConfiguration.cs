namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class InstituicaoConfiguration : IEntityTypeConfiguration<Instituicao>
{
    public void Configure(EntityTypeBuilder<Instituicao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // CHECK fixa a sentinela como verdadeiramente constante no banco: sem ela,
        // qualquer caminho de escrita (SQL de carga, correção manual) poderia gravar
        // registro_vivo_sentinela = false e burlar o índice único parcial — duas
        // Instituições vivas (uma true, uma false) passariam. Com o CHECK, a coluna
        // só admite true, então o índice único parcial garante de fato o singleton.
        builder.ToTable("instituicao", t =>
            t.HasCheckConstraint("ck_instituicao_singleton_sentinela", "registro_vivo_sentinela = true"));
        builder.HasKey(i => i.Id);

        builder.Property(i => i.CodigoEmec).HasMaxLength(20).IsRequired();
        builder.Property(i => i.Nome).HasMaxLength(250).IsRequired();
        builder.Property(i => i.Sigla).HasMaxLength(50).IsRequired();
        builder.Property(i => i.OrganizacaoAcademica).HasMaxLength(100).IsRequired();
        builder.Property(i => i.CategoriaAdministrativa).HasMaxLength(100).IsRequired();
        builder.Property(i => i.Cnpj).HasMaxLength(100);
        builder.Property(i => i.Mantenedora).HasMaxLength(250);
        builder.Property(i => i.CodigoMantenedoraEmec).HasMaxLength(20);
        builder.Property(i => i.Situacao).HasMaxLength(100);
        builder.Property(i => i.AtoCredenciamento).HasMaxLength(500);
        builder.Property(i => i.AtoRecredenciamento).HasMaxLength(500);
        builder.Property(i => i.ConceitoInstitucional).HasMaxLength(100);
        builder.Property(i => i.Igc).HasMaxLength(100);
        builder.Property(i => i.Website).HasMaxLength(255);
        builder.Property(i => i.EnderecoSede).HasMaxLength(500);

        // Referência de cidade do Geo (ADR-0090): código + display cache, opcional
        // (all-or-nothing), sem FK cross-banco para uniplus_geo.
        builder.Property(i => i.CidadeCodigoIbge)
            .HasMaxLength(ReferenciaCidadeGeo.CodigoIbgeLength)
            .IsFixedLength();
        builder.Property(i => i.CidadeNome).HasMaxLength(ReferenciaCidadeGeo.NomeMaxLength);
        builder.Property(i => i.CidadeUf)
            .HasMaxLength(ReferenciaCidadeGeo.UfLength)
            .IsFixedLength();
        builder.Property(i => i.CidadeOrigem).HasMaxLength(ReferenciaCidadeGeo.OrigemMaxLength);
        builder.Property(i => i.CidadeDisplayAtualizadoEm);

        // Auditoria (IAuditableEntity)
        builder.Property(i => i.CreatedBy).HasMaxLength(255);
        builder.Property(i => i.UpdatedBy).HasMaxLength(255);

        // Vínculo com a Unidade raiz (reitoria): FK intra-banco (ADR-0054).
        // Restrict no banco — a remoção lógica da Unidade é barrada pelo handler
        // (UnidadeErrorCodes.RemocaoBloqueadaPorInstituicao); o RESTRICT cobre o
        // DELETE físico residual.
        builder.HasOne<Unidade>()
            .WithMany()
            .HasForeignKey(i => i.UnidadeRaizId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.UnidadeRaizId)
            .HasDatabaseName("ix_instituicao_unidade_raiz_id");

        // Salvaguarda física do singleton: a sentinela é uma propriedade real
        // sempre true (a entidade a inicializa), então o EF grava true de forma
        // confiável — diferente de uma shadow bool, que o EF gravaria como o
        // default CLR (false) e violaria o CHECK. HasDefaultValue cobre inserts
        // crus que omitam a coluna.
        builder.Property(i => i.RegistroVivoSentinela)
            .HasColumnName("registro_vivo_sentinela")
            .HasDefaultValue(true);

        builder.HasIndex(i => i.RegistroVivoSentinela)
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_instituicao_singleton_vivo");

        // Índice de relatório/filtro pela cidade da sede (ADR-0090).
        builder.HasIndex(i => i.CidadeCodigoIbge)
            .HasDatabaseName("ix_instituicao_cidade_codigo_ibge");
    }
}
