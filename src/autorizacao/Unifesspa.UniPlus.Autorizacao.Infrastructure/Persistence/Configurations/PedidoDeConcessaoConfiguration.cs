namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>Configuração EF Core do pedido de concessão de acesso.</summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class PedidoDeConcessaoConfiguration : IEntityTypeConfiguration<PedidoDeConcessao>
{
    private const int IdentidadeMaxLength = 255;
    private const int SituacaoMaxLength = 40;

    private static readonly ValueConverter<SituacaoDoPedido, string> SituacaoConverter = new(
        situacao => situacao.ToCodigo(),
        codigo => SituacaoDoPedidoCodigo.FromCodigo(codigo));

    public void Configure(EntityTypeBuilder<PedidoDeConcessao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // O banco confere o que o agregado já garante: nenhuma decisão sem a identidade do beneficiário, o
        // par emissor e sub inteiro, e o encerramento só nos estados terminais.
        builder.ToTable("pedidos_de_concessao", tabela =>
        {
            tabela.HasCheckConstraint(
                "ck_pedidos_beneficiario_completo",
                "(beneficiario_emissor IS NULL) = (beneficiario_sub IS NULL)");
            tabela.HasCheckConstraint(
                "ck_pedidos_decisao_com_beneficiario",
                "situacao IN ('aguardando_documento', 'aguardando_beneficiario', 'cancelado') OR beneficiario_sub IS NOT NULL");
            tabela.HasCheckConstraint(
                "ck_pedidos_encerramento",
                "(situacao IN ('aprovado', 'recusado', 'cancelado')) = (encerrado_em IS NOT NULL)");
            tabela.HasCheckConstraint(
                "ck_pedidos_concessao_so_aprovado",
                "concessao_id IS NULL OR situacao = 'aprovado'");
        });
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.SolicitanteEmissor).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(p => p.SolicitanteSub).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(p => p.SolicitanteNome).HasMaxLength(NomeDeExibicao.MaxLength);
        builder.Property(p => p.BeneficiarioEmail).HasMaxLength(PedidoDeConcessao.EmailMaxLength).IsRequired();
        builder.Property(p => p.BeneficiarioEmissor).HasMaxLength(IdentidadeMaxLength);
        builder.Property(p => p.BeneficiarioSub).HasMaxLength(IdentidadeMaxLength);
        builder.Property(p => p.BeneficiarioNome).HasMaxLength(NomeDeExibicao.MaxLength);
        builder.Property(p => p.Justificativa).HasMaxLength(PedidoDeConcessao.JustificativaMaxLength).IsRequired();
        builder.Property(p => p.Situacao).HasConversion(SituacaoConverter).HasMaxLength(SituacaoMaxLength).IsRequired();

        // A unidade é de outro módulo (ADR-0087): sem chave estrangeira entre schemas.
        builder.Property(p => p.UnidadeId);

        // Derivados do estado: não se gravam de novo.
        builder.Ignore(p => p.Solicitante);
        builder.Ignore(p => p.Beneficiario);
        builder.Ignore(p => p.Escopo);
        builder.Ignore(p => p.Ativo);
        builder.Ignore(p => p.EtapaAtual);
        builder.Ignore(p => p.TemAnexoVerificado);

        // O perfil é do mesmo módulo. A chave estrangeira impede apagar o perfil pedido.
        builder.HasOne<Perfil>()
            .WithMany()
            .HasForeignKey(p => p.PerfilId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(p => p.Decisoes).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(p => p.Decisoes)
            .WithOne()
            .HasForeignKey(d => d.PedidoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(p => p.Anexos).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(p => p.Anexos)
            .WithOne()
            .HasForeignKey(a => a.PedidoId)
            .OnDelete(DeleteBehavior.Restrict);

        // Um pedido ativo por beneficiário (pelo e-mail), perfil e escopo. A unidade nula (escopo da instituição)
        // conta como igual a outra nula, o que o índice único não faria por padrão.
        builder.HasIndex(p => new { p.BeneficiarioEmail, p.PerfilId, p.UnidadeId })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasFilter(
                "situacao IN ("
                + string.Join(", ", PedidoDeConcessao.SituacoesAtivas.Select(s => $"'{s.ToCodigo()}'"))
                + ")")
            .HasDatabaseName("ux_pedidos_ativo_unico");

        // As filas dos aprovadores e a lista de quem pediu.
        builder.HasIndex(p => p.Situacao).HasDatabaseName("ix_pedidos_situacao");
        builder.HasIndex(p => new { p.SolicitanteEmissor, p.SolicitanteSub }).HasDatabaseName("ix_pedidos_solicitante");

        // Concorrência otimista pela coluna de sistema xmin (ADR-0119).
        builder.Property<uint>("Version").IsRowVersion();
    }
}
