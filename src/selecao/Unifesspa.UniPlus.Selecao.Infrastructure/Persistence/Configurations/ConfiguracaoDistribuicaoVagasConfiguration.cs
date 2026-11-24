namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

/// <summary>
/// Configuração EF Core de <see cref="ConfiguracaoDistribuicaoVagas"/> (Story
/// #773) — entidade filha do agregado <see cref="ProcessoSeletivo"/>, uma por
/// oferta de curso.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class ConfiguracaoDistribuicaoVagasConfiguration : IEntityTypeConfiguration<ConfiguracaoDistribuicaoVagas>
{

    // Alinhado ao ReferenciaReservaDemograficaConfiguration (Configuracao) —
    // o snapshot-copy (ADR-0061) precisa caber qualquer valor aceito na
    // origem, senão um Censo válido lá estoura em SaveChanges aqui em vez de
    // persistir.
    private const int CensoMaxLength = 20;
    private const int BaseLegalMaxLength = 500;

    public void Configure(EntityTypeBuilder<ConfiguracaoDistribuicaoVagas> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("configuracoes_de_vagas", QuadroDeVagasDaConfiguracao.RestricoesDoQuadro);
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        QuadroDeVagasDaConfiguracao.ConfigurarColunas(builder);
        ConfigurarRegras(builder);
        ConfigurarSnapshots(builder);
        ConfigurarFilhas(builder);

        // UNIQUE parcial: uma configuração de distribuição por oferta de curso dentro do mesmo processo
        // (INV do agregado, ProcessoSeletivo.OfertaCursoDuplicada).
        builder.HasIndex(c => new { c.ProcessoSeletivoId, c.OfertaCursoOrigemId })
            .IsUnique()
            .HasDatabaseName("ux_configuracoes_de_vagas_processo_oferta");
    }

    // A referência de regra (VO do catálogo) embutida por valor: codigo, versao e hash congelados no momento
    // em que o admin configurou. A regra de ajuste é obrigatória na Lei de Cotas e opcional no quadro fixado.
    private static void ConfigurarRegras(EntityTypeBuilder<ConfiguracaoDistribuicaoVagas> builder)
    {
        builder.OwnsOne(c => c.RegraDistribuicao, regra => regra.ConfigurarReferenciaRegra("regra_distribuicao"));
        builder.Navigation(c => c.RegraDistribuicao).IsRequired();

        builder.OwnsOne(c => c.RegraAjuste, regraAjuste => regraAjuste.ConfigurarReferenciaRegra("regra_ajuste"));
        builder.Navigation(c => c.RegraAjuste).IsRequired(false);
    }

    private static void ConfigurarSnapshots(EntityTypeBuilder<ConfiguracaoDistribuicaoVagas> builder)
    {
        ConfigurarReferenciaDemografica(builder);
        ConfigurarGrupoAreaEnem(builder);
        ConfigurarOfertaCurso(builder);
    }

    // Só na Lei 12.711: colunas nulas quando a distribuição é de quadro fixado.
    private static void ConfigurarReferenciaDemografica(EntityTypeBuilder<ConfiguracaoDistribuicaoVagas> builder)
    {
        builder.OwnsOne(c => c.ReferenciaDemografica, demografica =>
        {
            demografica.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
            demografica.Property(d => d.OrigemId).HasColumnName("referencia_demografica_origem_id");
            demografica.Property(d => d.CensoReferencia).HasColumnName("referencia_demografica_censo").HasMaxLength(CensoMaxLength);
            demografica.Property(d => d.PpiPercentual).HasColumnName("referencia_demografica_ppi_percentual").HasPrecision(5, 2);
            demografica.Property(d => d.QuilombolaPercentual).HasColumnName("referencia_demografica_quilombola_percentual").HasPrecision(5, 2);
            demografica.Property(d => d.PcdPercentual).HasColumnName("referencia_demografica_pcd_percentual").HasPrecision(5, 2);
            demografica.Property(d => d.BaseLegal).HasColumnName("referencia_demografica_base_legal").HasMaxLength(BaseLegalMaxLength);
        });
        builder.Navigation(c => c.ReferenciaDemografica).IsRequired(false);
    }

    // Grupo de área do ENEM do curso da oferta, com código e rótulo, congelado por valor (ADR-0061).
    private static void ConfigurarGrupoAreaEnem(EntityTypeBuilder<ConfiguracaoDistribuicaoVagas> builder)
    {
        builder.OwnsOne(c => c.GrupoAreaEnem, grupo =>
        {
            grupo.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
            grupo.Property(g => g.Codigo)
                .HasColumnName("grupo_area_enem_codigo")
                .HasMaxLength(LimitesDoEnvelope.GrupoAreaEnemCodigo)
                .HasComment("Código do grupo de área do ENEM do curso da oferta, sem abreviação e sem acento, copiado do cadastro de cursos na definição da distribuição e na publicação; casa a oferta com a linha de Pesos por Área. Nulo quando o curso não declara grupo.");
            grupo.Property(g => g.Rotulo)
                .HasColumnName("grupo_area_enem_rotulo")
                .HasMaxLength(LimitesDoEnvelope.GrupoAreaEnemRotulo)
                .HasComment("Rótulo do grupo de área do ENEM do curso da oferta, copiado junto do código na definição da distribuição e na publicação. Nulo quando o curso não declara grupo.");
        });
        builder.Navigation(c => c.GrupoAreaEnem).IsRequired(false);
    }

    // O que nomeia a oferta para o candidato, congelado por valor (ADR-0061). Colunas nulas só em
    // distribuição definida antes de a oferta ser nomeada aqui.
    private static void ConfigurarOfertaCurso(EntityTypeBuilder<ConfiguracaoDistribuicaoVagas> builder)
    {
        builder.OwnsOne(c => c.OfertaCurso, oferta =>
        {
            oferta.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
            oferta.Property(o => o.Codigo)
                .HasColumnName("oferta_curso_codigo")
                .HasMaxLength(OfertaCursoSnapshot.CodigoMaxLength)
                .HasComment("Código da oferta de curso, copiado do cadastro na definição da distribuição e na publicação.");
            oferta.Property(o => o.CursoNome)
                .HasColumnName("oferta_curso_nome")
                .HasMaxLength(OfertaCursoSnapshot.CursoNomeMaxLength)
                .HasComment("Nome do curso da oferta, copiado do cadastro na definição da distribuição e na publicação.");
            oferta.Property(o => o.CursoGrau)
                .HasColumnName("oferta_curso_grau")
                .HasMaxLength(OfertaCursoSnapshot.CursoGrauMaxLength)
                .HasComment("Grau do curso da oferta, copiado do cadastro na definição da distribuição e na publicação.");
            oferta.Property(o => o.UnidadeOfertanteNome)
                .HasColumnName("oferta_unidade_ofertante_nome")
                .HasMaxLength(OfertaCursoSnapshot.UnidadeOfertanteNomeMaxLength)
                .HasComment("Nome da unidade ofertante, copiado do cadastro na definição da distribuição e na publicação.");
            oferta.Property(o => o.Turnos)
                .HasColumnName("oferta_turnos")
                .HasComment("Turnos que a oferta ocupa (tokens), copiados do cadastro na definição da distribuição e na publicação.");
        });
        builder.Navigation(c => c.OfertaCurso).IsRequired(false);
    }

    // Coleções filhas: entidades próprias com FK para a raiz (nunca owned types), substituídas por inteiro a
    // cada redefinição. O quadro de vagas é derivado e se materializa junto da declaração (ADR-0115).
    private static void ConfigurarFilhas(EntityTypeBuilder<ConfiguracaoDistribuicaoVagas> builder)
    {
        builder.HasMany(c => c.Modalidades)
            .WithOne()
            .HasForeignKey(m => m.ConfiguracaoDistribuicaoVagasId)
            .HasConstraintName("fk_modalidades_selecionadas_configuracao_de_vagas")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.Modalidades).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(c => c.VagasOfertadas)
            .WithOne()
            .HasForeignKey(v => v.ConfiguracaoDistribuicaoVagasId)
            .HasConstraintName("fk_vagas_ofertadas_configuracao_de_vagas")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.VagasOfertadas).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
