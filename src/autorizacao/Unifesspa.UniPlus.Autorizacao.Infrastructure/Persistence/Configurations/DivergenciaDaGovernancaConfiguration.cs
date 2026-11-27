namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>Configuração EF Core das divergências dos perfis de governança, registro só acrescentado.</summary>
public sealed class DivergenciaDaGovernancaConfiguration : IEntityTypeConfiguration<DivergenciaDaGovernanca>
{
    private const int CodigoMaxLength = 120;
    private const int TipoMaxLength = 40;
    private const int ClassificacaoMaxLength = 20;

    public void Configure(EntityTypeBuilder<DivergenciaDaGovernanca> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("divergencias_da_governanca", ConfigurarRestricoes);
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        ConfigurarColunas(builder);
        ConfigurarMudancaDaClassificacao(builder);
        ConfigurarMudancaDoEscopo(builder);
        ConfigurarRelacionamentosEIndices(builder);
    }

    private static void ConfigurarColunas(EntityTypeBuilder<DivergenciaDaGovernanca> builder)
    {
        builder.Property(d => d.Codigo).HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(d => d.Tipo).HasMaxLength(TipoMaxLength).IsRequired();
        builder.Property(d => d.Instante).IsRequired();
        builder.Property(d => d.OrdemNoHistorico).IsRequired();
        builder.Ignore(d => d.OrdinalLocal);
    }

    private static void ConfigurarRelacionamentosEIndices(EntityTypeBuilder<DivergenciaDaGovernanca> builder)
    {
        builder.HasOne<ImplantacaoDasPermissoes>().WithMany().HasForeignKey(d => d.ImplantacaoId)
            .HasConstraintName("fk_divergencias_da_governanca_implantacao").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Perfil>().WithMany().HasForeignKey(d => d.PerfilId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => d.OrdemNoHistorico).IsUnique().HasDatabaseName("ux_divergencias_da_governanca_ordem");
    }

    private static void ConfigurarMudancaDaClassificacao(EntityTypeBuilder<DivergenciaDaGovernanca> builder) =>
        builder.OwnsOne(d => d.MudancaDaClassificacao, mudanca =>
        {
            mudanca.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
            mudanca.Property(m => m.Anterior).HasColumnName("classificacao_anterior").HasMaxLength(ClassificacaoMaxLength)
                .HasComment("Classificação de proteção da permissão antes da troca de catálogo; nula fora da divergência de classificação.");
            mudanca.Property(m => m.Nova).HasColumnName("classificacao_nova").HasMaxLength(ClassificacaoMaxLength)
                .HasComment("Classificação de proteção da permissão depois da troca de catálogo; nula fora da divergência de classificação.");
        });

    private static void ConfigurarMudancaDoEscopo(EntityTypeBuilder<DivergenciaDaGovernanca> builder) =>
        builder.OwnsOne(d => d.MudancaDoEscopo, mudanca =>
        {
            mudanca.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
            mudanca.Property(m => m.DeUnidadeAnterior).HasColumnName("de_unidade_anterior")
                .HasComment("Indica se a permissão valia por unidade antes da troca de catálogo; nula fora da divergência de escopo.");
            mudanca.Property(m => m.DeUnidadeNovo).HasColumnName("de_unidade_novo")
                .HasComment("Indica se a permissão vale por unidade depois da troca de catálogo; nula fora da divergência de escopo.");
        });

    // A composição não guarda valores; a classificação guarda só o par de classificações e o escopo só o par de escopos,
    // sempre diferentes entre si.
    private static void ConfigurarRestricoes(TableBuilder<DivergenciaDaGovernanca> tabela)
    {
        tabela.HasCheckConstraint(
            "ck_divergencias_da_governanca_tipo",
            $"tipo IN ({SqlDosCodigosDeContrato.Lista<TipoDeDivergencia>()})");
        tabela.HasCheckConstraint("ck_divergencias_da_governanca_valores", SqlDosValoresDaDivergencia);
    }

    private static string SqlDosValoresDaDivergencia =>
        $"(tipo IN ({SqlDosCodigosDeContrato.Literal(TipoDeDivergencia.ComposicaoFaltante)}, {SqlDosCodigosDeContrato.Literal(TipoDeDivergencia.ComposicaoExcedente)}) "
        + "AND classificacao_anterior IS NULL AND classificacao_nova IS NULL AND de_unidade_anterior IS NULL AND de_unidade_novo IS NULL) "
        + $"OR (tipo = {SqlDosCodigosDeContrato.Literal(TipoDeDivergencia.Classificacao)} "
        + "AND classificacao_anterior IS NOT NULL AND classificacao_nova IS NOT NULL AND classificacao_anterior <> classificacao_nova "
        + "AND de_unidade_anterior IS NULL AND de_unidade_novo IS NULL) "
        + $"OR (tipo = {SqlDosCodigosDeContrato.Literal(TipoDeDivergencia.Escopo)} "
        + "AND de_unidade_anterior IS NOT NULL AND de_unidade_novo IS NOT NULL AND de_unidade_anterior <> de_unidade_novo "
        + "AND classificacao_anterior IS NULL AND classificacao_nova IS NULL)";
}
