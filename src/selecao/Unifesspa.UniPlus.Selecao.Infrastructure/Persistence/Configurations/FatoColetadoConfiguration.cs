namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Configuração EF Core de <see cref="FatoColetado"/> (apresentação —
/// Rotulo/TipoDeCampo/Obrigatorio) — entidade filha de
/// <c>ProcessoSeletivo</c>, <c>EntityBase</c> puro (sem soft-delete). Substituível por inteiro
/// junto com o processo, mesmo padrão de <c>DocumentoExigido</c>.
/// </summary>
public sealed class FatoColetadoConfiguration : IEntityTypeConfiguration<FatoColetado>
{
    private const int TipoDeCampoMaxLength = 30;

    public void Configure(EntityTypeBuilder<FatoColetado> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.Property(f => f.ProcessoSeletivoId).HasComment("Identificador do processo seletivo ao qual pertence.");
        builder.Property(f => f.GrupoColetadoId).HasComment("Identificador do grupo repetível ao qual o dado pertence, quando houver.");
        builder.Property(f => f.FatoCodigo).HasComment("Código do dado do candidato. Exemplos: RENDA_PER_CAPITA, CONCORRER_RENDA.");
        builder.Property(f => f.Ordem).HasComment("Posição de apresentação ou avaliação no conjunto.");
        builder.Property(f => f.Rotulo).HasComment("Texto apresentado à pessoa candidata.");
        builder.Property(f => f.TipoDeCampo).HasColumnName("tipo_de_campo").HasComment("Tipo de campo: NUMERO, SIM_OU_NAO, ESCOLHA_UNICA, ESCOLHA_MULTIPLA, TEXTO, DATA, ENDERECO ou MUNICIPIO.");
        builder.Property(f => f.Obrigatoriedade).HasComment("Regra que determina quando a resposta é obrigatória, em JSON.");
        builder.Property(f => f.Ajuda).HasComment("Orientação adicional exibida junto ao campo, quando houver.");
        builder.Property(f => f.PedirConfirmacao).HasComment("Indica se a resposta deve ser confirmada pela pessoa candidata.");
        builder.Property(f => f.Restricoes).HasComment("Restrições aplicadas à resposta, em JSON.");
        builder.Property(f => f.Impedimento).HasComment("Regra de impedimento associada à resposta, em JSON, quando houver.");
        builder.Property(f => f.OrigemValores).HasComment("Origem das opções.");
        builder.Property(f => f.Formato).HasComment("Formato esperado da resposta, quando especificado.");
        builder.Property(f => f.ClassificacaoProtecao).HasComment("Classificação de proteção do dado coletado.");
        builder.Property(f => f.HipoteseLegal).HasComment("Hipótese legal que fundamenta o tratamento do dado.");
        builder.Property(f => f.FinalidadeTratamento).HasComment("Finalidade informada para o tratamento do dado.");
        builder.Property(f => f.Descricao).HasComment("Descrição do dado solicitado.");
        builder.Property(f => f.ComponenteExibicao).HasComment("Componente usado para exibir o dado, quando especificado.");
        builder.Property(f => f.EtapaCodigo).HasComment("Código da página do formulário, quando houver.");

        builder.ToTable("dados_dos_formularios", t =>
        {
            t.HasComment("Dados solicitados nos formulários de um processo seletivo.");
            t.HasCheckConstraint("ck_dados_dos_formularios_finalidade", $"finalidade IN ({SqlDosCodigosDeContrato.Lista<FinalidadeFormulario>()})");
            t.HasCheckConstraint("ck_dados_dos_formularios_origem_valores", $"origem_valores IN ({SqlDosCodigosDeContrato.Lista<OrigemValoresColeta>()})");
        });
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();

        builder.Property(f => f.FatoCodigo).HasColumnName("codigo_do_dado").HasMaxLength(FormaDoItem.FatoCodigoMaxLength).IsRequired();
        builder.Property(f => f.Ordem).IsRequired();
        builder.Property(f => f.Rotulo).HasMaxLength(FormaDoItem.RotuloMaxLength).IsRequired();
        builder.Property(f => f.TipoDeCampo).HasMaxLength(TipoDeCampoMaxLength).IsRequired();
        builder.Property(f => f.Obrigatoriedade)
            .HasConversion(ConversoresDeRegras.Obrigatoriedade, ConversoresDeRegras.ComparadorDeObrigatoriedade)
            .HasColumnType("jsonb")
            .IsRequired();
        builder.Property(f => f.Ajuda).HasMaxLength(FormaDoItem.AjudaMaxLength);
        builder.Property(f => f.PedirConfirmacao).IsRequired();
        builder.Property(f => f.Restricoes)
            .HasConversion(ConversoresDeRegras.Restricoes, ConversoresDeRegras.ComparadorDeRestricoes)
            .HasColumnType("jsonb")
            .IsRequired();
        builder.Property(f => f.Impedimento)
            .HasConversion(ConversoresDeRegras.Impedimento, ConversoresDeRegras.ComparadorDeImpedimento)
            .HasColumnType("jsonb");
        builder.Property(f => f.OrigemValores).HasMaxLength(TamanhoDoCodigoDeContrato.Maximo).IsRequired();
        builder.Property(f => f.Formato).HasMaxLength(FormaDoItem.FormatoMaxLength);
        builder.Property(f => f.ClassificacaoProtecao).HasMaxLength(FormaDoItem.ClassificacaoProtecaoMaxLength).IsRequired();
        builder.Property(f => f.HipoteseLegal).HasMaxLength(FormaDoItem.HipoteseLegalMaxLength).IsRequired();
        builder.Property(f => f.FinalidadeTratamento).HasMaxLength(FormaDoItem.FinalidadeTratamentoMaxLength).IsRequired();
        builder.Property(f => f.Descricao).HasMaxLength(FormaDoItem.DescricaoDoFatoMaxLength).IsRequired();
        builder.Property(f => f.ComponenteExibicao).HasMaxLength(FormaDoItem.ComponenteExibicaoMaxLength);
        builder.Ignore(f => f.OpcoesDoProcesso);
        builder.Ignore(f => f.Condicoes);
        builder.Ignore(f => f.Exibicao);
        builder.Ignore(f => f.Apresentacao);
        builder.Ignore(f => f.Regras);
        builder.Property(f => f.Finalidade).HasMaxLength(TamanhoDoCodigoDeContrato.Maximo).IsRequired().HasComment("Finalidade do formulário.");
        builder.Property(f => f.EtapaCodigo).HasColumnName("pagina_codigo").HasMaxLength(FormaDaEtapa.CodigoMaxLength);

        // As duas unicidades são invariantes do agregado, feitas cumprir em
        // DefinirFatosColetados; os índices as garantem também contra escrita concorrente e
        // contra qualquer caminho que não passe pelo agregado.
        builder.HasIndex(f => new { f.ProcessoSeletivoId, f.FatoCodigo })
            .IsUnique()
            .HasDatabaseName("ux_dados_dos_formularios_processo_dado");

        // O fato tem um só produtor no processo, item ou campo de grupo. A ordem do item é única no
        // formulário, e a do campo de grupo, dentro do grupo; a ordem que o grupo divide com os
        // itens é invariante do agregado.
        builder.HasIndex(f => new { f.ProcessoSeletivoId, f.Finalidade, f.Ordem })
            .IsUnique()
            .HasFilter("grupo_coletado_id IS NULL")
            .HasDatabaseName("ux_dados_dos_formularios_processo_finalidade_ordem");
        builder.HasIndex(f => new { f.GrupoColetadoId, f.Ordem })
            .IsUnique()
            .HasDatabaseName("ux_dados_dos_formularios_grupo_ordem");

        builder.HasMany(f => f.Precondicoes)
            .WithOne()
            .HasForeignKey(c => c.FatoColetadoId)
            .HasConstraintName("fk_condicoes_de_exibicao_do_dado_dado_formulario")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(f => f.Precondicoes)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
