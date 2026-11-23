namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ConferenciaDaConfiguracao;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// O painel de conformidade e a conferência da configuração julgam o mesmo processo: todos os itens do painel conformes é a
/// configuração apta, inclusive nas declarações das fases e das etapas, que têm item próprio para o que nenhum outro item cobre.
/// </summary>
public sealed class ParidadeDoPainelComAConferenciaTests
{
    public static TheoryData<string> Cenarios => ["conforme", "sem etapas", "sem identificador", "etapa com fase inexistente", "fase com janela invertida", "etapa com janela invertida"];

    [Theory(DisplayName = "O painel mostra todos os itens conformes exatamente quando a configuração está apta")]
    [MemberData(nameof(Cenarios))]
    public void Painel_TodoConforme_SeESoSeAConfiguracaoEstaApta(string cenario)
    {
        ProcessoSeletivo processo = Montar(cenario);
        ConfiguracaoDeclarada configuracao = processo.Declaracao();
        CadastrosDaConfiguracao cadastros = CadastrosDeTeste.Para(processo);

        bool painelTodoConforme = ChecklistDeConformidade.AvaliarConformidade(configuracao, cadastros).All(static item => item.Ok);
        bool configuracaoApta = ConfiguracaoAptaACongelar.Conferir(processo.Id, configuracao, cadastros).IsSuccess;

        painelTodoConforme.Should().Be(configuracaoApta);
    }

    [Fact(DisplayName = "Referência ausente no catálogo impede a conferência e aparece no painel")]
    public void ReferenciaAusente_PainelEConferenciaApontamPendencia()
    {
        ProcessoSeletivo processo = Montar("conforme");
        ConfiguracaoDeclarada configuracao = processo.Declaracao();
        CadastrosDaConfiguracao cadastros = CadastrosDeTeste.Padrao;

        CausasQueImpedemCongelar.De(configuracao, cadastros)
            .Should().Contain(erro => erro.Error.Code == "ProcessoSeletivo.RegraCitadaNaoCorrespondeAoCatalogo");
        ChecklistDeConformidade.AvaliarConformidade(configuracao, cadastros)
            .Should().Contain(item => item.Codigo.StartsWith("regra_citada_", StringComparison.Ordinal) && !item.Ok);
    }

    [Theory(DisplayName = "Cada item estrutural pendente do painel é uma causa de conformidade no bloco da dimensão dele, e só eles")]
    [MemberData(nameof(Cenarios))]
    public void ItemEstruturalPendente_EUmaCausaDeConformidadeNoBlocoDaDimensao(string cenario)
    {
        ConfiguracaoDeclarada configuracao = Montar(cenario).Declaracao();

        (string Codigo, string? Bloco)[] pendentes =
            [.. ItensEstruturaisDeConformidade.Avaliar(configuracao).Where(static item => !item.Ok).Select(static item => (item.Codigo, (string?)DimensaoConformidade.BlocoDoDocumento(item.Dimensao)))];
        (string? Origem, string? Campo)[] causas =
            [.. ChecklistDeConformidade.CausasDeConformidade(configuracao).Select(static causa => (causa.Origem, causa.Field))];

        causas.Should().Equal(pendentes.Select(static pendente => ((string?)pendente.Codigo, pendente.Bloco)));
    }

    [Theory(DisplayName = "Com defeito só num assunto, os itens do painel pendentes são exatamente os desse assunto, e os dos demais ficam conformes")]
    [MemberData(nameof(AssuntosDoPainel))]
    public void AssuntoComDefeito_PendeSoOsItensDoAssuntoNoPainel(string assunto)
    {
        CenarioDeCongelamentoPorAssunto comDefeito = CenarioDeCongelamentoPorAssunto.Para("Publicar");
        comDefeito.Quebrar(new HashSet<string> { assunto });

        comDefeito.CausasPorAssunto().Single(doAssunto => doAssunto.Assunto == assunto).Causas.Should().NotBeEmpty();
        comDefeito.CausasPorAssunto().Where(doAssunto => doAssunto.Assunto != assunto).Should().OnlyContain(static doAssunto => doAssunto.Causas.Count == 0);
        comDefeito.Painel().Where(static item => !item.Ok).Select(static item => item.Codigo).Should().BeEquivalentTo(ItensPendentesPorAssunto[assunto]);
        CenarioDeCongelamentoPorAssunto.Para("Publicar").Painel().Should().OnlyContain(static item => item.Ok);
    }

    [Fact(DisplayName = "O assunto de conformidade por item tem uma causa para cada item pendente do painel, com o mesmo código e o bloco da dimensão dele")]
    public void AssuntoDeConformidade_TemUmaCausaPorItemPendenteNoMesmoBloco()
    {
        CenarioDeCongelamentoPorAssunto comDefeito = CenarioDeCongelamentoPorAssunto.Para("Publicar");
        comDefeito.Quebrar(new HashSet<string> { "Conformidade" });

        (string Codigo, string Bloco)[] doPainel =
            [.. comDefeito.Painel().Where(static item => !item.Ok).Select(static item => (item.Codigo, DimensaoConformidade.BlocoDoDocumento(item.Dimensao)))];
        (string? Origem, string? Campo)[] dasCausas =
            [.. comDefeito.CausasPorAssunto().Single(static doAssunto => doAssunto.Assunto == "Conformidade").Causas.Select(static causa => (causa.Origem, causa.Field))];

        dasCausas.Should().Equal(doPainel.Select(static item => ((string?)item.Codigo, (string?)item.Bloco)));
    }

    [Fact(DisplayName = "O catálogo de itens pendentes por assunto cobre exatamente os assuntos da conferência")]
    public void ItensPendentesPorAssunto_CobremOsAssuntosDaConferencia() =>
        ItensPendentesPorAssunto.Keys.Should().BeEquivalentTo(CausasQueImpedemCongelar.Assuntos.Select(static assunto => assunto.Nome));

    public static TheoryData<string> AssuntosDoPainel => [.. ItensPendentesPorAssunto.Keys];

    private static readonly Dictionary<string, string[]> ItensPendentesPorAssunto = new(StringComparer.Ordinal)
    {
        ["Identificacao"] = ["identificador_legivel_nao_declarado", "localidade_nao_declarada", "algoritmo_contagem_prazo_nao_declarado", "calendario_vigente_ausente"],
        ["Conformidade"] = ["taxa_inscricao_nao_declarada"],
        ["Declaracoes"] = ["cronograma_fases_declaradas"],
        ["ReferenciasDeRegra"] = ["regra_citada_classificacao.regraCalculo"],
        ["Conjunto"] = ["etapas_em_conjunto"],
        ["Completude"] = ["cronograma_fase_agrupadora_sem_etapa_pontuada", "cronograma_etapa_declara_a_fase"],
        ["Recursos"] = ["cronograma_procedimento_de_recurso"],
        ["Bancas"] = ["cronograma_recurso_com_banca_que_julga"],
        ["CorrecaoIndependente"] = ["etapa_correcao_independente_com_dois_corretores"],
        ["TiposDeAto"] = ["cronograma_resultado_declarado_com_papel", "cronograma_tipo_que_congela_nao_eh_resultado", "cronograma_tipo_de_ato_unico_declarado_uma_vez"],
        ["Cascata"] = ["cascata_pendente", "cascata_modalidade_fora_do_regime_federal"],
        ["CondicoesDaCanonicalizacao"] = ["exigencia_condicional_vazia_determina_resultado"],
        ["MotivosDeDecisao"] = ["motivos_escolhidos_ativos_no_cadastro", "motivos_escolhidos_de_tipo_declarado_pelo_lugar"],
    };

    private static readonly Dictionary<string, Action<ProcessoSeletivo>> Estragos = new(StringComparer.Ordinal)
    {
        ["conforme"] = static _ => { },
        ["sem etapas"] = static processo => ProcessoCarregadoDoBanco.EsvaziarEtapas(processo),
        ["sem identificador"] = static processo => typeof(ProcessoSeletivo)
            .GetProperty(nameof(ProcessoSeletivo.IdentificadorLegivel), BindingFlags.Instance | BindingFlags.Public)!.SetValue(processo, null),
        ["etapa com fase inexistente"] = static processo => ProcessoCarregadoDoBanco.Etapa(
            processo,
            ApoioDeEtapaReidratada.DeProvaObjetiva(Guid.CreateVersion7(), "Prova", 99, "FASE_INEXISTENTE", 1m)),
        ["fase com janela invertida"] = static processo => ProcessoCarregadoDoBanco.Fase(processo, FaseComJanelaInvertida()),
        ["etapa com janela invertida"] = static processo => ProcessoCarregadoDoBanco.InverterJanela(processo.Etapas.First()),
    };

    private static readonly DateTimeOffset Inicio = new(2026, 3, 2, 12, 0, 0, TimeSpan.Zero);

    private static FaseCronograma FaseComJanelaInvertida() => FaseCronograma.Reidratar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        Guid.CreateVersion7(),
        new OrigemCanonicaDaFase(Guid.CreateVersion7(), "FASE_DA_JANELA", OrigemDataFase.NoEdital, false, false, false, false),
        new DecisoesDeCronogramaDaFase(99, Inicio, Inicio.AddDays(-1), UnidadesDeTeste.Ceps),
        new PublicacoesDaFase([], false, null),
        []);

    private static ProcessoSeletivo Montar(string cenario)
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        Estragos[cenario](processo);

        return processo;
    }
}
