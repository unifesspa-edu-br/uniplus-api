namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

internal sealed class ModalidadeConfiguration : IEntityTypeConfiguration<Modalidade>
{
    private const int CodigoMaxLength = 60;
    private const int NomeMaxLength = 200;
    private const int DescricaoMaxLength = 300;
    private const int CodigoDeContratoMaxLength = 30;
    private const int CodigoReferenciaMaxLength = 60;
    private const int BaseLegalMaxLength = 500;
    private const int AuditUserMaxLength = 255;

    // Instante fixo do seed (HasData exige valor determinístico).
    private static readonly DateTimeOffset InstanteDoSeed = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public void Configure(EntityTypeBuilder<Modalidade> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
        builder.ToTable("modalidades", tabela =>
        {
            tabela.HasComment("Modalidades de concorrência e suas regras de composição e remanejamento de vagas.");
            ConfigurarChecks(tabela);
        });
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasComment("Identificador da modalidade.");
        ConfigurarIdentificacao(builder);
        ConfigurarNaturezaEComposicaoDasVagas(builder);
        ConfigurarRemanejamento(builder);
        ConfigurarParametrosDoRemanejamento(builder);
        ConfigurarRegraDeAplicabilidade(builder);
        ConfigurarAuditoria(builder);
        ConfigurarIndices(builder);
        builder.HasData(MaterializarSeed());
    }

    // Codigo é value object imutável — persistido por valor como varchar via
    // CodigoModalidadeValueConverter (reidratação falha de imediato se o código gravado for inválido). O CHECK de formato
    // e o índice único parcial protegem a coluna.
    private static void ConfigurarIdentificacao(EntityTypeBuilder<Modalidade> builder)
    {
        builder.Property(m => m.Codigo)
            .HasComment("Código que identifica a modalidade. Exemplos: AC, AC_PCD.")
            .HasConversion<CodigoModalidadeValueConverter>()
            .HasMaxLength(CodigoMaxLength)
            .IsRequired();

        builder.Property(m => m.Nome).HasComment("Nome da modalidade para apresentação.").HasMaxLength(NomeMaxLength).IsRequired();
        builder.Property(m => m.Descricao).HasComment("Descrição da modalidade para apresentação.").HasMaxLength(DescricaoMaxLength);
        builder.Property(m => m.BaseLegal).HasComment("Fundamento legal da modalidade, quando informado.").HasMaxLength(BaseLegalMaxLength);
    }

    private static void ConfigurarNaturezaEComposicaoDasVagas(EntityTypeBuilder<Modalidade> builder)
    {
        builder.Property(m => m.NaturezaLegal)
            .HasComment("Natureza legal da modalidade.")
            .HasMaxLength(CodigoDeContratoMaxLength)
            .IsRequired();

        builder.Property(m => m.ComposicaoVagas)
            .HasComment("Forma de composição das vagas.")
            .HasMaxLength(CodigoDeContratoMaxLength)
            .IsRequired();

        builder.Property(m => m.ComposicaoOrigem).HasComment("Código da modalidade de origem das vagas, quando retiradas de outra modalidade.").HasMaxLength(CodigoReferenciaMaxLength);
    }

    // Enums opcionais: a ausência é null na coluna.
    private static void ConfigurarRemanejamento(EntityTypeBuilder<Modalidade> builder)
    {
        builder.Property(m => m.RegraRemanejamento)
            .HasComment("Regra aplicada ao remanejamento das vagas, quando definida.")
            .HasMaxLength(CodigoDeContratoMaxLength);

        builder.Property(m => m.AcaoQuandoIndeferido)
            .HasComment("Ação aplicada quando a candidatura é indeferida, quando definida.")
            .HasMaxLength(CodigoDeContratoMaxLength);
    }

    private static void ConfigurarParametrosDoRemanejamento(EntityTypeBuilder<Modalidade> builder)
    {
        builder.OwnsOne(m => m.RemanejamentoParametros, parametros =>
        {
            parametros.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
            parametros.Property(p => p.Destino).HasComment("Código da modalidade que recebe as vagas no destino único.").HasColumnName("remanejamento_destino").HasMaxLength(CodigoReferenciaMaxLength);
            parametros.Property(p => p.Par).HasComment("Código da modalidade que participa do remanejamento cruzado.").HasColumnName("remanejamento_par").HasMaxLength(CodigoReferenciaMaxLength);
            parametros.Property(p => p.DestinoFinal).HasComment("Código da modalidade que recebe as vagas restantes ao fim do remanejamento cruzado.").HasColumnName("remanejamento_destino_final").HasMaxLength(CodigoReferenciaMaxLength);
            parametros.Ignore(p => p.TemAlgum);
            parametros.HasData(ModalidadeSeed.Itens.Select(item => new
            {
                ModalidadeId = item.Id,
                item.RemanejamentoParametros.Destino,
                item.RemanejamentoParametros.Par,
                item.RemanejamentoParametros.DestinoFinal,
            }));
        });
        builder.Navigation(m => m.RemanejamentoParametros).IsRequired();
    }

    // A regra de aplicabilidade (ADR-0144): o predicado como lista de cláusulas, cada uma lista
    // de condições. Sem default: toda modalidade declara a sua, e a incondicional é a lista vazia.
    private static void ConfigurarRegraDeAplicabilidade(EntityTypeBuilder<Modalidade> builder)
    {
        builder.Property(m => m.RegraDeAplicabilidade)
            .HasComment("Condições que determinam a aplicação da modalidade, em JSON; lista vazia indica aplicação incondicional.")
            .HasConversion(PredicadoDnfColunaJson.Converter, PredicadoDnfColunaJson.Comparer)
            .HasColumnType("jsonb")
            .IsRequired();
    }

    // Auditoria (IAuditableEntity)
    private static void ConfigurarAuditoria(EntityTypeBuilder<Modalidade> builder)
    {
        builder.Property(m => m.CreatedBy).HasMaxLength(AuditUserMaxLength);
        builder.Property(m => m.UpdatedBy).HasMaxLength(AuditUserMaxLength);
    }

    // Unicidade do código e do nome entre modalidades vivas (índices parciais) — uma
    // modalidade viva por código e por nome; soft-delete libera o slot para recriação.
    private static void ConfigurarIndices(EntityTypeBuilder<Modalidade> builder)
    {
        builder.HasIndex(m => m.Codigo)
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName(RestricoesDoBanco.ModalidadeCodigoAtivos);

        builder.HasIndex(m => m.Nome)
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName(RestricoesDoBanco.ModalidadeNomeAtivos);
    }

    /// <summary>
    /// Projeta o seed (<see cref="ModalidadeSeed.Itens"/>) para linhas que o <c>HasData</c> congela
    /// como literais na migration inicial. O instante é fixo (as linhas não passam pelo
    /// <c>AuditableInterceptor</c>); as oito federais mais AC, AC_PCD, AC_I e AC_Q
    /// nascem com a migration inicial.
    /// </summary>
    private static IEnumerable<object> MaterializarSeed() =>
        ModalidadeSeed.Itens.Select(LinhaDoSeed);

    private static object LinhaDoSeed(ModalidadeSeedItem item) => new
    {
        item.Id,
        Codigo = CodigoModalidade.Criar(item.Codigo).Match(
            codigo => codigo,
            erro => throw new InvalidOperationException($"Código de modalidade inválido no cadastro inicial: {erro.Message}")),
        item.Nome,
        item.Descricao,
        NaturezaLegal = item.Natureza,
        ComposicaoVagas = item.Composicao,
        item.ComposicaoOrigem,
        RegraRemanejamento = item.Regra,
        item.RegraDeAplicabilidade,
        AcaoQuandoIndeferido = (AcaoQuandoIndeferido?)null,
        item.BaseLegal,
        CreatedAt = InstanteDoSeed,
        IsDeleted = false,
    };


    private static void ConfigurarChecks(TableBuilder<Modalidade> table)
    {
        ConfigurarChecksDosDominiosDasVagas(table);
        ConfigurarChecksDosDominiosDoRemanejamento(table);
        ConfigurarChecksDeCoerenciaDoCodigoEDaComposicao(table);
        string destinoUnico = SqlDosCodigosDeContrato.Literal(RegraRemanejamento.DestinoUnico);
        string cruzado = SqlDosCodigosDeContrato.Literal(RegraRemanejamento.Cruzado);
        table.HasCheckConstraint("ck_modalidades_remanejamento_destino_unico", $"regra_remanejamento IS DISTINCT FROM {destinoUnico} OR (remanejamento_destino IS NOT NULL AND remanejamento_par IS NULL AND remanejamento_destino_final IS NULL)");
        table.HasCheckConstraint("ck_modalidades_remanejamento_cruzado", $"regra_remanejamento IS DISTINCT FROM {cruzado} OR (remanejamento_par IS NOT NULL AND remanejamento_destino IS NULL)");
        table.HasCheckConstraint("ck_modalidades_remanejamento_sem_parametros", $"(regra_remanejamento IS NOT NULL AND regra_remanejamento IN ({destinoUnico}, {cruzado})) OR (remanejamento_destino IS NULL AND remanejamento_par IS NULL AND remanejamento_destino_final IS NULL)");
    }

    // Domínios fechados dos enums (defesa em profundidade contra inserts crus).
    private static void ConfigurarChecksDosDominiosDasVagas(TableBuilder<Modalidade> table)
    {
        table.HasCheckConstraint(
            "ck_modalidades_natureza_legal",
            $"natureza_legal IN ({SqlDosCodigosDeContrato.Lista<NaturezaLegal>()})");

        table.HasCheckConstraint(
            "ck_modalidades_composicao_vagas",
            $"composicao_vagas IN ({SqlDosCodigosDeContrato.Lista<ComposicaoVagas>()})");
    }

    private static void ConfigurarChecksDosDominiosDoRemanejamento(TableBuilder<Modalidade> table)
    {
        table.HasCheckConstraint(
            "ck_modalidades_regra_remanejamento",
            $"regra_remanejamento IS NULL OR regra_remanejamento IN ({SqlDosCodigosDeContrato.Lista<RegraRemanejamento>()})");

        table.HasCheckConstraint(
            "ck_modalidades_acao_quando_indeferido",
            $"acao_quando_indeferido IS NULL OR acao_quando_indeferido IN ({SqlDosCodigosDeContrato.Lista<AcaoQuandoIndeferido>()})");
    }

    // Formato fechado do código — alinhado ao value object CodigoModalidade.
    // Equivalência exata RETIRADA_DE_OUTRA_MODALIDADE ⟺ composicao_origem preenchida (invariante 4),
    // como igualdade de dois booleanos — null-safe (ambos os lados nunca são null).
    private static void ConfigurarChecksDeCoerenciaDoCodigoEDaComposicao(TableBuilder<Modalidade> table)
    {
        table.HasCheckConstraint(
            "ck_modalidades_codigo_formato",
            $"codigo ~ '{FormatoDoCodigo.ExpressaoNoBanco}'");

        table.HasCheckConstraint(
            "ck_modalidades_retirada_de_outra_modalidade_origem",
            $"(composicao_vagas = {SqlDosCodigosDeContrato.Literal(ComposicaoVagas.RetiradaDeOutraModalidade)}) = (composicao_origem IS NOT NULL)");
    }

}
