namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Enums;

internal sealed class TermoConsentimentoConfiguration : IEntityTypeConfiguration<TermoConsentimento>
{
    private const int CodigoDeContratoMaxLength = 30;
    private const int NomeMaxLength = 200;
    private const int TextoMaxLength = 20_000;
    private const int BaseLegalMaxLength = 500;
    private const int RevisadoPorMaxLength = 255;
    private const int AuditUserMaxLength = 255;

    public void Configure(EntityTypeBuilder<TermoConsentimento> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
        builder.ToTable("termos_de_consentimento", ConfigurarChecks);
        builder.HasKey(t => t.Id);
        ConfigurarIdentificacao(builder);
        ConfigurarRascunho(builder);
        ConfigurarRevisao(builder);
        ConfigurarConcorrenciaOtimista(builder);
        ConfigurarAuditoria(builder);
        ConfigurarVersoes(builder);
        ConfigurarIndices(builder);
    }

    private static void ConfigurarChecks(TableBuilder<TermoConsentimento> table)
    {
        table.HasCheckConstraint(
            "ck_termos_de_consentimento_forma_aceite_rascunho",
            $"forma_aceite_rascunho IN ({SqlDosCodigosDeContrato.Lista<FormaAceite>()})");
        table.HasCheckConstraint("ck_termos_de_consentimento_codigo_formato", "codigo ~ '^[A-Z][A-Z0-9_]{1,99}$'");
    }

    private static void ConfigurarIdentificacao(EntityTypeBuilder<TermoConsentimento> builder)
    {
        builder.Property(t => t.Nome).HasMaxLength(NomeMaxLength).IsRequired();
        builder.Property(t => t.Codigo)
            .HasConversion<CodigoTermoConsentimentoValueConverter>()
            .HasMaxLength(CodigoTermoConsentimento.TamanhoMaximo)
            .IsRequired();
    }

    private static void ConfigurarRascunho(EntityTypeBuilder<TermoConsentimento> builder)
    {
        builder.Property(t => t.TextoRascunho).HasMaxLength(TextoMaxLength);
        builder.Property(t => t.BaseLegalRascunho).HasMaxLength(BaseLegalMaxLength);

        builder.Property(t => t.FormaAceiteRascunho)
            .HasMaxLength(CodigoDeContratoMaxLength)
            .IsRequired();
    }

    private static void ConfigurarRevisao(EntityTypeBuilder<TermoConsentimento> builder)
    {
        builder.Property(t => t.Revisado).IsRequired();
        builder.Property(t => t.RevisadoPor).ClassificaComo(ClassificacaoDeProtecao.Pessoal).HasMaxLength(RevisadoPorMaxLength);
        builder.Property(t => t.RevisadoEm);
    }

    // Token de concorrência otimista mapeado para a coluna de sistema `xmin` do
    // Postgres (shadow property `uint` + IsRowVersion — convenção do provider
    // Npgsql, sem coluna/migration própria, mesmo padrão de
    // CalendarioDiasUteisConfiguration): promover uma versão não escreve nenhum
    // campo do próprio rascunho, então sem um write explícito amarrado ao xmin
    // (ver PromoverVersaoTermoConsentimentoCommandHandler) uma promoção concorrente
    // com uma edição de rascunho que reverte a revisão não colidiria — a versão
    // imutável sairia gravada a partir de um rascunho já invalidado.
    private static void ConfigurarConcorrenciaOtimista(EntityTypeBuilder<TermoConsentimento> builder) =>
        builder.Property<uint>("Version").IsRowVersion();

    // Auditoria (IAuditableEntity)
    private static void ConfigurarAuditoria(EntityTypeBuilder<TermoConsentimento> builder)
    {
        builder.Property(t => t.CreatedBy).HasMaxLength(AuditUserMaxLength);
        builder.Property(t => t.UpdatedBy).HasMaxLength(AuditUserMaxLength);
    }

    // Versões promovidas: append-only, o agregado nunca edita/remove uma
    // versão existente, só adiciona. ClientNoAction (não
    // Restrict/Cascade): com a coleção já rastreada, Restrict/NoAction
    // lançam "required relationship severed" ao marcar o TermoConsentimento
    // como Deleted, ANTES de o interceptor convertê-lo em soft-delete;
    // ClientNoAction deixa o interceptor converter em UPDATE e as versões
    // permanecem intactas (mesmo padrão de CalendarioDiasUteis.DiasNaoUteis).
    // O handler de remoção já recusa apagar termo com versão promovida — na
    // prática esta relação nunca chega a ser exercitada por um DELETE físico.
    private static void ConfigurarVersoes(EntityTypeBuilder<TermoConsentimento> builder)
    {
        builder.HasMany(t => t.Versoes)
            .WithOne()
            .HasForeignKey(v => v.TermoConsentimentoId)
            .HasConstraintName("fk_versoes_do_termo_consentimento_termo_de_consentimento")
            .OnDelete(DeleteBehavior.ClientNoAction);
        builder.Navigation(t => t.Versoes)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigurarIndices(EntityTypeBuilder<TermoConsentimento> builder)
    {
        builder.HasIndex(t => t.Codigo)
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName(RestricoesDoBanco.TermoDeConsentimentoCodigoVivo);

        builder.HasIndex(t => t.Nome)
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_termos_de_consentimento_nome");
    }
}
