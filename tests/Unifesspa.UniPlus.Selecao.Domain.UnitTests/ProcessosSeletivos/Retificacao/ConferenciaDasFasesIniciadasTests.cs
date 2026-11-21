namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Retificacao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Contratos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class ConferenciaDasFasesIniciadasTests
{
    private const string CodigoDaTroca = "FaseCronograma.UnidadeDeFaseIniciadaNaoMuda";
    private const string CodigoDaRemocaoDaFase = "FaseCronograma.FaseIniciadaNaoSaiDoCronograma";
    private const string CodigoDaRemocaoDaEtapa = "FaseCronograma.EtapaIniciadaNaoSaiDoCronograma";

    [Fact(DisplayName = "A unidade trocada numa fase iniciada é recusada, citando a fase, com a unidade dela como caminho")]
    public void Conferir_UnidadeTrocadaEmFaseIniciada_Recusa()
    {
        FaseCronograma habilitacao = Fase(1, "HABILITACAO");

        IReadOnlyList<FieldError> recusas = Conferencia(Iniciadas(habilitacao), [ComUnidade(habilitacao, UnidadesDeTeste.Cga)]);

        FieldError recusa = recusas.Should().ContainSingle().Subject;
        recusa.Field.Should().Be("fases[0].unidadeResponsavel");
        PonteiroNoDocumento.Converter(recusa.Field, OrigemNoDocumento.Identidade).Should().Be("/cronograma/fases/0/unidadeResponsavelCodigo");
        recusa.Error.Code.Should().Be(CodigoDaTroca);
        recusa.Error.Message.Should().Contain("HABILITACAO").And.Contain("ordem 1");
    }

    [Fact(DisplayName = "A fase iniciada mantida com a mesma unidade não é recusada")]
    public void Conferir_MesmaUnidadeEmFaseIniciada_NaoRecusa()
    {
        FaseCronograma habilitacao = Fase(1, "HABILITACAO");

        Conferencia(Iniciadas(habilitacao), [ComUnidade(habilitacao, UnidadesDeTeste.Ceps)]).Should().BeEmpty();
    }

    [Fact(DisplayName = "A unidade trocada numa fase que não começou não é recusada")]
    public void Conferir_UnidadeTrocadaEmFaseNaoIniciada_NaoRecusa()
    {
        FaseCronograma habilitacao = Fase(1, "HABILITACAO");

        Conferencia(FasesIniciadas.Nenhuma, [ComUnidade(habilitacao, UnidadesDeTeste.Cga)]).Should().BeEmpty();
    }

    [Fact(DisplayName = "Duas fases com a unidade trocada geram duas recusas, cada uma com a unidade da própria fase como caminho")]
    public void Conferir_DuasUnidadesTrocadas_AcumulaDuasRecusas()
    {
        FaseCronograma habilitacao = Fase(1, "HABILITACAO");
        FaseCronograma classificacao = Fase(2, "CLASSIFICACAO");

        IReadOnlyList<FieldError> recusas = Conferencia(
            Iniciadas(habilitacao, classificacao),
            [ComUnidade(habilitacao, UnidadesDeTeste.Cga), ComUnidade(classificacao, UnidadesDeTeste.Cga)]);

        recusas.Select(static r => r.Field).Should().Equal("fases[0].unidadeResponsavel", "fases[1].unidadeResponsavel");
        recusas.Select(static r => r.Error.Code).Should().AllBe(CodigoDaTroca);
    }

    [Fact(DisplayName = "A fase trocada é apontada pela posição no documento, na ordem canônica, e não pela posição na lista como veio")]
    public void Conferir_FasesForaDeOrdem_ApontaAPosicaoNoDocumento()
    {
        FaseCronograma habilitacao = Fase(1, "HABILITACAO");
        FaseCronograma classificacao = Fase(2, "CLASSIFICACAO");
        FaseCronograma resultado = Fase(3, "RESULTADO");

        IReadOnlyList<FieldError> recusas = Conferencia(
            Iniciadas(habilitacao, classificacao, resultado),
            [ComUnidade(resultado, UnidadesDeTeste.Cga), habilitacao, ComUnidade(classificacao, UnidadesDeTeste.Cga)]);

        recusas.Select(static r => r.Error.Message).Should().SatisfyRespectively(
            static primeira => primeira.Should().Contain("CLASSIFICACAO"), static segunda => segunda.Should().Contain("RESULTADO"));
        recusas.Select(static r => PonteiroNoDocumento.Converter(r.Field, OrigemNoDocumento.Identidade)).Should().Equal(
            "/cronograma/fases/1/unidadeResponsavelCodigo", "/cronograma/fases/2/unidadeResponsavelCodigo");
    }

    [Fact(DisplayName = "Remover do cronograma uma fase iniciada é recusado, citando a fase, com a coleção das fases como caminho")]
    public void Conferir_FaseIniciadaRemovida_Recusa()
    {
        (FaseCronograma habilitacao, FaseCronograma classificacao) = DuasFases();

        IReadOnlyList<FieldError> recusas = Conferencia(Iniciadas(habilitacao), [classificacao]);

        FieldError recusa = recusas.Should().ContainSingle().Subject;
        recusa.Field.Should().Be("cronograma.fases");
        PonteiroNoDocumento.Converter(recusa.Field, OrigemNoDocumento.Identidade).Should().Be("/cronograma/fases");
        recusa.Error.Code.Should().Be(CodigoDaRemocaoDaFase);
        recusa.Error.Message.Should().Contain("HABILITACAO");
    }

    [Fact(DisplayName = "Retirar a fase iniciada e recolocá-la na mesma retificação não é remoção")]
    public void Conferir_FaseIniciadaRecolocada_NaoRecusa()
    {
        (FaseCronograma habilitacao, FaseCronograma classificacao) = DuasFases();
        FasesIniciadas iniciadas = Iniciadas(habilitacao);

        Conferencia(iniciadas, [classificacao]).Should().ContainSingle();
        Conferencia(iniciadas, [classificacao, habilitacao]).Should().BeEmpty();
    }

    [Fact(DisplayName = "Recolocar a fase iniciada com outra unidade cai na recusa da troca, e não na da remoção")]
    public void Conferir_FaseIniciadaRecolocadaComOutraUnidade_RecusaATroca()
    {
        (FaseCronograma habilitacao, FaseCronograma classificacao) = DuasFases();

        IReadOnlyList<FieldError> recusas = Conferencia(
            Iniciadas(habilitacao), [classificacao, ComUnidade(habilitacao, UnidadesDeTeste.Cga)]);

        recusas.Should().ContainSingle().Which.Error.Code.Should().Be(CodigoDaTroca);
    }

    [Fact(DisplayName = "A fase iniciada que a versão vigente já não traz não é recusada como removida")]
    public void Conferir_FaseIniciadaForaDaVersaoVigente_NaoRecusa()
    {
        (FaseCronograma habilitacao, FaseCronograma classificacao) = DuasFases();
        FasesIniciadas iniciadas = new(
            [KeyValuePair.Create<Guid, Guid?>(habilitacao.FaseCanonicaOrigemId, UnidadesDeTeste.Ceps.OrigemId)]);

        Conferencia(iniciadas, [classificacao]).Should().BeEmpty();
    }

    [Fact(DisplayName = "Remover uma etapa iniciada é recusado, citando a etapa, com a coleção das etapas como caminho")]
    public void Conferir_EtapaIniciadaRemovida_Recusa()
    {
        (EtapaProcesso prova, EtapaProcesso heteroidentificacao, EtapaProcesso analise) = TresEtapas();

        IReadOnlyList<FieldError> recusas = Conferencia(IniciadasComEtapa(heteroidentificacao), etapas: [prova, analise]);

        FieldError recusa = recusas.Should().ContainSingle().Subject;
        recusa.Field.Should().Be("etapas");
        PonteiroNoDocumento.Converter(recusa.Field, OrigemNoDocumento.Identidade).Should().Be("/etapas");
        recusa.Error.Code.Should().Be(CodigoDaRemocaoDaEtapa);
        recusa.Error.Message.Should().Contain("Heteroidentificação");
    }

    [Fact(DisplayName = "Remover uma etapa que ainda não começou é aceito")]
    public void Conferir_EtapaNaoIniciada_NaoRecusa()
    {
        (EtapaProcesso prova, EtapaProcesso heteroidentificacao, _) = TresEtapas();

        Conferencia(FasesIniciadas.Nenhuma, etapas: [prova, heteroidentificacao]).Should().BeEmpty();
    }

    [Fact(DisplayName = "A etapa irmã de uma etapa iniciada sai, porque a abertura de uma etapa não inicia as outras da fase")]
    public void Conferir_IrmaDeEtapaIniciada_NaoRecusa()
    {
        (EtapaProcesso prova, EtapaProcesso heteroidentificacao, _) = TresEtapas();

        Conferencia(IniciadasComEtapa(heteroidentificacao), etapas: [prova, heteroidentificacao]).Should().BeEmpty();
    }

    [Fact(DisplayName = "Remover e recolocar a etapa iniciada na mesma retificação não é remoção")]
    public void Conferir_EtapaIniciadaRecolocada_NaoRecusa()
    {
        (EtapaProcesso prova, EtapaProcesso heteroidentificacao, EtapaProcesso analise) = TresEtapas();
        FasesIniciadas iniciadas = IniciadasComEtapa(heteroidentificacao);

        Conferencia(iniciadas, etapas: [prova, analise]).Should().ContainSingle();
        Conferencia(iniciadas, etapas: [prova, analise, heteroidentificacao]).Should().BeEmpty();
    }

    [Fact(DisplayName = "A etapa iniciada que a versão vigente já não traz não é recusada como removida")]
    public void Conferir_EtapaIniciadaForaDaVersaoVigente_NaoRecusa()
    {
        (EtapaProcesso prova, EtapaProcesso heteroidentificacao, _) = TresEtapas();
        FasesIniciadas iniciadas = new([], etapas: [new EtapaIniciada(heteroidentificacao.Id, heteroidentificacao.Nome, NaVersaoVigente: false)]);

        Conferencia(iniciadas, etapas: [prova]).Should().BeEmpty();
        iniciadas.ContemEtapa(heteroidentificacao.Id).Should().BeTrue();
        FasesIniciadas.Nenhuma.ContemEtapa(heteroidentificacao.Id).Should().BeFalse();
    }

    [Fact(DisplayName = "Unidade trocada, fase removida e etapa removida geram três recusas, nessa ordem")]
    public void Conferir_TrocaERemocoes_AcumulaTresRecusas()
    {
        (FaseCronograma habilitacao, FaseCronograma classificacao) = DuasFases();
        (EtapaProcesso prova, EtapaProcesso heteroidentificacao, _) = TresEtapas();
        FasesIniciadas iniciadas = new(
            [KeyValuePair.Create<Guid, Guid?>(classificacao.FaseCanonicaOrigemId, UnidadesDeTeste.Ceps.OrigemId)],
            [KeyValuePair.Create(habilitacao.FaseCanonicaOrigemId, habilitacao.Codigo)],
            [new EtapaIniciada(heteroidentificacao.Id, heteroidentificacao.Nome, NaVersaoVigente: true)]);

        IReadOnlyList<FieldError> recusas = Conferencia(iniciadas, [ComUnidade(classificacao, UnidadesDeTeste.Cga)], [prova]);

        recusas.Select(static r => r.Error.Code).Should().Equal(CodigoDaTroca, CodigoDaRemocaoDaFase, CodigoDaRemocaoDaEtapa);
        recusas.Select(static r => r.Field).Should().Equal("fases[0].unidadeResponsavel", "cronograma.fases", "etapas");
        PonteiroNoDocumento.Converter(recusas, OrigemNoDocumento.Identidade).Select(static r => r.Field!)
            .Where(ponteiro => CaminhosDoDocumentoNoEsquema.Publicado.PrimeiroSegmentoAusente(ponteiro) is not null)
            .Should().BeEmpty("o ponteiro de cada recusa do fechamento existe no esquema publicado");
    }

    [Fact(DisplayName = "Sem fase nem etapa iniciada, nada é recusado")]
    public void Conferir_NenhumaIniciada_ListaVazia()
    {
        (FaseCronograma habilitacao, _) = DuasFases();
        (EtapaProcesso prova, _, _) = TresEtapas();

        Conferencia(FasesIniciadas.Nenhuma, [ComUnidade(habilitacao, UnidadesDeTeste.Cga)], [prova]).Should().BeEmpty();
    }

    [Fact(DisplayName = "O conjunto de fases iniciadas responde só pelas fases que recebeu")]
    public void FasesIniciadas_ContemSoAsRecebidas()
    {
        Guid iniciada = Guid.CreateVersion7();
        FasesIniciadas conjunto = new([KeyValuePair.Create<Guid, Guid?>(iniciada, null)]);

        conjunto.Contem(iniciada).Should().BeTrue();
        conjunto.Contem(Guid.CreateVersion7()).Should().BeFalse();
        FasesIniciadas.Nenhuma.Contem(iniciada).Should().BeFalse();
    }

    private static IReadOnlyList<FieldError> Conferencia(
        FasesIniciadas iniciadas, FaseCronograma[]? fases = null, EtapaProcesso[]? etapas = null) =>
        ConferenciaDasFasesIniciadas.Conferir(iniciadas, fases ?? [], etapas ?? []);

    private static FasesIniciadas Iniciadas(params FaseCronograma[] fases) => new(
        [.. fases.Select(static f => KeyValuePair.Create<Guid, Guid?>(f.FaseCanonicaOrigemId, UnidadesDeTeste.Ceps.OrigemId))],
        [.. fases.Select(static f => KeyValuePair.Create(f.FaseCanonicaOrigemId, f.Codigo))]);

    private static FasesIniciadas IniciadasComEtapa(EtapaProcesso etapa) =>
        new([], etapas: [new EtapaIniciada(etapa.Id, etapa.Nome, NaVersaoVigente: true)]);

    private static (FaseCronograma Habilitacao, FaseCronograma Classificacao) DuasFases() =>
        (Fase(1, "HABILITACAO"), Fase(2, "CLASSIFICACAO"));

    private static (EtapaProcesso Prova, EtapaProcesso Heteroidentificacao, EtapaProcesso Analise) TresEtapas() =>
        (Etapa("Prova", CaraterEtapa.Classificatoria, 1m), Etapa("Heteroidentificação", CaraterEtapa.Eliminatoria, null),
            Etapa("Análise documental", CaraterEtapa.Eliminatoria, null));

    private static FaseCronograma Fase(int ordem, string codigo) =>
        FaseCronograma.Criar(
            new OrigemCanonicaDaFase(
                Guid.CreateVersion7(), codigo, OrigemDataFase.NoEdital, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(
                ordem, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
            bancasRequeridas: []).Value!;

    private static FaseCronograma ComUnidade(FaseCronograma atual, UnidadeResponsavelSnapshot unidade) =>
        FaseCronograma.Criar(
            new OrigemCanonicaDaFase(
                atual.FaseCanonicaOrigemId, atual.Codigo, atual.OrigemData, atual.AgrupaEtapas, atual.PermiteComplementacao, atual.ColetaInscricao, atual.ColetaSolicitacaoIsencao),
            new DecisoesDeCronogramaDaFase(atual.Ordem, atual.Inicio, atual.Fim, unidade),
            new PublicacoesDaFase([.. atual.Produtos], atual.EmiteParecerIndividual, atual.RegraRecurso),
            [.. atual.BancasRequeridas]).Value!;

    private static EtapaProcesso Etapa(string nome, CaraterEtapa carater, decimal? peso) =>
        EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(Nome: nome, Carater: carater, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: null, FaseCodigo: "RESULTADO_FINAL"),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: peso, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
}
