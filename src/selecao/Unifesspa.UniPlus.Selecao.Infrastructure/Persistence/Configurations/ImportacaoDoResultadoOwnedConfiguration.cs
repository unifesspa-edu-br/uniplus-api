namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;

internal static class ImportacaoDoResultadoOwnedConfiguration
{
    // A declaração pode ter só relatórios, sem primeira fase, e os relatórios ficam em outra tabela: sem uma coluna que nunca é nula
    // quando a declaração existe, a leitura a trataria como ausente e perderia os relatórios. A coluna guarda sempre verdadeiro.
    private const string MarcaDeDeclaracao = "Declarada";

    internal static void ConfigurarImportacaoDoResultado(this OwnedNavigationBuilder<ProcessoSeletivo, DeclaracaoDaImportacaoDoResultado> importacao)
    {
        ArgumentNullException.ThrowIfNull(importacao);
        importacao.ClassificaPorPadrao(ClassificacaoDeProtecao.Publico);
        importacao.Property<bool>(MarcaDeDeclaracao).HasColumnName("importacao_declarada")
            .HasConversion(new ValueConverter<bool, bool>(static _ => true, static valor => valor))
            .HasComment("Sempre verdadeiro quando o processo declara a importação do resultado; nulo = ainda não declarada.");
        importacao.Property(d => d.PrimeiraFaseDepoisDaImportacaoId).HasColumnName("primeira_fase_depois_importacao_id")
            .HasComment("Identificador da fase do cronograma que é a primeira depois da importação do resultado. Nulo = ainda não declarada.");
        importacao.OwnsMany(d => d.Relatorios, ConfigurarRelatorios);
        importacao.Navigation(d => d.Relatorios).HasField("_relatorios").UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigurarRelatorios(OwnedNavigationBuilder<DeclaracaoDaImportacaoDoResultado, RelatorioDeclarado> relatorio)
    {
        relatorio.ClassificaPorPadrao(ClassificacaoDeProtecao.Publico);
        relatorio.ToTable("relatorios_da_importacao", ConfigurarTabela);
        relatorio.WithOwner().HasForeignKey("ProcessoSeletivoId").HasConstraintName("fk_relatorios_da_importacao_processo_seletivo");
        relatorio.Property<int>("Id").ValueGeneratedOnAdd().HasComment("Identificador técnico da linha, substituída com a declaração.");
        relatorio.HasKey("Id");
        relatorio.Property<Guid>("ProcessoSeletivoId").HasComment("Processo seletivo que declara o relatório da importação.");
        ConfigurarColunas(relatorio);
        relatorio.HasIndex("ProcessoSeletivoId", nameof(RelatorioDeclarado.Posicao)).IsUnique().HasDatabaseName("ux_relatorios_da_importacao_processo_posicao");
        relatorio.HasIndex("ProcessoSeletivoId", nameof(RelatorioDeclarado.LeiauteCodigo)).IsUnique().HasDatabaseName("ux_relatorios_da_importacao_processo_leiaute");
    }

    private static void ConfigurarColunas(OwnedNavigationBuilder<DeclaracaoDaImportacaoDoResultado, RelatorioDeclarado> relatorio)
    {
        relatorio.Property(r => r.LeiauteCodigo).HasColumnName("leiaute_codigo").HasMaxLength(RelatorioDeclarado.LeiauteCodigoComprimentoMaximo).IsRequired()
            .HasComment("Código do leiaute que o relatório segue.");
        relatorio.Property(r => r.LeiauteVersao).HasColumnName("leiaute_versao").HasMaxLength(RelatorioDeclarado.LeiauteVersaoComprimentoMaximo).IsRequired()
            .HasComment("Versão do leiaute que o relatório segue.");
        relatorio.Property(r => r.Posicao).HasColumnName("posicao").HasComment("Posição do relatório na declaração, a partir de 1.");
    }

    private static void ConfigurarTabela(OwnedNavigationTableBuilder<DeclaracaoDaImportacaoDoResultado, RelatorioDeclarado> tabela)
    {
        tabela.HasComment("Relatórios que o processo de resultado importado declara importar, na ordem declarada.");
        tabela.HasCheckConstraint("ck_relatorios_da_importacao_posicao", "posicao >= 1");
    }
}
