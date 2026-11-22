namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Configuração EF Core da tabela append-only <c>versoes_do_termo_consentimento</c>
/// (termos imunes após a publicação, UNI-REQ-0086). Sem soft-delete, sem audit fields, sem updates —
/// qualquer mutação fora de <c>INSERT</c> é incidente operacional.
/// </summary>
internal sealed class TermoConsentimentoVersaoConfiguration : IEntityTypeConfiguration<TermoConsentimentoVersao>
{
    private const int CodigoDeContratoMaxLength = 30;
    private const int HashLength = 64;
    private const int PromovidaPorMaxLength = 255;

    public void Configure(EntityTypeBuilder<TermoConsentimentoVersao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
        builder.ToTable("versoes_do_termo_consentimento", ConfigurarChecks);
        builder.HasKey(v => v.Id);
        ConfigurarConteudo(builder);
        ConfigurarFormaDeAceiteEIntegridade(builder);
        ConfigurarPromocao(builder);
        ConfigurarIndices(builder);
    }

    private static void ConfigurarChecks(TableBuilder<TermoConsentimentoVersao> table)
    {
        table.HasCheckConstraint(
            "ck_versoes_do_termo_consentimento_forma_aceite",
            $"forma_aceite IN ({SqlDosCodigosDeContrato.Lista<FormaAceite>()})");
        table.HasCheckConstraint("ck_versoes_do_termo_consentimento_numero", "numero >= 1");
    }

    private static void ConfigurarConteudo(EntityTypeBuilder<TermoConsentimentoVersao> builder)
    {
        builder.Property(v => v.TermoConsentimentoId).IsRequired();
        builder.Property(v => v.Numero).IsRequired();
        builder.Property(v => v.Texto).IsRequired();
        builder.Property(v => v.BaseLegal).IsRequired();
    }

    private static void ConfigurarFormaDeAceiteEIntegridade(EntityTypeBuilder<TermoConsentimentoVersao> builder)
    {
        builder.Property(v => v.FormaAceite)
            .HasMaxLength(CodigoDeContratoMaxLength)
            .IsRequired();

        builder.Property(v => v.Hash)
            .HasMaxLength(HashLength)
            .IsFixedLength()
            .IsRequired();
    }

    private static void ConfigurarPromocao(EntityTypeBuilder<TermoConsentimentoVersao> builder)
    {
        builder.Property(v => v.PromovidaEm).IsRequired();

        builder.Property(v => v.PromovidaPor).ClassificaComo(ClassificacaoDeProtecao.Pessoal)
            .HasMaxLength(PromovidaPorMaxLength)
            .IsRequired();
    }

    private static void ConfigurarIndices(EntityTypeBuilder<TermoConsentimentoVersao> builder)
    {
        builder.HasIndex(v => new { v.TermoConsentimentoId, v.Numero })
            .IsUnique()
            .HasDatabaseName(RestricoesDoBanco.VersaoDoTermoNumeroUnico);

        builder.HasIndex(v => new { v.TermoConsentimentoId, v.PromovidaEm })
            .HasDatabaseName("ix_versoes_do_termo_consentimento_termo_promovida_em");
    }
}
