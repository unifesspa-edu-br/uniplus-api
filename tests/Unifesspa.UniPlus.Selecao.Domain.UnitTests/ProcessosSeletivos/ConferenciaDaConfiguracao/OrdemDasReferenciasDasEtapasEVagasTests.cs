namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ConferenciaDaConfiguracao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class OrdemDasReferenciasDasEtapasEVagasTests
{
    [Fact]
    public void De_VagasCascataEFada_DevolveCamposNaOrdemDosBlocos()
    {
        ConfiguracaoDeclarada configuracao = ConfiguracaoComVagas();
        ConfiguracaoDistribuicaoVagas oferta = configuracao.Vagas.DistribuicaoVagas[0];
        ConfiguracaoCascataRemanejamento cascata = configuracao.Vagas.Cascata!;
        ConfiguracaoDoFada fada = configuracao.Vagas.Fada!;
        (string Campo, ReferenciaRegra Regra)[] referencias = [.. ReferenciasDeRegraDaConfiguracao.De(configuracao)];

        referencias.Select(static item => item.Campo).Should().Equal(
            "distribuicaoVagas[0].regraDistribuicao", "distribuicaoVagas[0].regraAjuste", "ordemDeRemanejamento.regra",
            "classificacao.regraCalculo", "classificacao.regraOrdemAlocacao", "classificacao.criteriosDeDesempate[0].regra", "fada.regra");
        referencias[0].Regra.Should().Be(oferta.RegraDistribuicao);
        referencias[1].Regra.Should().Be(oferta.RegraAjuste);
        referencias[2].Regra.Should().Be(cascata.Regra);
        referencias[^1].Regra.Should().Be(fada.Regra);
    }

    [Fact]
    public void De_ConfiguracaoComTodosOsBlocos_DevolveOsBlocosNaOrdem()
    {
        ConfiguracaoDeclarada configuracao = ConfiguracaoComTodosOsBlocos();
        string[] campos = [.. ReferenciasDeRegraDaConfiguracao.De(configuracao).Select(static item => item.Campo)];

        ConferirOrdemDosBlocos(campos);
    }

    [Fact]
    public void Conferir_RegrasAusentesEmTodosOsBlocos_DevolveCadaCampoNaOrdem()
    {
        IReadOnlyList<(string Campo, ReferenciaRegra Regra)> referencias = ReferenciasDeRegraDaConfiguracao.De(ConfiguracaoComTodosOsBlocos());
        IReadOnlyList<FieldError> recusas = ConferenciaDasReferenciasDeRegra.Conferir(referencias, new Dictionary<(string, string), RegraDoCatalogoCitada>());

        ConferirOrdemDosBlocos([.. recusas.Select(static r => r.Field!)]);
        recusas.Should().OnlyContain(r => r.Error.Code == ProcessoSeletivoErrorCodes.RegraCitadaNaoCorrespondeAoCatalogo);
    }

    [Fact]
    public void Conferir_ReferenciasIntercaladasEmTodosOsBlocos_PreservaOcorrenciasEOrdemDeEntrada()
    {
        ConfiguracaoDeclarada configuracao = ConfiguracaoComOrdemDeEntradaDiferente();
        IReadOnlyList<(string Campo, ReferenciaRegra Regra)> referencias = ReferenciasDeRegraDaConfiguracao.De(configuracao);
        Dictionary<(string, string), RegraDoCatalogoCitada> catalogo = CatalogoIntercalado(referencias);

        IReadOnlyList<FieldError> recusas = ConferenciaDasReferenciasDeRegra.Conferir(referencias, catalogo);

        recusas.Select(static r => (r.Field!, r.Error.Code)).Should().Equal(
            CamposRecusados().Select(static campo => (campo, ProcessoSeletivoErrorCodes.RegraCitadaNaoCorrespondeAoCatalogo)));
        referencias[7].Regra.Should().Be(referencias[11].Regra);
    }

    [Fact]
    public void Avaliar_DuasEtapasComRegrasAusentes_PreservaCamposMensagensEOrdem()
    {
        ConfiguracaoDeclarada configuracao = ConfiguracaoComOrdemDeEntradaDiferente();
        CadastrosDaConfiguracao cadastros = Unifesspa.UniPlus.Testes.Compartilhado.CadastrosDeTeste.Padrao;
        FieldError[] causas = ConferenciaDasReferenciasDeRegra.Conferir(configuracao, cadastros)
            .Where(static erro => erro.Field!.StartsWith("etapas", StringComparison.Ordinal)).ToArray();
        ItemConformidade[] itens = ItensDasReferenciasDeRegra.Avaliar(configuracao, cadastros)
            .Where(static item => item.Dimensao == DimensaoConformidade.Etapas).ToArray();

        causas.Select(static erro => erro.Field).Should().Contain("etapas[1].regraConsolidacao").And.Contain("etapas[0].regraConsolidacao");
        itens.Select(static item => (item.Codigo, item.Mensagem)).Should().Equal(
            causas.Select(erro => ($"regra_citada_{erro.Field}", erro.Error.Message)));
        causas.Where(static erro => erro.Field!.EndsWith(".regraConsolidacao", StringComparison.Ordinal))
            .Select(static erro => erro.Error.Message).Should().Equal(
                $"A regra citada na 1ª etapa ({configuracao.Avaliacao.Etapas[1].Nome}), na regra de consolidação não corresponde à regra atual do catálogo. Cite a regra novamente.",
                $"A regra citada na 2ª etapa ({configuracao.Avaliacao.Etapas[0].Nome}), na regra de consolidação não corresponde à regra atual do catálogo. Cite a regra novamente.");
    }

    [Fact]
    public void Avaliar_NomeDaFaseAlteradoOuAusente_IdentificaSemAlterarARecusa()
    {
        ConfiguracaoDeclarada configuracao = ConfiguracaoComTodosOsBlocos();
        Guid id = configuracao.Cronograma.CronogramaFases[0].FaseCanonicaOrigemId;
        CadastrosDaConfiguracao comNome = CadastrosComNome(id, "Classificação");
        CadastrosDaConfiguracao renomeada = CadastrosComNome(id, "Seleção");
        CadastrosDaConfiguracao ausente = Unifesspa.UniPlus.Testes.Compartilhado.CadastrosDeTeste.Padrao;

        FieldError causa = ConferenciaDasReferenciasDeRegra.Conferir(configuracao, comNome).First(e => e.Field == "fases[0].regraRecurso.regraCodigo");
        FieldError alterada = ConferenciaDasReferenciasDeRegra.Conferir(configuracao, renomeada).First(e => e.Field == causa.Field);
        FieldError semNome = ConferenciaDasReferenciasDeRegra.Conferir(configuracao, ausente).First(e => e.Field == causa.Field);
        ConferirNomes(causa, alterada, semNome);
        ItensDasReferenciasDeRegra.Avaliar(configuracao, comNome).Single(i => i.Codigo == $"regra_citada_{causa.Field}").Mensagem.Should().Be(causa.Error.Message);
    }

    private static void ConferirNomes(FieldError causa, FieldError alterada, FieldError semNome)
    {
        causa.Error.Message.Should().Contain("na 1ª fase (Classificação), no recurso");
        alterada.Error.Message.Should().Contain("na 1ª fase (Seleção), no recurso");
        semNome.Error.Message.Should().Contain("na 1ª fase, no recurso");
        alterada.Error.Code.Should().Be(causa.Error.Code);
        semNome.Error.Code.Should().Be(causa.Error.Code);
    }

    [Fact]
    public void Avaliar_DuasFasesEmOrdemRecebidaDiferente_UsaMesmasMensagens()
    {
        ConfiguracaoDeclarada original = ConfiguracaoComTodosOsBlocos();
        RegraRecursoFase recurso = original.Cronograma.CronogramaFases[0].RegraRecurso!;
        FaseCronograma primeira = ProcessoConformeFactory.FaseConforme(recurso);
        FaseCronograma segunda = ProcessoConformeFactory.FaseConforme(recurso);
        ConfiguracaoDeclarada configuracao = new(original.Identidade,
            new CronogramaDeclarado(null, original.Cronograma.AlgoritmoContagemPrazo, [primeira, segunda]),
            original.Avaliacao, original.Vagas, original.Exigencias, original.Coleta, original.DivulgacaoETaxa)
        { Origem = new OrigemNoDocumento(new Dictionary<string, IReadOnlyList<int>> { ["fases"] = [1, 0] }, []) };
        CadastrosDaConfiguracao cadastros = CadastrosComNomes(primeira.FaseCanonicaOrigemId, segunda.FaseCanonicaOrigemId);
        FieldError[] causas = ConferenciaDasReferenciasDeRegra.Conferir(configuracao, cadastros)
            .Where(e => e.Field!.EndsWith("regraRecurso.regraCodigo", StringComparison.Ordinal)).ToArray();
        ConferirMensagensDasFases(configuracao, cadastros, causas);
    }

    private static void ConferirMensagensDasFases(ConfiguracaoDeclarada configuracao, CadastrosDaConfiguracao cadastros, FieldError[] causas)
    {
        causas.Select(e => e.Error.Message).Should().Equal(
            "A regra citada na 1ª fase (Classificação), no recurso, na regra de prazo não corresponde à regra atual do catálogo. Cite a regra novamente.",
            "A regra citada na 2ª fase (Inscrição), no recurso, na regra de prazo não corresponde à regra atual do catálogo. Cite a regra novamente.");
        ItemConformidade[] itens = [.. ItensDasReferenciasDeRegra.Avaliar(configuracao, cadastros)];
        causas.Should().OnlyContain(c => itens.Any(i => i.Codigo == $"regra_citada_{c.Field}" && i.Mensagem == c.Error.Message));
    }

    private static CadastrosDaConfiguracao CadastrosComNomes(Guid primeira, Guid segunda)
    {
        CadastrosDaConfiguracao padrao = Unifesspa.UniPlus.Testes.Compartilhado.CadastrosDeTeste.Padrao;
        return new(padrao.ContagemDePrazos, padrao.FatosDeModalidade, padrao.TiposDeAtoUnicos,
            padrao.Corretores, padrao.Protecao, padrao.MotivosEscolhidos)
        { NomesDasFases = new Dictionary<Guid, string> { [primeira] = "Inscrição", [segunda] = "Classificação" }, MunicipiosDoGeo = padrao.MunicipiosDoGeo, Precedencias = padrao.Precedencias };
    }

    private static CadastrosDaConfiguracao CadastrosComNome(Guid id, string nome)
    {
        CadastrosDaConfiguracao padrao = Unifesspa.UniPlus.Testes.Compartilhado.CadastrosDeTeste.Padrao;
        return new(padrao.ContagemDePrazos, padrao.FatosDeModalidade, padrao.TiposDeAtoUnicos,
            padrao.Corretores, padrao.Protecao, padrao.MotivosEscolhidos)
        { NomesDasFases = new Dictionary<Guid, string> { [id] = nome }, MunicipiosDoGeo = padrao.MunicipiosDoGeo, Precedencias = padrao.Precedencias };
    }

    private static ConfiguracaoDeclarada ConfiguracaoComOrdemDeEntradaDiferente()
    {
        ConfiguracaoDeclarada original = ConfiguracaoComTodosOsBlocos();
        return new ConfiguracaoDeclarada(original.Identidade, original.Cronograma, original.Avaliacao, original.Vagas,
            original.Exigencias, original.Coleta, original.DivulgacaoETaxa)
        {
            Origem = new OrigemNoDocumento(new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal)
            {
                ["etapas"] = [1, 0],
                ["etapas[1].recursos[0].procedimentosDeRecurso"] = [1, 0],
                ["classificacao.regrasEliminacao"] = [1, 0],
            }, []),
        };
    }

    private static Dictionary<(string, string), RegraDoCatalogoCitada> CatalogoIntercalado(
        IReadOnlyList<(string Campo, ReferenciaRegra Regra)> referencias)
    {
        Dictionary<(string, string), RegraDoCatalogoCitada> catalogo = [];
        AdicionarAoCatalogo(catalogo, referencias[0].Regra, divergente: true);
        AdicionarAoCatalogo(catalogo, referencias[2].Regra, divergente: false);
        AdicionarAoCatalogo(catalogo, referencias[3].Regra, divergente: true);
        AdicionarAoCatalogo(catalogo, referencias[7].Regra, divergente: true);
        return catalogo;
    }

    private static void AdicionarAoCatalogo(Dictionary<(string, string), RegraDoCatalogoCitada> catalogo,
        ReferenciaRegra referencia, bool divergente)
    {
        ReferenciaRegra atual = divergente
            ? ReferenciaRegra.De(referencia.Codigo, referencia.Versao, new string('f', 64))
            : referencia;
        catalogo[(referencia.Codigo, referencia.Versao)] = new(TipoRegra.RegraCalculo, atual);
    }

    private static string[] CamposRecusados() =>
    [
        "identificacao.algoritmoContagemPrazo", "distribuicaoVagas[0].regraDistribuicao",
        "ordemDeRemanejamento.regra", "fases[0].regraRecurso.regraCodigo",
        "fases[0].regraRecurso.procedimentos[0].regra", "fases[0].regraRecurso.procedimentos[1].regra",
        "etapas[1].regraConsolidacao", "etapas[1].recursos[0].regraCodigo",
        "etapas[1].recursos[0].procedimentosDeRecurso[1].regra", "etapas[1].recursos[0].procedimentosDeRecurso[0].regra",
        "etapas[0].regraConsolidacao", "etapas[0].recursos[0].regraCodigo",
        "etapas[0].recursos[0].procedimentosDeRecurso[0].regra", "etapas[0].recursos[0].procedimentosDeRecurso[1].regra",
        "classificacao.regraCalculo", "classificacao.regraArredondamento", "classificacao.regraOrdemAlocacao",
        "classificacao.regrasEliminacao[1].regra", "classificacao.regrasEliminacao[0].regra",
        "classificacao.criteriosDeDesempate[0].regra", "fada.regra",
    ];

    private static ConfiguracaoDeclarada ConfiguracaoComTodosOsBlocos()
    {
        ConfiguracaoDeclarada vagas = ConfiguracaoComVagas();
        ConfiguracaoDeclarada etapas = ConfiguracaoComEtapas();
        RegraRecursoFase prazo = ModeloDeFase.RecursoAncoradoEm(ProcessoConformeFactory.ProdutoPreliminarId);
        RegraRecursoFase recurso = RegraRecursoFase.Criar(prazo.Regra, ParametrosDoRecursoDeTeste.DeclaradosDe(prazo.Parametros), prazo.ProdutoAncoraId,
            OrdemDasReferenciasDeRegraTests.Procedimentos()).Value!;
        FaseCronograma fase = ProcessoConformeFactory.FaseConforme(recurso);
        CronogramaDeclarado cronograma = new(null, ProcessoConformeFactory.Regra("CONTAGEM", "a"), [fase]);
        ConfiguracaoClassificacao classificacao = OrdemDasReferenciasDaClassificacaoTests.Classificacao(
            OrdemDasReferenciasDaClassificacaoTests.Eliminacao('a'), OrdemDasReferenciasDaClassificacaoTests.Eliminacao('b'));
        AvaliacaoDeclarada avaliacao = new(etapas.Avaliacao.Etapas, vagas.Avaliacao.CriteriosDesempate, classificacao);
        return new ConfiguracaoDeclarada(vagas.Identidade, cronograma, avaliacao, vagas.Vagas,
            vagas.Exigencias, vagas.Coleta, vagas.DivulgacaoETaxa);
    }

    private static void ConferirOrdemDosBlocos(string[] campos) =>
        campos.Should().Equal("identificacao.algoritmoContagemPrazo", "distribuicaoVagas[0].regraDistribuicao",
            "distribuicaoVagas[0].regraAjuste", "ordemDeRemanejamento.regra", "fases[0].regraRecurso.regraCodigo",
            "fases[0].regraRecurso.procedimentos[0].regra", "fases[0].regraRecurso.procedimentos[1].regra",
            "etapas[0].regraConsolidacao", "etapas[0].recursos[0].regraCodigo",
            "etapas[0].recursos[0].procedimentosDeRecurso[0].regra", "etapas[0].recursos[0].procedimentosDeRecurso[1].regra",
            "etapas[1].regraConsolidacao", "etapas[1].recursos[0].regraCodigo",
            "etapas[1].recursos[0].procedimentosDeRecurso[0].regra", "etapas[1].recursos[0].procedimentosDeRecurso[1].regra",
            "classificacao.regraCalculo", "classificacao.regraArredondamento", "classificacao.regraOrdemAlocacao",
            "classificacao.regrasEliminacao[0].regra", "classificacao.regrasEliminacao[1].regra",
            "classificacao.criteriosDeDesempate[0].regra", "fada.regra");

    private static ConfiguracaoDeclarada ConfiguracaoComVagas()
    {
        ConfiguracaoDeclarada original = ProcessoConformeFactory.Criar().Declaracao();
        ConfiguracaoDistribuicaoVagas oferta = ApoioDasCotasFederaisNaConformidade.OfertaFederalCompleta();
        ConfiguracaoCascataRemanejamento cascata = ConfiguracaoCascataRemanejamento.Criar(
            ProcessoConformeFactory.Regra(CriterioDeRemanejamento.OrdemDaLei12711.Codigo(), "a"), "AC",
            [DestinoRemanejamento.Criar("LB_PPI", 1, "LB_Q").Value!]).Value!;
        ConfiguracaoDoFada fada = ConfiguracaoDoFada.Criar(
            ProcessoConformeFactory.Regra(RegraDoFada.Multiplicativo.Codigo(), "b"), 1.2m, null, null,
            new BaseLegalCongelada(Guid.CreateVersion7(), "PORTARIA", "PORTARIA", "Portaria", "Inclusão regional", [("1504208", "Marabá", "PA")])).Value!;
        VagasDeclaradas vagas = new([oferta], null, true, fada, cascata, null);
        return new ConfiguracaoDeclarada(original.Identidade, original.Cronograma, original.Avaliacao, vagas,
            original.Exigencias, original.Coleta, original.DivulgacaoETaxa);
    }

    [Fact]
    public void De_EtapasEProcedimentosForaDaOrdemDeEntrada_PreservaCamposEPosicoes()
    {
        ConfiguracaoDeclarada configuracao = ConfiguracaoComEtapas();
        (string Campo, ReferenciaRegra Regra)[] referencias = [.. ReferenciasDeRegraDaConfiguracao.De(configuracao)
            .Where(static item => item.Campo.StartsWith("etapas", StringComparison.Ordinal))];

        ConferirCamposDasEtapas(referencias);
        referencias[0].Regra.Should().Be(configuracao.Avaliacao.Etapas[1].RegraConsolidacao);
        referencias[2].Regra.Should().Be(configuracao.Avaliacao.Etapas[1].Recursos.Single().Procedimentos.Itens[1].Regra);
        ConferirPonteirosDasEtapas(configuracao, referencias);
    }

    private static void ConferirCamposDasEtapas((string Campo, ReferenciaRegra Regra)[] referencias)
    {
        referencias.Select(static item => item.Campo).Should().Equal(
            "etapas[1].regraConsolidacao", "etapas[1].recursos[0].regraCodigo",
            "etapas[1].recursos[0].procedimentosDeRecurso[1].regra", "etapas[1].recursos[0].procedimentosDeRecurso[0].regra",
            "etapas[0].regraConsolidacao", "etapas[0].recursos[0].regraCodigo",
            "etapas[0].recursos[0].procedimentosDeRecurso[0].regra", "etapas[0].recursos[0].procedimentosDeRecurso[1].regra");
    }

    private static void ConferirPonteirosDasEtapas(ConfiguracaoDeclarada configuracao, (string Campo, ReferenciaRegra Regra)[] referencias)
    {
        referencias.Select(item => PonteiroNoDocumento.Converter(item.Campo, configuracao.Origem)).Should().Equal(
            "/etapas/0/regraDeApuracaoDaAvaliacao", "/etapas/0/recursos/0/regraCodigo",
            "/etapas/0/recursos/0/procedimentosDeRecurso/0/regra", "/etapas/0/recursos/0/procedimentosDeRecurso/1/regra",
            "/etapas/1/regraDeApuracaoDaAvaliacao", "/etapas/1/recursos/0/regraCodigo",
            "/etapas/1/recursos/0/procedimentosDeRecurso/0/regra", "/etapas/1/recursos/0/procedimentosDeRecurso/1/regra");
    }

    private static ConfiguracaoDeclarada ConfiguracaoComEtapas()
    {
        ConfiguracaoDeclarada original = ProcessoConformeFactory.Criar().Declaracao();
        AvaliacaoDeclarada avaliacao = new([Etapa(), Etapa()], [], null);
        return new ConfiguracaoDeclarada(original.Identidade, original.Cronograma, avaliacao, original.Vagas,
            original.Exigencias, original.Coleta, original.DivulgacaoETaxa)
        {
            Origem = new OrigemNoDocumento(new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal)
            {
                ["etapas"] = [1, 0],
                ["etapas[1].recursos[0].procedimentosDeRecurso"] = [1, 0],
            }, []),
        };
    }

    private static EtapaProcesso Etapa()
    {
        EtapaProcesso etapa = EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
            new IdentificacaoDaEtapa("Prova", CaraterEtapa.Classificatoria,
                TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA", "Prova", true, true, false).Value!, 1, null),
            new DeclaracaoDaAvaliacaoDaEtapa(1m, null, 10m, null, FormaDeAvaliacao.Humana,
                ProcessoConformeFactory.Regra(RegraDeApuracaoDaAvaliacao.AvaliacaoUnica.Codigo(), "a"), null)).Value!;
        etapa.DefinirProdutos([ProdutoDaEtapa.Criar("RESULTADO_PROVA", PapelDaPublicacaoPrevista.Preliminar)]).IsSuccess.Should().BeTrue();
        etapa.DefinirRecursos([Recurso(etapa.Produtos.Single().Id)]).IsSuccess.Should().BeTrue();
        return etapa;
    }

    private static RecursoDaEtapa Recurso(Guid produtoId)
    {
        RegraRecursoFase prazo = ModeloDeFase.RecursoAncoradoEm(ProcessoConformeFactory.ProdutoPreliminarId);
        ProcedimentosDeRecurso procedimentos = ProcedimentosDeRecurso.De([
            ProcedimentoDeRecursoDeclarado.De(TipoMotivoDeDecisao.Classificacao, null, ProcessoConformeFactory.Regra("PRIMEIRA", "a")),
            ProcedimentoDeRecursoDeclarado.De(TipoMotivoDeDecisao.IsencaoDaTaxa, null, ProcessoConformeFactory.Regra("SEGUNDA", "b")),
        ]);
        return RecursoDaEtapa.Criar(EventoInicialDoPrazo.PublicacaoDoAto, prazo.Regra, ParametrosDoRecursoDeTeste.DeclaradosDe(prazo.Parametros), produtoId, procedimentos).Value!;
    }
}
