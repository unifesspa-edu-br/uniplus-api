namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

public sealed class EtapaProcessoConfiguration : IEntityTypeConfiguration<EtapaProcesso>
{
    public void Configure(EntityTypeBuilder<EtapaProcesso> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("etapas_processo", t =>
        {
            t.HasComment("Etapas de avaliação e suas definições em cada processo seletivo.");
            // A coerência entre notas e passo é conferida pelo domínio.
            t.HasCheckConstraint("ck_etapas_processo_peso_positivo", "peso IS NULL OR peso > 0");
            t.HasCheckConstraint("ck_etapas_processo_nota_minima_nao_negativa", "nota_minima IS NULL OR nota_minima >= 0");
            t.HasCheckConstraint("ck_etapas_processo_periodo_em_ordem", "inicio IS NULL OR fim IS NULL OR inicio <= fim");
            t.HasCheckConstraint("ck_etapas_processo_nota_maxima_positiva", "nota_maxima IS NULL OR nota_maxima > 0");
            t.HasCheckConstraint("ck_etapas_processo_passo_positivo", "passo_do_lancamento IS NULL OR passo_do_lancamento > 0");
            t.HasCheckConstraint("ck_etapas_processo_forma_de_avaliacao_valida", "forma_de_avaliacao IS NULL OR forma_de_avaliacao IN (1, 2)");
        });
        builder.HasKey(e => e.Id);
        // Chave Guid v7 do domínio (EntityBase) — ValueGeneratedNever para o EF
        // tratar a chave como fornecida pela aplicação (evita UPDATE de filho novo
        // ao reconfigurar o agregado tracked). Convenção do repo.
        builder.Property(e => e.Id).ValueGeneratedNever().HasComment("Identificador da etapa.");
        builder.Property(e => e.Chave)
            .HasConversion(chave => chave.Valor, valor => ChaveLocal.Criar(valor).Value)
            .HasMaxLength(ChaveLocal.ComprimentoMaximo).IsRequired()
            .HasComment("Chave local estável da etapa no processo seletivo.");
        builder.HasIndex(e => new { e.ProcessoSeletivoId, e.Chave })
            .IsUnique().HasDatabaseName("ux_etapas_processo_processo_chave");
        builder.Property(e => e.ProcessoSeletivoId).HasComment("Processo seletivo ao qual a etapa pertence.");
        builder.Property(e => e.Ordem).IsRequired().HasComment("Posição da etapa no processo, a partir de um.");

        builder.Property(e => e.Nome).HasMaxLength(EtapaProcesso.NomeMaxLength).IsRequired().HasComment("Nome da etapa no processo seletivo.");
        builder.Property(e => e.Carater).HasConversion<int>().IsRequired()
            .HasComment($"Finalidade da etapa na avaliação. Códigos: {CodigosNumericos<CaraterEtapa>()}.");

        builder.Ignore(e => e.TipoEtapaOrigemId);
        // Sem produção em nenhum ambiente: o vínculo nasce obrigatório, sem coluna nullable
        // transitória (issue #1071).
        builder.OwnsOne(e => e.TipoEtapa, tipo =>
        {
            tipo.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
            tipo.Property(x => x.OrigemId)
                .HasColumnName("tipo_etapa_origem_id")
                .IsRequired()
                .HasComment("Identificador do tipo de etapa de origem, copiado na definição.");
            tipo.Property(x => x.Codigo).HasComment("Código do tipo de etapa copiado na definição.").HasColumnName("tipo_etapa_codigo").HasMaxLength(64).IsRequired();
            tipo.Property(x => x.Nome).HasComment("Nome do tipo de etapa copiado na definição.").HasColumnName("tipo_etapa_nome").HasMaxLength(200).IsRequired();
            tipo.Property(x => x.AdmitePontuacao).HasComment("Indica se o tipo de etapa admite pontuação, conforme a definição copiada.").HasColumnName("tipo_etapa_admite_pontuacao").IsRequired();
            tipo.Property(x => x.AdmiteEliminacao).HasComment("Indica se o tipo de etapa admite eliminação, conforme a definição copiada.").HasColumnName("tipo_etapa_admite_eliminacao").IsRequired();
            tipo.Property(x => x.NotaDeOrigemNoEnem)
                .HasColumnName("tipo_etapa_nota_de_origem_no_enem")
                .IsRequired()
                .HasComment("Indica se a nota da etapa vem do Enem, conforme a definição do tipo.");
            tipo.Property(x => x.ExigeCorrecaoIndependente)
                .HasColumnName("tipo_etapa_exige_correcao_independente")
                .IsRequired()
                .HasComment("Indica se a etapa exige correções independentes por mais de um corretor, conforme a definição do tipo.");
        });
        builder.Navigation(e => e.TipoEtapa).IsRequired();

        // O vínculo com a fase é o código, e só ele: é o que o cliente declara, o que o
        // envelope congela e o que a raiz resolve contra o cronograma quando precisa da fase.
        // Sem FK — a fase é substituída por inteiro a cada gravação do cronograma, e uma FK
        // restritiva recusaria a substituição.
        builder.Property(e => e.FaseCodigo)
            .HasColumnName("fase_codigo")
            .HasMaxLength(60)
            .HasComment("Código da fase do cronograma em que a etapa acontece.");

        // Produtos da etapa em cascata: a configuração em rascunho é substituída por
        // inteiro, e nada fora do agregado referencia o produto por FK.
        builder.HasMany(e => e.Produtos)
            .WithOne()
            .HasForeignKey(p => p.EtapaProcessoId)
            .HasConstraintName("fk_publicacoes_previstas_da_etapa_etapa")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Metadata.FindNavigation(nameof(EtapaProcesso.Produtos))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(e => e.Bancas).WithOne()
            .HasForeignKey(b => b.EtapaProcessoId).OnDelete(DeleteBehavior.Cascade);
        builder.Metadata.FindNavigation(nameof(EtapaProcesso.Bancas))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(e => e.Recursos).WithOne()
            .HasForeignKey(r => r.EtapaProcessoId).HasConstraintName("fk_recursos_configurados_da_etapa_etapa").OnDelete(DeleteBehavior.Cascade);
        builder.Metadata.FindNavigation(nameof(EtapaProcesso.Recursos))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(e => e.ItensDeAvaliacao).WithOne()
            .HasForeignKey(i => i.EtapaProcessoId).HasConstraintName("fk_itens_de_avaliacao_da_etapa_etapa_processo").OnDelete(DeleteBehavior.Cascade);
        builder.Metadata.FindNavigation(nameof(EtapaProcesso.ItensDeAvaliacao))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // A janela própria da etapa: ausente quando ela herda a da fase.
        builder.Property(e => e.Inicio).HasColumnName("inicio").HasComment("Início do período próprio da etapa; ausente quando usa o período da fase.");
        builder.Property(e => e.Fim).HasColumnName("fim").HasComment("Fim do período próprio da etapa; ausente quando usa o período da fase.");
        builder.Property(e => e.EmiteParecerIndividual)
            .HasColumnName("emite_parecer_individual").HasDefaultValue(false).HasComment("Indica se a etapa emite parecer individual para cada candidato.");
        builder.MapearTiposDeDecisao();

        builder.Property(e => e.Peso).HasPrecision(18, 4).HasComment("Peso da etapa no cálculo da nota final, quando aplicável.");
        builder.Property(e => e.NotaMinima).HasPrecision(18, 4).HasComment("Nota mínima exigida na etapa, quando definida.");
        builder.Property(e => e.NotaMaxima).HasPrecision(18, 4)
            .HasComment("Nota máxima da escala da etapa com nota; ausente na etapa sem nota e na do ENEM.");
        builder.Property(e => e.PassoDoLancamento).HasPrecision(18, 4)
            .HasComment("De quanto em quanto a nota pode ser lançada; vale só para a nota digitada.");

        builder.Property(e => e.FormaDeAvaliacao)
            .HasColumnName("forma_de_avaliacao")
            .HasConversion<int>()
            .HasComment($"Como a nota da etapa é obtida; ausente quando não definida. Códigos: {CodigosNumericos<FormaDeAvaliacao>()}.");

        // Só a forma humana tem regra de consolidação, mas a coerência é regra de gravação do
        // domínio, não de armazenamento: a coluna não a repete.
        builder.OwnsOne(e => e.RegraConsolidacao, regra =>
        {
            regra.ConfigurarReferenciaRegra("apuracao_avaliacao");
            regra.Property(r => r.Codigo).HasComment("Código da regra de consolidação das notas, quando definida.");
            regra.Property(r => r.Versao).HasComment("Versão da regra de consolidação das notas, quando definida.");
            regra.Property(r => r.Hash).HasComment("Resumo que identifica o conteúdo da regra de consolidação, quando definida.");
        });
        builder.Navigation(e => e.RegraConsolidacao).IsRequired(false);

        builder.OwnsMany(e => e.EscalaDeAtendimento, nivel =>
        {
            nivel.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
            nivel.ToTable("niveis_de_atendimento_da_etapa", tabela =>
            {
                tabela.HasComment("Níveis de atendimento usados na avaliação dos itens da etapa.");
                tabela.HasCheckConstraint("ck_niveis_atendimento_fracao_entre_0_e_1", "fracao BETWEEN 0 AND 1");
                tabela.HasCheckConstraint("ck_niveis_atendimento_rotulo_preenchido", "char_length(btrim(rotulo)) > 0");
            });
            nivel.WithOwner().HasForeignKey("EtapaProcessoId").HasConstraintName("fk_niveis_atendimento_etapa");
            nivel.Property<int>("Id").ValueGeneratedOnAdd().HasComment("Identificador técnico do nível.");
            nivel.HasKey("Id");
            nivel.Property<Guid>("EtapaProcessoId").HasComment("Etapa à qual o nível pertence.");
            nivel.Property(n => n.Rotulo).HasMaxLength(NivelDeAtendimento.RotuloMaxLength).IsRequired().HasComment("Texto escolhido pela banca na avaliação.");
            nivel.Property(n => n.Fracao).HasPrecision(5, 4).IsRequired().HasComment("Fração da pontuação do item atribuída ao nível.");
            // A unicidade da ordem e do rótulo é adiada pelo script de restrições para permitir trocas.
            nivel.Property(n => n.Ordem).IsRequired().HasComment("Posição do nível na escala, a partir de um.");
        });
        builder.Navigation(e => e.EscalaDeAtendimento).HasField("_escalaDeAtendimento").UsePropertyAccessMode(PropertyAccessMode.Field);

        // A unicidade da ordem é adiada pelo script de restrições para permitir trocas.
        builder.HasIndex(e => new { e.ProcessoSeletivoId, e.Ordem });
    }
    private static string CodigosNumericos<TEnum>() where TEnum : struct, Enum =>
        string.Join(", ", Enum.GetValues<TEnum>().Select(static valor => $"{Convert.ToInt32(valor, System.Globalization.CultureInfo.InvariantCulture)} ({valor})"));
}
