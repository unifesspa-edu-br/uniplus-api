namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>Configuração EF Core das decisões do pedido, registro só acrescentado.</summary>
internal sealed class DecisaoDoPedidoConfiguration : IEntityTypeConfiguration<DecisaoDoPedido>
{
    private const int IdentidadeMaxLength = 255;
    private const int CodigoMaxLength = 40;
    private const int SensibilidadeMaxLength = 20;

    public void Configure(EntityTypeBuilder<DecisaoDoPedido> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Pessoal);

        builder.ToTable("decisoes_do_pedido", tabela => tabela.HasCheckConstraint(
            "ck_decisoes_do_pedido_motivo_na_recusa",
            $"resultado <> {SqlDosCodigosDeContrato.Literal(ResultadoDaDecisao.Recusado)} OR motivo IS NOT NULL"));
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.Etapa).HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(d => d.Resultado).HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(d => d.DecisorEmissor).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(d => d.DecisorContaId).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(d => d.DecisorNome).HasMaxLength(NomeDeExibicao.MaxLength);
        builder.Property(d => d.Motivo).HasMaxLength(MotivoInformado.MaximoDeCaracteres);
        builder.Property(d => d.Instante).IsRequired();
        builder.Property(d => d.OrdemDeRegistro).UseIdentityAlwaysColumn().ValueGeneratedOnAdd();
        builder.Property(d => d.PermissoesAvaliadas).IsRequired();
        builder.Property(d => d.SensibilidadeAvaliada).HasMaxLength(SensibilidadeMaxLength).IsRequired();
        builder.Property(d => d.Dispensa).HasMaxLength(CodigoMaxLength);

        builder.Ignore(d => d.Decisor);

        builder.HasIndex(d => d.PedidoId).HasDatabaseName("ix_decisoes_do_pedido_pedido");
        builder.HasIndex(d => d.OrdemDeRegistro).IsUnique().HasDatabaseName("ux_decisoes_do_pedido_ordem_de_registro");
    }
}
