namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.ApoioDaSegundaInstancia;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.CenarioDeProcesso;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.DeclaracoesAdotadasDeTeste;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.RecursosParaQuemJulga;

/// <summary>
/// A ordem única das causas das bancas na publicação e a dos itens do painel. Cada configuração dispara cada causa uma vez, e as
/// causas ficam em lugares diferentes, para que a sequência seja a da regra e não a da posição da fase ou da etapa.
/// </summary>
public sealed class OrdemDasCausasDasBancasTests
{
    private const string Avaliacao = "AVALIACAO";
    private const string Homologacao = "HOMOLOGACAO_DAS_INSCRICOES";
    private const string Resultado = "RESULTADO";

    private static readonly string[] CausasDasBancas =
    [
        "Cronograma.RecursoSemBanca",
        "Banca.TipoNaoAdmiteInstanciaSuperior",
        "Banca.FaseSemBancaDaSegundaInstancia",
        "Banca.EtapaSemBancaDaSegundaInstancia",
        "Banca.FaseComBancaDaSegundaInstanciaSemRecursoParaJulgar",
        "Banca.EtapaComBancaDaSegundaInstanciaSemRecursoParaJulgar",
        "Banca.FaseComMaisDeUmaBancaDaSegundaInstancia",
        "Banca.EtapaComMaisDeUmaBancaDaSegundaInstancia",
        "Banca.TipoExigeMaisDeUmaBanca",
        "ProcessoSeletivo.HomologacaoDasInscricoesSemBanca",
        "ProcessoSeletivo.NotaDoEnemSemBancaDeImportacaoDasNotas",
        "ProcessoSeletivo.ImportacaoExternaSemBancaDeImportacaoDeCandidatos",
        "Banca.RegraDeDeliberacaoObrigatoria",
        "Banca.IntegrantesAbaixoDoMinimo",
        "EtapaProcesso.RegraDeConsolidacaoNaoResolvida",
    ];

    private static readonly string[] ItensDoPainel =
    [
        "cronograma_banca_de_recursos_sem_candidatos_atendidos",
        "cronograma_recurso_com_banca_que_julga",
        "cronograma_instancia_superior_admitida_pelo_tipo_de_banca",
        "cronograma_instancia_superior_com_banca_da_segunda_instancia",
        "cronograma_banca_da_segunda_instancia_com_recurso_para_julgar",
        "cronograma_uma_banca_da_segunda_instancia_por_fase_ou_etapa",
        "cronograma_tipo_de_banca_com_o_minimo_de_bancas",
        "cronograma_homologacao_das_inscricoes_com_banca",
        "cronograma_nota_do_enem_com_banca_de_importacao_das_notas",
        "cronograma_importacao_externa_com_banca_de_importacao_de_candidatos",
        "cronograma_banca_com_regra_de_deliberacao",
        "cronograma_banca_com_o_minimo_de_integrantes",
    ];

    private static readonly ReferenciaRegra RegraLida = ReferenciaRegra.De(RegraConsolidacaoCodigo.AvaliacaoUnica, "v1", new string('a', 64));

    private static readonly ReferenciaRegra RegraForaDoCatalogo = ReferenciaRegra.De(RegraConsolidacaoCodigo.AvaliacaoUnica, "v1", new string('b', 64));

    private static readonly CorretoresDasEtapas TresCorretores =
        new(new Dictionary<ReferenciaRegra, CorretoresDaRegra> { [RegraLida] = CorretoresDaRegra.SemNovoCorretor(3) });

    private static readonly ProcedimentoDeRecursoDeclarado[] DaNota = [Procedimento(TipoMotivoDeDecisao.NotaDaEtapa)];

    private static readonly RegraDeHomologacaoDasInscricoes RegraDaHomologacao = RegraDeHomologacaoDasInscricoes.Declarar("RESULTADO", []);

    private static ModeloDeFase FaseQueHomologa(int ordem, params BancaRequerida[] bancas) => new ModeloDeFase(Homologacao, ordem).Homologando(RegraDaHomologacao) with { Bancas = bancas };

    private static ModeloDeEtapa DaRegraLida(ModeloDeEtapa etapa, ReferenciaRegra? regra = null) =>
        etapa with { Forma = FormaDeAvaliacao.Humana, RegraConsolidacao = regra ?? RegraLida, Passo = 0.5m };

    // O tipo que exige duas ou mais bancas e avalia a nota da etapa, para que a banca sozinha seja recusada e conte no mínimo de integrantes.
    private static TipoDeBancaCopiado QueExigeDuasOuMais() => AvaliadoraComMarcas(TipoDeBanca.Entrevista, true, false, TipoMotivoDeDecisao.NotaDaEtapa);

    private static ConfiguracaoDeclarada Montar(ModeloDeFase[] fases, ModeloDeEtapa[] etapas, ConfiguracaoDeclarada raiz) =>
        DeclaracoesAdotadasDeTeste.Lida([.. fases.Select(static fase => fase.Adotada())], [.. etapas.Select(static etapa => etapa.Adotada())], OrigemNoDocumento.Identidade, raiz);

    // A importação externa com um defeito em cada causa, na ordem inversa da posição: as causas das etapas vêm antes das das fases
    // em algumas e depois em outras, e a fase que homologa vem depois da que tem a banca sem regra.
    private static ConfiguracaoDeclarada ImportacaoExternaComTodasAsCausas()
    {
        BancaRequerida semRegra = BancaRequerida.Criar(ChaveLocal.Criar("banca").Value, TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.CorrecaoDeRedacoes), []);
        ModeloDeFase comBancaSemRegra = new ModeloDeFase(Avaliacao, 1) with { Bancas = [semRegra] };
        ModeloDeFase comBancaDaSegundaInstanciaSemRecurso = new ModeloDeFase(Resultado, 2) with { Bancas = [DaSegundaInstanciaNaFase()] };
        ModeloDeFase homologaComInstanciaSuperiorSemBancaEComRecursoSemJulgador =
            FaseComRecurso(Homologacao, [Procedimento(TipoMotivoDeDecisao.HomologacaoDasInscricoes)], true).Homologando(RegraDaHomologacao) with { Ordem = 3 };

        return Montar(
            [comBancaSemRegra, comBancaDaSegundaInstanciaSemRecurso, homologaComInstanciaSuperiorSemBancaEComRecursoSemJulgador,
                FaseComRecurso("EXCESSO", DaNota, true, BancasDeTeste.DeRecursosDaFase(), DaSegundaInstanciaNaFase(), DaSegundaInstanciaNaFase()) with { Ordem = 4 }],
            [.. EtapasComDefeitos(), .. EtapasDaSegundaInstanciaComDefeitos()],
            ApoioConformidadePublicabilidadeEstrutural.ProcessoConforme().Declaracao());
    }

    // A instância superior não admitida, o tipo que exige duas ou mais bancas com os integrantes abaixo do mínimo, e a regra de
    // consolidação que o catálogo não confirmou. A etapa da instância superior não admitida não recebe nenhuma das causas da banca
    // de 2ª instância.
    private static ModeloDeEtapa[] EtapasComDefeitos()
    {
        ModeloDeEtapa instanciaSuperiorNaoAdmitida = EtapaComRecurso(
            "Prova", Avaliacao, [Procedimento(TipoMotivoDeDecisao.Heteroidentificacao)], true, NaEtapa(Avaliadora(TipoDeBanca.Heteroidentificacao, true, TipoMotivoDeDecisao.Heteroidentificacao)));
        ModeloDeEtapa minimoDeBancasEDeIntegrantes = DaRegraLida(EtapaComRecurso(
            "Redação", Avaliacao, DaNota, false, BancaDaEtapa.Criar(ChaveLocal.Criar("banca").Value, QueExigeDuasOuMais(), BancasDeTeste.Regra(integrantes: 2, quorum: 2))));
        ModeloDeEtapa regraNaoResolvida = DaRegraLida(
            EtapaComRecurso("Entrevista", Avaliacao, DaNota, false, NaEtapa(Avaliadora(TipoDeBanca.Entrevista, false, TipoMotivoDeDecisao.NotaDaEtapa))), RegraForaDoCatalogo);

        return [instanciaSuperiorNaoAdmitida, minimoDeBancasEDeIntegrantes, regraNaoResolvida];
    }

    // A banca de 2ª instância numa etapa sem recurso que admita instância superior, e o recurso que admite instância superior em
    // etapa sem banca de 2ª instância, nem dela nem da fase.
    private static ModeloDeEtapa[] EtapasDaSegundaInstanciaComDefeitos()
    {
        ModeloDeEtapa bancaSemRecurso = EtapaSemRecurso(DaSegundaInstanciaNaEtapa());
        ModeloDeEtapa recursoSemBanca = EtapaComRecurso(
            "Documentos", Avaliacao, DaNota, true, NaEtapa(Avaliadora(TipoDeBanca.Entrevista, false, TipoMotivoDeDecisao.NotaDaEtapa)));

        return [bancaSemRecurso, recursoSemBanca, EtapaQueAdmite(BancasDeTeste.DeRecursosDaEtapa(), DaSegundaInstanciaNaEtapa(), DaSegundaInstanciaNaEtapa())];
    }

    // A inscrição própria com a nota do ENEM sem a banca de importação das notas, entre a falta de bancas do tipo, na fase, e a banca
    // sem regra, na etapa.
    private static ConfiguracaoDeclarada InscricaoPropriaComANotaDoEnemSemBancaDeImportacao()
    {
        ModeloDeFase soUmaBancaDoTipo = new ModeloDeFase(Avaliacao, 1) with { Bancas = [NaFase(QueExigeDuasOuMais())] };
        ModeloDeEtapa semRegra = EtapaNaFase(Avaliacao) with { Bancas = [BancaDaEtapa.Criar(ChaveLocal.Criar("banca").Value, Avaliadora(TipoDeBanca.Entrevista, false, TipoMotivoDeDecisao.NotaDaEtapa))] };
        ModeloDeEtapa notaDoEnem = new ModeloDeEtapa("Nota do ENEM").DoEnem() with { FaseCodigo = Avaliacao };

        return Montar(
            [soUmaBancaDoTipo, FaseQueHomologa(2, BancasDeTeste.DaHomologacaoDasInscricoesDaFase())],
            [notaDoEnem, semRegra],
            ProcessoConformeFactory.Criar().Declaracao());
    }

    private static (string Codigo, string? Campo)[] CausasDasBancasDe(ConfiguracaoDeclarada configuracao) =>
    [
        .. CausasQueImpedemCongelar.De(configuracao, CadastrosDeTeste.Com(corretores: TresCorretores))
            .Where(static causa => CausasDasBancas.Contains(causa.Error.Code))
            .Select(static causa => (causa.Error.Code, causa.Field)),
    ];

    private static (string[] Todos, string[] Vermelhos) ItensDe(ConfiguracaoDeclarada configuracao)
    {
        ContextoDaConferencia contexto = ContextoDaConferencia.De(configuracao, CadastrosDeTeste.Com(corretores: TresCorretores));
        IReadOnlyList<ItemConformidade> itens = ConferenciaDasBancas.Itens(configuracao.Identidade.OrigemCandidatos, contexto.Cronograma, contexto.Etapas, TresCorretores);

        return ([.. itens.Select(static item => item.Codigo)], [.. itens.Where(static item => !item.Ok).Select(static item => item.Codigo)]);
    }

    [Fact(DisplayName = "Na importação externa, as causas das bancas saem na ordem única, e não na posição de quem as declara")]
    public void ImportacaoExterna_CausasSaemNaOrdemUnica()
    {
        (string Codigo, string? Campo)[] causas = CausasDasBancasDe(ImportacaoExternaComTodasAsCausas());

        causas.Should().Equal(CausasDaImportacaoExterna);
    }

    [Fact(DisplayName = "A etapa que dispara a instância superior não admitida não recebe a falta nem a sobra da banca de 2ª instância")]
    public void EtapaDaInstanciaSuperiorNaoAdmitida_SoTemACausaDoTipo()
    {
        (string Codigo, string? Campo)[] daEtapa =
            [.. CausasDasBancasDe(ImportacaoExternaComTodasAsCausas()).Where(static causa => causa.Campo!.StartsWith("etapas[0]", StringComparison.Ordinal))];

        daEtapa.Should().Equal(("Banca.TipoNaoAdmiteInstanciaSuperior", "etapas[0].recursos[0].admiteInstanciaSuperior"));
    }

    [Fact(DisplayName = "Na inscrição própria com a nota do ENEM, a falta da banca de importação das notas vem depois do tipo e antes da regra de deliberação")]
    public void InscricaoPropria_FaltaDaImportacaoDasNotasEntreOTipoEAsRegras()
    {
        (string Codigo, string? Campo)[] causas = CausasDasBancasDe(InscricaoPropriaComANotaDoEnemSemBancaDeImportacao());

        causas.Should().Equal(
            ("Banca.TipoExigeMaisDeUmaBanca", "fases[0].bancasRequeridas[0]"),
            ("ProcessoSeletivo.NotaDoEnemSemBancaDeImportacaoDasNotas", "fases"),
            ("Banca.RegraDeDeliberacaoObrigatoria", "etapas[1].bancas[0]"));
    }

    [Fact(DisplayName = "Cada causa das bancas aparece em alguma das duas configurações, e nenhuma aparece duas vezes na mesma")]
    public void DuasConfiguracoes_DisparamCadaCausaDasBancasUmaVez()
    {
        string[] daImportacaoExterna = [.. CausasDasBancasDe(ImportacaoExternaComTodasAsCausas()).Select(static causa => causa.Codigo)];
        string[] daInscricaoPropria = [.. CausasDasBancasDe(InscricaoPropriaComANotaDoEnemSemBancaDeImportacao()).Select(static causa => causa.Codigo)];

        daImportacaoExterna.Should().OnlyHaveUniqueItems();
        daInscricaoPropria.Should().OnlyHaveUniqueItems();
        daImportacaoExterna.Union(daInscricaoPropria).Should().BeEquivalentTo(CausasDasBancas);
    }

    private static readonly (string Codigo, string? Campo)[] CausasDaImportacaoExterna =
    [
        ("Cronograma.RecursoSemBanca", "fases[2].bancasRequeridas"),
        ("Banca.TipoNaoAdmiteInstanciaSuperior", "etapas[0].recursos[0].admiteInstanciaSuperior"),
        ("Banca.FaseSemBancaDaSegundaInstancia", "fases[2].bancasRequeridas"),
        ("Banca.EtapaSemBancaDaSegundaInstancia", "etapas[4].bancas"),
        ("Banca.FaseComBancaDaSegundaInstanciaSemRecursoParaJulgar", "fases[1].bancasRequeridas[0]"),
        ("Banca.EtapaComBancaDaSegundaInstanciaSemRecursoParaJulgar", "etapas[3].bancas[0]"),
        ("Banca.FaseComMaisDeUmaBancaDaSegundaInstancia", "fases[3].bancasRequeridas[2]"),
        ("Banca.EtapaComMaisDeUmaBancaDaSegundaInstancia", "etapas[5].bancas[2]"),
        ("Banca.TipoExigeMaisDeUmaBanca", "etapas[1].bancas[0]"),
        ("ProcessoSeletivo.HomologacaoDasInscricoesSemBanca", "fases[2].bancasRequeridas"),
        ("ProcessoSeletivo.ImportacaoExternaSemBancaDeImportacaoDeCandidatos", "fases"),
        ("Banca.RegraDeDeliberacaoObrigatoria", "fases[0].bancasRequeridas[0]"),
        ("Banca.IntegrantesAbaixoDoMinimo", "etapas[1].bancas[0]"),
        ("EtapaProcesso.RegraDeConsolidacaoNaoResolvida", "etapas[2].bancas[0]"),
    ];

    private static readonly string[] VermelhosDaImportacaoExterna =
    [
        "cronograma_recurso_com_banca_que_julga",
        "cronograma_instancia_superior_admitida_pelo_tipo_de_banca",
        "cronograma_instancia_superior_com_banca_da_segunda_instancia",
        "cronograma_banca_da_segunda_instancia_com_recurso_para_julgar",
        "cronograma_uma_banca_da_segunda_instancia_por_fase_ou_etapa",
        "cronograma_tipo_de_banca_com_o_minimo_de_bancas",
        "cronograma_homologacao_das_inscricoes_com_banca",
        "cronograma_importacao_externa_com_banca_de_importacao_de_candidatos",
        "cronograma_banca_com_regra_de_deliberacao",
        "cronograma_banca_com_o_minimo_de_integrantes",
    ];

    private static readonly string[] VermelhosDaInscricaoPropria =
    [
        "cronograma_tipo_de_banca_com_o_minimo_de_bancas",
        "cronograma_nota_do_enem_com_banca_de_importacao_das_notas",
        "cronograma_banca_com_regra_de_deliberacao",
    ];

    [Fact(DisplayName = "Os itens das bancas saem na ordem do painel, e ficam vermelhos só os das causas que a configuração dispara")]
    public void Itens_SaemNaOrdemDoPainel_EVermelhosSoOsDasCausas()
    {
        (string[] todos, string[] vermelhos) = ItensDe(ImportacaoExternaComTodasAsCausas());
        (string[] todosDaInscricaoPropria, string[] vermelhosDaInscricaoPropria) = ItensDe(InscricaoPropriaComANotaDoEnemSemBancaDeImportacao());

        todos.Should().Equal(ItensDoPainel);
        todosDaInscricaoPropria.Should().Equal(ItensDoPainel);
        vermelhos.Should().Equal(VermelhosDaImportacaoExterna);
        vermelhosDaInscricaoPropria.Should().Equal(VermelhosDaInscricaoPropria);
    }
}
