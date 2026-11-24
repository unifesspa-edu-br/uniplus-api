namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDeRecusaNoCongelamento;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.PublicacaoDeTeste;

/// <summary>
/// A forma de cada recusa, por etapa: a que traz um erro único e sem campo, a que acumula o lote inteiro na ordem em
/// que a conferência o produziu, a da conferência do estado e a que acontece depois de o agregado transitar.
/// </summary>
public sealed class FormaDasRecusasAoCongelarTests
{
    public static TheoryData<DefeitoDoCongelamento> RecusasDeErroUnico() =>
    [
        DefeitoDoCongelamento.Documento,
        DefeitoDoCongelamento.TipoDeAto,
        DefeitoDoCongelamento.TiposDeAtoCongelados,
        DefeitoDoCongelamento.Anexos,
        DefeitoDoCongelamento.ConformidadeLegal,
    ];

    [Theory(DisplayName = "A recusa de uma conferência de erro único chega sem campo e sem eventos")]
    [MemberData(nameof(RecusasDeErroUnico))]
    public async Task ConferenciaDeErroUnico_ChegaSemCampo(DefeitoDoCongelamento defeito)
    {
        (Result resposta, IEnumerable<object> eventos) = await Para(OperacaoQueCongela.Publicar).Com(defeito).ExecutarAsync();

        resposta.Errors.Should().ContainSingle().Which.Field.Should().BeNull();
        eventos.Should().BeEmpty();
    }

    [Fact(DisplayName = "O ciclo recursal aberto chega com os erros que a conferência produziu, sem perder nenhum")]
    public async Task CicloRecursal_ChegaComOsErrosDaConferencia()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado();
        cenario.ComPreliminarPublicadoEDefinitivoPendente();
        VersaoConfiguracao versao = (await cenario.Servicos.Repositorio.ObterVersaoAtualAsync(cenario.Processo.Id, CancellationToken.None))!;
        Result esperado = await ConferenciaDoCicloRecursalAberto.ConferirAsync(
            cenario.Servicos.Atos, cenario.Servicos.Codecs, cenario.Processo, versao, CancellationToken.None);

        (Result resposta, IEnumerable<object> eventos) = await cenario.RetificarAsync();

        esperado.IsFailure.Should().BeTrue();
        resposta.Errors.Should().Equal(esperado.Errors);
        eventos.Should().BeEmpty();
    }

    [Fact(DisplayName = "A recópia chega com o lote inteiro de erros, os itens antes das unidades")]
    public async Task Recopia_ChegaComOLoteInteiro()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Preparo");
        ServicosDoCongelamentoDeTeste servicos = ServicosComORgUfPorMunicipio(processo);
        Result<RecopiadoAoCongelar> esperado = await RecopiaEsperadaAsync(servicos, processo);

        (Result resposta, IEnumerable<object> eventos) = await servicos.PublicarAsync(ComandoDePublicacao(processo));

        esperado.Errors.Should().HaveCountGreaterThan(1);
        resposta.Errors.Should().BeEquivalentTo(
            PonteiroNoDocumento.Converter(IdentificacaoDosItensDaCausa.Identificar(esperado.Errors, processo.Declaracao()), OrigemNoDocumento.Identidade),
            static opcoes => opcoes.WithStrictOrdering());
        resposta.Errors.Single(static e => e.Error.Code == "FaseCronograma.UnidadeResponsavelNaoEncontrada").Identificadores.Should().Equal(
            new IdentificadorDoItem("fases", processo.CronogramaFases.Single().Codigo));
        eventos.Should().BeEmpty();
    }

    [Fact(DisplayName = "A proteção pendente do tipo e do fato chega com os dois erros, nessa ordem")]
    public async Task Protecao_ChegaComOsDoisErrosNaOrdem()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado();
        cenario.ComTipoDaExigenciaPendente();
        await cenario.ComFatoCitadoPendenteAsync();

        (Result resposta, _) = await cenario.RetificarAsync();

        resposta.Errors.Select(static e => e.Error.Code).Should().Equal(
            "ProcessoSeletivo.TipoDocumentoComProtecaoPendente", "ProcessoSeletivo.FatoComProtecaoPendente");
    }

    [Fact(DisplayName = "A recusa da conferência da configuração chega com o campo do documento em que se corrige")]
    public async Task RecusaDaConferencia_ChegaComOCampoDoDocumento()
    {
        (Result resposta, _) = await Para(OperacaoQueCongela.Publicar).Com(DefeitoDoCongelamento.Cronograma).ExecutarAsync();

        resposta.Errors.Should().NotBeEmpty().And.OnlyContain(static e => e.Field != null);
    }

    [Fact(DisplayName = "A causa da declaração da etapa chega no ponteiro do documento, e nenhuma causa chega em notação de pontos e colchetes")]
    public async Task CausaDaDeclaracaoDaEtapa_ChegaNoPonteiroDoDocumento()
    {
        CenarioDeRecusaNoCongelamento cenario = Para(OperacaoQueCongela.Publicar);
        EtapaProcesso etapa = EtapaQuePublicaPreliminarSemDefinitivo(cenario.Processo);

        (Result resposta, _) = await cenario.ExecutarAsync();

        etapa.Produtos.Should().ContainSingle();
        resposta.Errors.Select(static e => e.Field).Should().Contain("/etapas/0/publicacoesPrevistas");
        resposta.Errors.Select(static e => e.Field).OfType<string>().Should().OnlyContain(static campo => campo.Length == 0 || campo[0] == '/');
    }

    [Fact(DisplayName = "A causa em uma banca da etapa chega com o identificador da etapa e o da banca na posição canônica, e a gravação do estado não as acrescenta ao campo")]
    public async Task CausaDaBancaDaEtapa_ChegaComOsIdentificadoresDaEtapaEDaBanca()
    {
        CenarioDeRecusaNoCongelamento cenario = Para(OperacaoQueCongela.Publicar);
        EtapaProcesso etapa = EtapaComBancasSemRegra(cenario.Processo);
        BancaDaEtapa[] naOrdemCanonica = [.. etapa.Bancas];

        (Result resposta, _) = await cenario.ExecutarAsync();

        FieldError[] daBanca = [.. resposta.Errors.Where(static e => e.Error.Code == BancaErrorCodes.RegraDeDeliberacaoObrigatoria)];
        daBanca.Select(static e => e.Field).Should().Equal("/etapas/0/bancas/0", "/etapas/0/bancas/1");
        for (int posicao = 0; posicao < daBanca.Length; posicao++)
        {
            daBanca[posicao].Identificadores.Should().Equal(
                new IdentificadorDoItem("etapas", etapa.Id.ToString("D")), new IdentificadorDoItem("bancas", naOrdemCanonica[posicao].Id.ToString("D")));
        }
    }

    // A banca de heteroidentificação chega antes da de análise documental e fica depois dela na ordem canônica; nenhuma declara a regra de deliberação.
    private static EtapaProcesso EtapaComBancasSemRegra(ProcessoSeletivo processo)
    {
        EtapaProcesso etapa = EtapaQuePublicaPreliminarSemDefinitivo(processo);
        etapa.DefinirBancas(
        [
            BancaDaEtapa.Criar(ChaveLocal.Criar("banca").Value, TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.Heteroidentificacao, Guid.CreateVersion7())),
            BancaDaEtapa.Criar(ChaveLocal.Criar("banca").Value, TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.AnaliseDocumental, Guid.CreateVersion7())),
        ]);

        return etapa;
    }

    // O conjunto de etapas recusa o preliminar sem definitivo ao ser gravado; o estado violado chega pelo produto declarado depois.
    private static EtapaProcesso EtapaQuePublicaPreliminarSemDefinitivo(ProcessoSeletivo processo)
    {
        EtapaProcesso etapa = EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
            new IdentificacaoDaEtapa(
                "Prova Objetiva", CaraterEtapa.Classificatoria,
                TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!,
                Ordem: 1, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(
                Peso: 3m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
        processo.DefinirEtapas([etapa]).IsSuccess.Should().BeTrue();
        etapa.DefinirProdutos([ProdutoDaEtapa.Criar("RESULTADO_PROVA", PapelDaPublicacaoPrevista.Preliminar)]).IsSuccess.Should().BeTrue();

        return etapa;
    }

    [Fact(DisplayName = "O estado recusado devolve erro único e sem eventos, antes de ler o ato, o documento e os cadastros")]
    public async Task EstadoRecusado_NaoLeOAtoNemOsCadastros()
    {
        DependenciasDosHandlersQueCongelam cenario = DependenciasDosHandlersQueCongelam.Para(NovoProcessoConforme());

        (Result resposta, IEnumerable<object> eventos) = await cenario.RetificarAsync();

        resposta.Errors.Should().ContainSingle().Which.Field.Should().BeNull();
        resposta.Error!.Code.Should().Be("ProcessoSeletivo.TransicaoInvalida");
        eventos.Should().BeEmpty();
        await cenario.Servicos.Documentos.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
        await cenario.Servicos.TiposDeAto.DidNotReceiveWithAnyArgs().ObterVigenteAsync(default!, default, default);
        _ = cenario.Servicos.Canonicalizer.DidNotReceive().Canonicalizar(Arg.Any<EntradaCanonicalizacao>());
        await cenario.Servicos.Repositorio.DidNotReceive().AdicionarVersaoConfiguracaoAsync(Arg.Any<VersaoConfiguracao>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A unicidade violada no flush chega como erro único, sem eventos, e o rastreamento com a remoção do rascunho é descartado")]
    public async Task UnicidadeNoFlush_ChegaComoErroUnico()
    {
        DependenciasDosHandlersQueCongelam publicacao = DependenciasDosHandlersQueCongelam.Para(NovoProcessoConforme());
        publicacao.Servicos.UnitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(ExcecaoDeBancoDeTeste.ViolacaoDeUnicidade("ux_versoes_configuracao_processo_numero"));

        (Result resposta, IEnumerable<object> eventos) = await publicacao.PublicarAsync();

        resposta.Errors.Should().ContainSingle().Which.Field.Should().BeNull();
        resposta.Error!.Code.Should().Be("VersaoConfiguracao.NumeroDuplicado");
        eventos.Should().BeEmpty();
        publicacao.Servicos.UnitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    private static PublicarProcessoSeletivoCommand ComandoDePublicacao(ProcessoSeletivo processo) =>
        new(processo.Id, "001/2026", null, null, DocumentoEditalId: Guid.CreateVersion7(), Ato: NovoAto());

    private static ServicosDoCongelamentoDeTeste ServicosComORgUfPorMunicipio(ProcessoSeletivo processo) =>
        ServicosDoCongelamentoDeTeste.Para(processo)
            .ComCatalogoDeFatos(CatalogoComORgUfPorMunicipio())
            .ComUnidades(ResolvedorDeUnidadeDeTeste.Com());

    private static async Task<Result<RecopiadoAoCongelar>> RecopiaEsperadaAsync(ServicosDoCongelamentoDeTeste servicos, ProcessoSeletivo processo)
    {
        CatalogoVivo catalogo = await servicos.Catalogos().LerAsync(processo, CancellationToken.None);

        return await servicos.Recopia().RecopiarAsync(processo, catalogo, new PrimeiraPublicacao(), Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial.MunicipiosConferidosNoGeo.Nenhum, CancellationToken.None);
    }

    private static IFatoCandidatoReader CatalogoComORgUfPorMunicipio()
    {
        IReadOnlyList<FatoCandidatoView> fatos =
            [.. CatalogoDoConjuntoBasico.Fatos.Select(static f => f.Codigo == "RG_UF" ? f with { FonteValores = "GEO_MUNICIPIO" } : f)];
        IFatoCandidatoReader catalogo = Substitute.For<IFatoCandidatoReader>();
        catalogo.ListarAsync(Arg.Any<CancellationToken>()).Returns(fatos);

        return catalogo;
    }
}
