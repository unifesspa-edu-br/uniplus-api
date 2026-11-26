namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>Configuração EF Core do pedido de concessão de acesso.</summary>
internal sealed class PedidoDeConcessaoConfiguration : IEntityTypeConfiguration<PedidoDeConcessao>
{
    private const int IdentidadeMaxLength = 255;
    private const int SituacaoMaxLength = 40;

    public void Configure(EntityTypeBuilder<PedidoDeConcessao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Pessoal);
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        ConfigurarRestricoes(builder);
        ConfigurarColunas(builder);
        ConfigurarRelacionamentos(builder);
        ConfigurarIndices(builder);

        // Concorrência otimista pela coluna de sistema xmin (ADR-0119).
        builder.Property<uint>("Version").IsRowVersion();
    }

    // O banco confere o que o agregado já garante: nenhuma decisão sem a identidade do beneficiário, o
    // par emissor e conta inteiro, e o encerramento só nos estados terminais. O pedido devolvido, como o cancelado, pode
    // encerrar antes de o beneficiário aceitar.
    private static void ConfigurarRestricoes(EntityTypeBuilder<PedidoDeConcessao> builder) =>
        builder.ToTable("pedidos_de_concessao", tabela =>
        {
            tabela.HasCheckConstraint(
                "ck_pedidos_de_concessao_beneficiario_completo",
                "(beneficiario_emissor IS NULL) = (beneficiario_conta_id IS NULL)");
            tabela.HasCheckConstraint(
                "ck_pedidos_de_concessao_decisao_com_beneficiario",
                $"situacao IN ({SqlDosCodigosDeContrato.Literal(SituacaoDoPedido.AguardandoDocumento)}, {SqlDosCodigosDeContrato.Literal(SituacaoDoPedido.AguardandoBeneficiario)}, {SqlDosCodigosDeContrato.Literal(SituacaoDoPedido.Cancelado)}, {SqlDosCodigosDeContrato.Literal(SituacaoDoPedido.Devolvido)}) OR beneficiario_conta_id IS NOT NULL");
            tabela.HasCheckConstraint(
                "ck_pedidos_de_concessao_encerramento",
                $"(situacao IN ({SqlDosCodigosDeContrato.Literal(SituacaoDoPedido.Aprovado)}, {SqlDosCodigosDeContrato.Literal(SituacaoDoPedido.Recusado)}, {SqlDosCodigosDeContrato.Literal(SituacaoDoPedido.Cancelado)}, {SqlDosCodigosDeContrato.Literal(SituacaoDoPedido.Devolvido)})) = (encerrado_em IS NOT NULL)");
            tabela.HasCheckConstraint(
                "ck_pedidos_de_concessao_concessao_so_aprovado",
                $"concessao_id IS NULL OR situacao = {SqlDosCodigosDeContrato.Literal(SituacaoDoPedido.Aprovado)}");
            tabela.HasCheckConstraint(
                "ck_pedidos_de_concessao_termo_completo",
                "(termo_versao_id IS NULL) = (termo_numero IS NULL) AND (termo_versao_id IS NULL) = (termo_resumo IS NULL)");
        });

    private static void ConfigurarColunas(EntityTypeBuilder<PedidoDeConcessao> builder)
    {
        ConfigurarSolicitante(builder);
        builder.Property(p => p.BeneficiarioEmail).HasMaxLength(EmailDaPessoa.MaxLength).IsRequired();
        ConfigurarBeneficiario(builder);
        ConfigurarDeclaracao(builder);
        ConfigurarAndamento(builder);
        // A unidade é de outro módulo (ADR-0087): sem chave estrangeira entre schemas.
        builder.Property(p => p.UnidadeId);
        ConfigurarTermo(builder);

        // Derivados do estado: não se gravam de novo.
        builder.Ignore(p => p.Escopo);
        builder.Ignore(p => p.AguardaAceiteDoTermo);
    }

    private static void ConfigurarAndamento(EntityTypeBuilder<PedidoDeConcessao> builder)
    {
        builder.OwnsOne(p => p.Andamento, andamento =>
        {
            andamento.ClassificaPorPadrao(ClassificacaoDeProtecao.Pessoal);
            andamento.Property(a => a.Situacao).HasColumnName("situacao").HasMaxLength(SituacaoMaxLength).IsRequired();
            andamento.Property(a => a.ValidaAte).HasColumnName("valida_ate");
            andamento.Property(a => a.EncerradoEm).HasColumnName("encerrado_em");
            andamento.Ignore(a => a.Ativo);
            andamento.Ignore(a => a.EtapaAtual);
            andamento.HasIndex(a => a.Situacao).HasDatabaseName("ix_pedidos_de_concessao_situacao");
        });
        builder.Navigation(p => p.Andamento).IsRequired();
    }

    private static void ConfigurarDeclaracao(EntityTypeBuilder<PedidoDeConcessao> builder)
    {
        builder.OwnsOne(p => p.Declaracao, declaracao =>
        {
            declaracao.ClassificaPorPadrao(ClassificacaoDeProtecao.Pessoal);
            declaracao.Property(d => d.Justificativa).HasColumnName("justificativa").HasMaxLength(DeclaracaoDoPedido.JustificativaMaxLength).IsRequired();
            declaracao.Property(d => d.ValidaAteSugerida).HasColumnName("valida_ate_sugerida");
            declaracao.Property(d => d.AnexaraDocumento).HasColumnName("anexara_documento").IsRequired();
        });
        builder.Navigation(p => p.Declaracao).IsRequired();
    }

    private static void ConfigurarTermo(EntityTypeBuilder<PedidoDeConcessao> builder)
    {
        builder.OwnsOne(p => p.Termo, termo =>
        {
            termo.ClassificaPorPadrao(ClassificacaoDeProtecao.Pessoal);
            termo.Property(t => t.VersaoId).HasColumnName("termo_versao_id");
            termo.Property(t => t.Numero).HasColumnName("termo_numero");
            termo.Property(t => t.Resumo).HasColumnName("termo_resumo").HasMaxLength(ConteudoDoTermo.ResumoLength).IsFixedLength();

            // A versão fixada é imutável e não se apaga: a chave estrangeira impede o pedido de apontar para versão inexistente.
            termo.HasOne<VersaoDoTermo>().WithMany().HasForeignKey(t => t.VersaoId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurarSolicitante(EntityTypeBuilder<PedidoDeConcessao> builder)
    {
        builder.OwnsOne(p => p.Solicitante, solicitante =>
        {
            solicitante.ClassificaPorPadrao(ClassificacaoDeProtecao.Pessoal);
            solicitante.Property(s => s.Emissor).HasColumnName("solicitante_emissor").HasMaxLength(IdentidadeMaxLength).IsRequired();
            solicitante.Property(s => s.ContaId).HasColumnName("solicitante_conta_id").HasMaxLength(IdentidadeMaxLength).IsRequired();
            solicitante.Property(s => s.Nome).HasColumnName("solicitante_nome").HasMaxLength(NomeDeExibicao.MaxLength);
            solicitante.Property(s => s.Email).HasColumnName("solicitante_email").HasMaxLength(EmailDaPessoa.MaxLength);
            solicitante.Ignore(s => s.Identidade);
            solicitante.HasIndex(s => new { s.Emissor, s.ContaId }).HasDatabaseName("ix_pedidos_de_concessao_solicitante");
        });
        builder.Navigation(p => p.Solicitante).IsRequired();
    }

    private static void ConfigurarBeneficiario(EntityTypeBuilder<PedidoDeConcessao> builder)
    {
        builder.OwnsOne(p => p.Beneficiario, beneficiario =>
        {
            beneficiario.ClassificaPorPadrao(ClassificacaoDeProtecao.Pessoal);
            beneficiario.Property(b => b.Emissor).HasColumnName("beneficiario_emissor").HasMaxLength(IdentidadeMaxLength);
            beneficiario.Property(b => b.ContaId).HasColumnName("beneficiario_conta_id").HasMaxLength(IdentidadeMaxLength);
            beneficiario.Property(b => b.Nome).HasColumnName("beneficiario_nome").HasMaxLength(NomeDeExibicao.MaxLength);
            beneficiario.Property(b => b.AceitouEm).HasColumnName("beneficiario_aceitou_em");
            beneficiario.Ignore(b => b.Identidade);
        });
        builder.Navigation(p => p.Beneficiario).IsRequired();
    }

    private static void ConfigurarRelacionamentos(EntityTypeBuilder<PedidoDeConcessao> builder)
    {
        // O perfil é do mesmo módulo. A chave estrangeira impede apagar o perfil pedido.
        builder.HasOne<Perfil>()
            .WithMany()
            .HasForeignKey(p => p.PerfilId)
            .OnDelete(DeleteBehavior.Restrict);

        // O par (perfil, versão) impede o pedido de seguir a versão de outro perfil; o perfil de governança não tem versão.
        builder.HasOne<VersaoDoPerfil>()
            .WithMany()
            .HasForeignKey(p => new { p.PerfilId, p.VersaoId })
            .HasPrincipalKey(v => new { v.PerfilId, v.Id })
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

        // O aceite acompanha toda leitura do pedido: a trilha, o detalhe e a aprovação o conferem.
        builder.HasOne(p => p.AceiteDoTermo)
            .WithOne()
            .HasForeignKey<AceiteDoTermo>(a => a.PedidoId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(p => p.AceiteDoTermo).AutoInclude();

        builder.Navigation(p => p.TrocasDeVersao).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(p => p.TrocasDeVersao)
            .WithOne()
            .HasForeignKey(t => t.PedidoId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigurarIndices(EntityTypeBuilder<PedidoDeConcessao> builder)
    {
        // Um pedido ativo por beneficiário (pelo e-mail), perfil e escopo. A unidade nula (escopo da instituição)
        // conta como igual a outra nula, o que o índice único não faria por padrão.
        builder.HasIndex(p => new { p.BeneficiarioEmail, p.PerfilId, p.UnidadeId })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasFilter(
                "situacao IN ("
                + string.Join(", ", AndamentoDoPedido.SituacoesAtivas.Select(SqlDosCodigosDeContrato.Literal))
                + ")")
            .HasDatabaseName("ux_pedidos_de_concessao_ativo_unico");
    }
}
