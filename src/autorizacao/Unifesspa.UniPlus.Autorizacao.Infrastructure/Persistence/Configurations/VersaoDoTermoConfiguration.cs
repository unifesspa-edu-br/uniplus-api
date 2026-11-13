namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Configuração EF Core das versões publicadas do termo de responsabilidade. O texto é institucional; só o autor é dado
/// pessoal, gravado para auditoria e nunca exposto nas consultas.
/// </summary>
public sealed class VersaoDoTermoConfiguration : IEntityTypeConfiguration<VersaoDoTermo>
{
    private const int IdentidadeMaxLength = 255;

    public void Configure(EntityTypeBuilder<VersaoDoTermo> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("versoes_do_termo", tabela =>
        {
            tabela.HasCheckConstraint("ck_versoes_do_termo_numero", "numero >= 1");
            tabela.HasCheckConstraint(
                "ck_versoes_do_termo_texto",
                $"char_length(texto) BETWEEN {ConteudoDoTermo.TextoMinLength} AND {ConteudoDoTermo.TextoMaxLength}");
            tabela.HasCheckConstraint("ck_versoes_do_termo_resumo", "resumo ~ '^[0-9a-f]{64}$'");
        });
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();

        builder.Property(v => v.Numero).IsRequired();
        builder.Property(v => v.Texto).HasColumnType("text").IsRequired();
        builder.Property(v => v.Resumo).HasMaxLength(ConteudoDoTermo.ResumoLength).IsFixedLength().IsRequired();
        builder.Property(v => v.Ato).HasMaxLength(ConteudoDoTermo.AtoMaxLength).IsRequired();
        builder.Property(v => v.AutorEmissor).ClassificaComo(ClassificacaoDeProtecao.Pessoal).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(v => v.AutorSub).ClassificaComo(ClassificacaoDeProtecao.Pessoal).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(v => v.AutorNome).ClassificaComo(ClassificacaoDeProtecao.Pessoal).HasMaxLength(NomeDeExibicao.MaxLength);
        builder.Property(v => v.PublicadaEm).IsRequired();

        builder.HasOne<TermoDeResponsabilidade>()
            .WithMany()
            .HasForeignKey(v => v.TermoId)
            .OnDelete(DeleteBehavior.Restrict);

        // O número é sequencial. O resumo não é único: a versão nova pode retomar o texto de uma anterior, e só o texto
        // igual ao da vigente é recusado, pelo agregado.
        builder.HasIndex(v => v.Numero).IsUnique().HasDatabaseName("ux_versoes_do_termo_numero");
        builder.HasIndex(v => v.Resumo).HasDatabaseName("ix_versoes_do_termo_resumo");
    }
}
