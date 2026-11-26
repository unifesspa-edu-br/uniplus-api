namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>Configuração EF Core da revisão periódica dos acessos e dos itens dela.</summary>
internal sealed class RevisaoDeAcessosConfiguration : IEntityTypeConfiguration<RevisaoDeAcessos>, IEntityTypeConfiguration<ItemDaRevisao>
{
    private const int IdentidadeMaxLength = 255;
    private const int CodigoMaxLength = 40;

    public void Configure(EntityTypeBuilder<RevisaoDeAcessos> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Pessoal);

        builder.ToTable("revisoes_de_acessos");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.AbertaEm).IsRequired();
        builder.Property(r => r.AbertaPorEmissor).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(r => r.AbertaPorContaId).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(r => r.AbertaPorNome).HasMaxLength(NomeDeExibicao.MaxLength);

        builder.Ignore(r => r.AbertaPor);

        builder.Navigation(r => r.Itens).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(r => r.Itens)
            .WithOne()
            .HasForeignKey(i => i.RevisaoId)
            .OnDelete(DeleteBehavior.Restrict);

        // Concorrência otimista pela coluna de sistema xmin (ADR-0119).
        builder.Property<uint>("Version").IsRowVersion();
    }

    public void Configure(EntityTypeBuilder<ItemDaRevisao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Pessoal);

        // O banco confere o que o agregado já garante: a decisão, com a governança que a tomou, é gravada inteira ou não é.
        builder.ToTable("itens_da_revisao", tabela => tabela.HasCheckConstraint(
            "ck_itens_da_revisao_decisao_completa",
            "(resultado IS NULL AND decidido_como IS NULL AND decidido_por_emissor IS NULL AND decidido_por_conta_id IS NULL AND decidido_em IS NULL)"
            + " OR (resultado IS NOT NULL AND decidido_como IS NOT NULL AND decidido_por_emissor IS NOT NULL AND decidido_por_conta_id IS NOT NULL AND decidido_em IS NOT NULL)"));
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.Property(i => i.DecididoComo).HasMaxLength(CodigoMaxLength);
        builder.Property(i => i.Resultado).HasMaxLength(CodigoMaxLength);
        builder.Property(i => i.DecididoPorEmissor).HasMaxLength(IdentidadeMaxLength);
        builder.Property(i => i.DecididoPorContaId).HasMaxLength(IdentidadeMaxLength);
        builder.Property(i => i.DecididoPorNome).HasMaxLength(NomeDeExibicao.MaxLength);

        builder.Ignore(i => i.Pendente);
        builder.Ignore(i => i.DecididoPor);

        // A concessão é do mesmo módulo. A chave estrangeira mantém o item sempre apontando para uma concessão que existe.
        builder.HasOne<Concessao>()
            .WithMany()
            .HasForeignKey(i => i.ConcessaoId)
            .OnDelete(DeleteBehavior.Restrict);

        // Cada concessão aparece uma vez por revisão.
        builder.HasIndex(i => new { i.RevisaoId, i.ConcessaoId })
            .IsUnique()
            .HasDatabaseName("ux_itens_da_revisao_revisao_concessao");

        // Trilha da concessão e fila de pendentes.
        builder.HasIndex(i => i.ConcessaoId).HasDatabaseName("ix_itens_da_revisao_concessao");

        // Concorrência otimista pela coluna de sistema xmin (ADR-0119): duas decisões do mesmo item não se misturam.
        builder.Property<uint>("Version").IsRowVersion();
    }
}
