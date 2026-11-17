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

        bool painelTodoConforme = ChecklistDeConformidade.AvaliarConformidade(configuracao, CadastrosDeTeste.Padrao).All(static item => item.Ok);
        bool configuracaoApta = ConfiguracaoAptaACongelar.Conferir(processo.Id, configuracao, CadastrosDeTeste.Padrao).IsSuccess;

        painelTodoConforme.Should().Be(configuracaoApta);
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

    [Theory(DisplayName = "Com defeito só numa família, os itens do painel pendentes são exatamente os dessa família, e os das demais ficam conformes")]
    [MemberData(nameof(FamiliasDoPainel))]
    public void FamiliaComDefeito_PendeSoOsItensDaFamiliaNoPainel(string familia)
    {
        CenarioDeCongelamentoPorFamilia comDefeito = CenarioDeCongelamentoPorFamilia.Para("Publicar");
        comDefeito.Quebrar(new HashSet<string> { familia });

        comDefeito.CausasPorFamilia().Single(daFamilia => daFamilia.Familia == familia).Causas.Should().NotBeEmpty();
        comDefeito.CausasPorFamilia().Where(daFamilia => daFamilia.Familia != familia).Should().OnlyContain(static daFamilia => daFamilia.Causas.Count == 0);
        comDefeito.Painel().Where(static item => !item.Ok).Select(static item => item.Codigo).Should().BeEquivalentTo(ItensPendentesPorFamilia[familia]);
        CenarioDeCongelamentoPorFamilia.Para("Publicar").Painel().Should().OnlyContain(static item => item.Ok);
    }

    [Fact(DisplayName = "A família de conformidade por item tem uma causa para cada item pendente do painel, com o mesmo código e o bloco da dimensão dele")]
    public void FamiliaDeConformidade_TemUmaCausaPorItemPendenteNoMesmoBloco()
    {
        CenarioDeCongelamentoPorFamilia comDefeito = CenarioDeCongelamentoPorFamilia.Para("Publicar");
        comDefeito.Quebrar(new HashSet<string> { "Conformidade" });

        (string Codigo, string Bloco)[] doPainel =
            [.. comDefeito.Painel().Where(static item => !item.Ok).Select(static item => (item.Codigo, DimensaoConformidade.BlocoDoDocumento(item.Dimensao)))];
        (string? Origem, string? Campo)[] dasCausas =
            [.. comDefeito.CausasPorFamilia().Single(static daFamilia => daFamilia.Familia == "Conformidade").Causas.Select(static causa => (causa.Origem, causa.Field))];

        dasCausas.Should().Equal(doPainel.Select(static item => ((string?)item.Codigo, (string?)item.Bloco)));
    }

    [Fact(DisplayName = "O catálogo de itens pendentes por família cobre exatamente as famílias da conferência")]
    public void ItensPendentesPorFamilia_CobremAsFamiliasDaConferencia() =>
        ItensPendentesPorFamilia.Keys.Should().BeEquivalentTo(CausasQueImpedemCongelar.Familias.Select(static familia => familia.Nome));

    public static TheoryData<string> FamiliasDoPainel => [.. ItensPendentesPorFamilia.Keys];

    private static readonly Dictionary<string, string[]> ItensPendentesPorFamilia = new(StringComparer.Ordinal)
    {
        ["Identificacao"] = ["identificador_legivel_nao_declarado", "localidade_nao_declarada", "algoritmo_contagem_prazo_nao_declarado", "calendario_vigente_ausente"],
        ["Conformidade"] = ["taxa_inscricao_nao_declarada"],
        ["Declaracoes"] = ["cronograma_fases_declaradas"],
        ["Conjunto"] = ["etapas_em_conjunto"],
        ["Completude"] = ["cronograma_fase_agrupadora_sem_etapa_pontuada", "cronograma_etapa_declara_a_fase"],
        ["Recursos"] = ["cronograma_procedimento_de_recurso"],
        ["Bancas"] = ["cronograma_recurso_com_banca_de_analise"],
        ["CorrecaoIndependente"] = ["etapa_correcao_independente_com_dois_corretores"],
        ["TiposDeAto"] = ["cronograma_resultado_declarado_com_papel", "cronograma_tipo_que_congela_nao_eh_resultado", "cronograma_tipo_de_ato_unico_declarado_uma_vez"],
        ["Cascata"] = ["cascata_pendente", "cascata_modalidade_fora_do_regime_federal"],
        ["CondicoesDaCanonicalizacao"] = ["exigencia_condicional_vazia_determina_resultado"],
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

    private static FaseCronograma FaseComJanelaInvertida() => FaseCronograma.Reidratar(
        Guid.CreateVersion7(),
        new OrigemCanonicaDaFase(Guid.CreateVersion7(), "FASE_DA_JANELA", OrigemDataFase.NoEdital, false, false, false, false),
        new DecisoesDeCronogramaDaFase(99, Inicio, Inicio.AddDays(-1), null, UnidadesDeTeste.Ceps),
        new PublicacoesDaFase([], false, null),
        []);

    private static ProcessoSeletivo Montar(string cenario)
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        Estragos[cenario](processo);

        return processo;
    }
}
