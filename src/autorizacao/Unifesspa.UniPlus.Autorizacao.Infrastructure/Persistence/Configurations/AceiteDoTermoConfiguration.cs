namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>Configuração EF Core do aceite do termo de responsabilidade, um por pedido.</summary>
public sealed class AceiteDoTermoConfiguration : IEntityTypeConfiguration<AceiteDoTermo>
{
    private const int IdentidadeMaxLength = 255;

    public void Configure(EntityTypeBuilder<AceiteDoTermo> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Pessoal);

        builder.ToTable("aceites_do_termo");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.Numero).IsRequired();
        builder.Property(a => a.Resumo).HasMaxLength(ConteudoDoTermo.ResumoLength).IsFixedLength().IsRequired();
        builder.Property(a => a.PessoaEmissor).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(a => a.PessoaSub).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(a => a.PessoaNome).HasMaxLength(NomeDeExibicao.MaxLength);
        builder.Property(a => a.AceitoEm).IsRequired();
        builder.Ignore(a => a.Pessoa);

        builder.HasOne<VersaoDoTermo>()
            .WithMany()
            .HasForeignKey(a => a.VersaoDoTermoId)
            .OnDelete(DeleteBehavior.Restrict);

        // Um aceite por pedido: a rede do aceite duplo, além do controle otimista do pedido.
        builder.HasIndex(a => a.PedidoId).IsUnique().HasDatabaseName("ux_aceites_do_termo_pedido");
    }
}
