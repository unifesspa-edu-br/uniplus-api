namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using Microsoft.Extensions.Options;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.AtosDoProcesso;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProdutosPrevistos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A consulta dos itens que o edital vigente declara publicar cruza o cronograma congelado com o
/// registro dos atos do processo e diz, de cada item, a situação e se ele pode sair agora.
/// </summary>
public sealed class ListarProdutosPrevistosDoProcessoQueryHandlerTests
{
    private const string Materia = "RESULTADO_HOMOLOGACAO";
    private const string OutraMateria = "RESULTADO_ISENCAO";
    private const string Versao = "0.0.10";
    private static readonly DateTimeOffset Agora = new(2027, 1, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly Guid _processoId = Guid.CreateVersion7();
    private readonly IProcessoSeletivoRepository _processos = Substitute.For<IProcessoSeletivoRepository>();
    private readonly IAtoDoProcessoRepository _atos = Substitute.For<IAtoDoProcessoRepository>();
    private readonly IDocumentoAPublicarRepository _documentos = Substitute.For<IDocumentoAPublicarRepository>();
    private readonly IRegistroCodecsEnvelope _codecs = Substitute.For<IRegistroCodecsEnvelope>();
    private readonly ITipoAtoPublicadoReader _tipos = Substitute.For<ITipoAtoPublicadoReader>();
    private readonly IResolvedorFusoInstitucional _fuso = Substitute.For<IResolvedorFusoInstitucional>();
    private readonly List<DocumentoAPublicar> _documentosDoProcesso = [];
    private readonly List<AtoDoProcesso> _atosDoProcesso = [];

    public ListarProdutosPrevistosDoProcessoQueryHandlerTests()
    {
        _processos.ExisteAsync(_processoId, Arg.Any<CancellationToken>()).Returns(true);
        _atos.ListarDoProcessoAsync(_processoId, Arg.Any<CancellationToken>()).Returns(_ => _atosDoProcesso.ToList());
        _documentos.ListarDoProcessoAsync(_processoId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(_ => _documentosDoProcesso.ToList());
        _codecs.Capacidades.Returns([new CapacidadeCodec(Versao, TemEncoder: true, TemDecoder: true, MotivoDaRecusa: null)]);
        _fuso.Resolver().Returns(Result<TimeZoneInfo>.Success(TimeZoneInfo.Utc));
        TiposVigentes(Materia, OutraMateria);
    }

    private void TiposVigentes(params string[] codigos) =>
        _tipos.ListarVigentesAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(codigos.Select(c => new TipoAtoPublicadoView(c, $"Nome de {c}", false, true, false, true)).ToList());

    private void Cronograma(IReadOnlyList<FaseCronograma> fases, IReadOnlyList<EtapaProcesso>? etapas = null, ConfiguracaoDivulgacao? divulgacao = null)
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS 2027 — Medicina");
        VersaoConfiguracao versao = VersaoConfiguracao.Abrir(
            _processoId, [1], Versao, "canonical-json/sha256@v1", Guid.CreateVersion7(), new string('a', 64), "sub", Agora);
        _processos.ObterVersaoVigenteAsync(_processoId, Agora, Arg.Any<CancellationToken>()).Returns(versao);
        _codecs.Reidratar(versao).Returns(Result<EnvelopeReidratado>.Success(new EnvelopeReidratado(
            new GrafoConfiguracao(
                etapas ?? [.. processo.Etapas], processo.OfertaAtendimento!, [.. processo.DistribuicaoVagas],
                processo.BonusRegional, [.. processo.CriteriosDesempate], processo.Classificacao!,
                fases, [.. processo.DocumentosExigidos], [], null, configuracaoDivulgacao: divulgacao, numeroDeOpcoesDeCurso: 1),
            DadosEdital.Criar("001/2027", Agora, Agora.AddDays(30), Guid.CreateVersion7()).Value!,
            new string('a', 64), "America/Sao_Paulo", retificacao: null, conformidade: null)));
    }

    private static FaseCronograma Fase(string codigo, int ordem, params ProdutoDaFase[] produtos) =>
        FaseQueConclui(codigo, ordem, null, produtos);

    private static FaseCronograma FaseQueConclui(string codigo, int ordem, string? faseConcluinteCodigo, params ProdutoDaFase[] produtos) =>
        FaseCronograma.Criar(
            ordem, Guid.CreateVersion7(), codigo, "CEPS", OrigemDataFase.Propria,
            agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false,
            inicio: Agora, fim: Agora.AddDays(10), produtos: produtos,
            faseConcluinteCodigo: faseConcluinteCodigo, emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null).Value!;

    private static EtapaProcesso Etapa(string nome, string? faseCodigo, params ProdutoDaEtapa[] produtos)
    {
        EtapaProcesso etapa = EtapaProcesso.Criar(
            nome, CaraterEtapa.Classificatoria,
            TipoEtapaSnapshot.Criar(CadastrosVivos.IdentidadeDe("PROVA_OBJETIVA"), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!,
            peso: 1m, ordem: 1, faseCodigo: faseCodigo).Value!;
        etapa.DefinirProdutos(produtos).IsSuccess.Should().BeTrue();
        return etapa;
    }

    private DocumentoAPublicar Documento()
    {
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(
            _processoId, FinalidadeDoDocumento.DocumentoDeAto, "lista.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        documento.Confirmar(2048, new string('c', 64), TimeProvider.System).IsSuccess.Should().BeTrue();
        _documentosDoProcesso.Add(documento);
        return documento;
    }

    private AtoDoProcesso Publicar(string tipo, Guid? produtoId, bool registrado, Guid? corrige = null)
    {
        AtoDoProcesso ato = AtoDoProcesso.Registrar(
            new NovoAtoDoProcesso(
                Guid.CreateVersion7(), _processoId, tipo, $"Nome de {tipo}", "5/2027", new DateOnly(2027, 1, 20),
                "Diretor do CEPS", "ceps", "psm-medicina-2027", Documento().Id, [],
                ProdutoDoEditalId: produtoId, AtoCorrigidoId: corrige, MotivoDaCorrecao: corrige is null ? null : "Erro de digitação"),
            new RelogioFixo(Agora.AddMinutes(-10))).Value!;
        typeof(AtoDoProcesso).GetProperty(nameof(AtoDoProcesso.CreatedAt))!.SetValue(ato, Agora.AddMinutes(-10 + _atosDoProcesso.Count));
        if (registrado)
        {
            ato.ConfirmarRegistro(new RelogioFixo(Agora.AddMinutes(-5)));
        }

        _atosDoProcesso.Add(ato);
        return ato;
    }

    private async Task<Result<ProdutosPrevistosDoProcessoDto>> Consultar() =>
        await ListarProdutosPrevistosDoProcessoQueryHandler.Handle(
            new ListarProdutosPrevistosDoProcessoQuery(_processoId),
            _processos, _atos, _documentos, _codecs, _tipos, _fuso,
            Options.Create(new AtosDoProcessoOptions()),
            new RelogioFixo(Agora),
            CancellationToken.None);

    private async Task<ProdutoPrevistoDto> ItemDaFase(Guid produtoId)
    {
        Result<ProdutosPrevistosDoProcessoDto> resultado = await Consultar();
        resultado.IsSuccess.Should().BeTrue();
        return resultado.Value!.Fases.SelectMany(f => f.Produtos).Single(p => p.Id == produtoId);
    }

    private static (ProdutoDaFase Preliminar, ProdutoDaFase Definitivo) Par(string materia = Materia) =>
        (ProdutoDaFase.Criar(materia, PapelProdutoFase.Preliminar), ProdutoDaFase.Criar(materia, PapelProdutoFase.Definitivo));

    [Fact(DisplayName = "A consulta mostra a política de divulgação que a versão congelou, e o piso quando ela não declarou")]
    public async Task Consultar_ExibeAPoliticaDeDivulgacaoDaVersao()
    {
        (ProdutoDaFase preliminar, _) = Par();
        ConfiguracaoDivulgacao ampliada = ConfiguracaoDivulgacao.Criar(
            [ConfiguracaoDivulgacao.NumeroInscricao, ConfiguracaoDivulgacao.Nome], "Lista nominal exigida pelo edital").Value!;

        Cronograma([Fase("HOMOLOGACAO", 1, preliminar)], divulgacao: ampliada);
        (await Consultar()).Value!.PoliticaDeDivulgacao.CamposPublicos
            .Should().BeEquivalentTo([ConfiguracaoDivulgacao.NumeroInscricao, ConfiguracaoDivulgacao.Nome]);

        Cronograma([Fase("HOMOLOGACAO", 1, preliminar)]);
        (await Consultar()).Value!.PoliticaDeDivulgacao.CamposPublicos
            .Should().Equal(ConfiguracaoDivulgacao.NumeroInscricao);
    }

    [Fact(DisplayName = "Preliminar e definitivo da mesma matéria vêm abertos, e o definitivo diz que espera o preliminar")]
    public async Task Handle_PreliminarEDefinitivoAbertos_DefinitivoNaoPodeSair()
    {
        (ProdutoDaFase preliminar, ProdutoDaFase definitivo) = Par();
        Cronograma([Fase("INSCRICAO", 1, preliminar, definitivo)]);

        Result<ProdutosPrevistosDoProcessoDto> resultado = await Consultar();

        ProdutoPrevistoDto[] produtos = [.. resultado.Value!.Fases.Single().Produtos];
        produtos.Select(p => p.Situacao).Should().AllBeEquivalentTo(SituacaoDoProdutoPrevisto.Aberto);
        produtos.Single(p => p.Id == preliminar.Id).PodePublicar.Should().BeTrue();
        produtos.Select(p => p.Papel).Should().BeEquivalentTo("PRELIMINAR", "DEFINITIVO");
        ProdutoPrevistoDto doDefinitivo = produtos.Single(p => p.Id == definitivo.Id);
        doDefinitivo.PodePublicar.Should().BeFalse();
        doDefinitivo.Impedimento!.Motivo.Should().Be(ImpedimentoDoProduto.PreliminarNaoPublicado);
        doDefinitivo.Impedimento.Mensagem.Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "Publicado o preliminar, ele aparece com ato e documento e o definitivo passa a poder sair")]
    public async Task Handle_PreliminarPublicado_LiberaODefinitivo()
    {
        (ProdutoDaFase preliminar, ProdutoDaFase definitivo) = Par();
        Cronograma([Fase("INSCRICAO", 1, preliminar, definitivo)]);
        AtoDoProcesso ato = Publicar(Materia, preliminar.Id, registrado: true);

        ProdutoPrevistoDto doPreliminar = await ItemDaFase(preliminar.Id);
        ProdutoPrevistoDto doDefinitivo = await ItemDaFase(definitivo.Id);

        doPreliminar.Situacao.Should().Be(SituacaoDoProdutoPrevisto.Publicado);
        doPreliminar.Ato!.Id.Should().Be(ato.Id);
        doPreliminar.Ato.Numero.Should().Be("5/2027");
        doPreliminar.Ato.DocumentoPrincipal.NomeArquivo.Should().Be("lista.pdf");
        doPreliminar.PodePublicar.Should().BeFalse();
        doPreliminar.Impedimento!.Motivo.Should().Be(ImpedimentoDoProduto.JaPublicado);
        doDefinitivo.Situacao.Should().Be(SituacaoDoProdutoPrevisto.Aberto);
        doDefinitivo.PodePublicar.Should().BeTrue();
        doDefinitivo.Impedimento.Should().BeNull();
    }

    [Fact(DisplayName = "Preliminar enviado e sem confirmação está pendente e ainda não libera o definitivo")]
    public async Task Handle_PreliminarPendente_NaoLiberaODefinitivo()
    {
        (ProdutoDaFase preliminar, ProdutoDaFase definitivo) = Par();
        Cronograma([Fase("INSCRICAO", 1, preliminar, definitivo)]);
        Publicar(Materia, preliminar.Id, registrado: false);

        ProdutoPrevistoDto doPreliminar = await ItemDaFase(preliminar.Id);
        ProdutoPrevistoDto doDefinitivo = await ItemDaFase(definitivo.Id);

        doPreliminar.Situacao.Should().Be(SituacaoDoProdutoPrevisto.Pendente);
        doPreliminar.Impedimento!.Motivo.Should().Be(ImpedimentoDoProduto.AguardaConfirmacaoDoRegistro);
        doDefinitivo.PodePublicar.Should().BeFalse();
        doDefinitivo.Impedimento!.Motivo.Should().Be(ImpedimentoDoProduto.PreliminarAguardaConfirmacao);
    }

    [Fact(DisplayName = "O preliminar de outra matéria não libera o definitivo")]
    public async Task Handle_PreliminarDeOutraMateria_NaoLibera()
    {
        (ProdutoDaFase preliminar, ProdutoDaFase definitivo) = Par();
        (ProdutoDaFase outroPreliminar, ProdutoDaFase outroDefinitivo) = Par(OutraMateria);
        Cronograma([Fase("INSCRICAO", 1, preliminar, definitivo, outroPreliminar, outroDefinitivo)]);
        Publicar(OutraMateria, outroPreliminar.Id, registrado: true);

        (await ItemDaFase(definitivo.Id)).Impedimento!.Motivo.Should().Be(ImpedimentoDoProduto.PreliminarNaoPublicado);
        (await ItemDaFase(outroDefinitivo.Id)).PodePublicar.Should().BeTrue();
    }

    [Fact(DisplayName = "O definitivo não espera o preliminar de outra fase")]
    public async Task Handle_PreliminarDeOutraFase_NaoEhEsperado()
    {
        (ProdutoDaFase preliminar, ProdutoDaFase definitivo) = Par();
        ProdutoDaFase definitivoDaOutraFase = ProdutoDaFase.Criar(Materia, PapelProdutoFase.Definitivo);
        Cronograma([Fase("INSCRICAO", 1, preliminar, definitivo), Fase("ISENCAO", 2, definitivoDaOutraFase)]);
        Publicar(Materia, preliminar.Id, registrado: true);

        // Sem preliminar declarado na própria fase, o definitivo não espera nada.
        (await ItemDaFase(definitivoDaOutraFase.Id)).PodePublicar.Should().BeTrue();
        (await ItemDaFase(definitivo.Id)).PodePublicar.Should().BeTrue();
    }

    [Fact(DisplayName = "O definitivo da fase concluinte espera o preliminar do mesmo tipo da fase que abre o ciclo")]
    public async Task Handle_CicloEntreFases_DefinitivoEsperaOPreliminarDaFaseQueAbre()
    {
        ProdutoDaFase preliminar = ProdutoDaFase.Criar(Materia, PapelProdutoFase.Preliminar);
        ProdutoDaFase preliminarOutroTipo = ProdutoDaFase.Criar(OutraMateria, PapelProdutoFase.Preliminar);
        ProdutoDaFase definitivo = ProdutoDaFase.Criar(Materia, PapelProdutoFase.Definitivo);
        Cronograma([FaseQueConclui("AVALIACAO", 1, "RESULTADO", preliminar, preliminarOutroTipo), Fase("RESULTADO", 2, definitivo)]);

        (await ItemDaFase(definitivo.Id)).Impedimento!.Motivo.Should().Be(ImpedimentoDoProduto.PreliminarNaoPublicado);

        Publicar(OutraMateria, preliminarOutroTipo.Id, registrado: true);
        (await ItemDaFase(definitivo.Id)).Impedimento!.Motivo.Should().Be(
            ImpedimentoDoProduto.PreliminarNaoPublicado, "o preliminar de outro tipo de ato não libera o definitivo");

        Publicar(Materia, preliminar.Id, registrado: true);
        (await ItemDaFase(definitivo.Id)).PodePublicar.Should().BeTrue();
    }

    [Fact(DisplayName = "Duas fases que abrem ciclo com o mesmo tipo e apontam a mesma concluinte: o definitivo espera os dois preliminares")]
    public async Task Handle_DuasFasesAbremCicloComOMesmoTipo_DefinitivoEsperaOsDois()
    {
        ProdutoDaFase primeiroPreliminar = ProdutoDaFase.Criar(Materia, PapelProdutoFase.Preliminar);
        ProdutoDaFase segundoPreliminar = ProdutoDaFase.Criar(Materia, PapelProdutoFase.Preliminar);
        ProdutoDaFase definitivo = ProdutoDaFase.Criar(Materia, PapelProdutoFase.Definitivo);
        Cronograma(
        [
            FaseQueConclui("PROVA_1", 1, "RESULTADO", primeiroPreliminar),
            FaseQueConclui("PROVA_2", 2, "RESULTADO", segundoPreliminar),
            Fase("RESULTADO", 3, definitivo),
        ]);

        Publicar(Materia, primeiroPreliminar.Id, registrado: true);
        (await ItemDaFase(definitivo.Id)).Impedimento!.Motivo.Should().Be(ImpedimentoDoProduto.PreliminarNaoPublicado);

        Publicar(Materia, segundoPreliminar.Id, registrado: true);
        (await ItemDaFase(definitivo.Id)).PodePublicar.Should().BeTrue();
    }

    [Fact(DisplayName = "O preliminar de uma fase que aponta outra concluinte não é esperado")]
    public async Task Handle_FaseQueApontaOutraConcluinte_NaoEhEsperada()
    {
        ProdutoDaFase preliminar = ProdutoDaFase.Criar(Materia, PapelProdutoFase.Preliminar);
        ProdutoDaFase definitivo = ProdutoDaFase.Criar(Materia, PapelProdutoFase.Definitivo);
        Cronograma([FaseQueConclui("PROVA", 1, "OUTRA", preliminar), Fase("RESULTADO", 2, definitivo), Fase("OUTRA", 3)]);

        (await ItemDaFase(definitivo.Id)).PodePublicar.Should().BeTrue();
    }

    [Fact(DisplayName = "O ciclo que a fase de origem fecha com o próprio definitivo não chega à concluinte")]
    public async Task Handle_CicloFechadoNaFaseDeOrigem_NaoEhEsperadoPelaConcluinte()
    {
        (ProdutoDaFase preliminar, ProdutoDaFase definitivoDaOrigem) = Par();
        ProdutoDaFase preliminarAberto = ProdutoDaFase.Criar(OutraMateria, PapelProdutoFase.Preliminar);
        ProdutoDaFase definitivoDaConcluinte = ProdutoDaFase.Criar(Materia, PapelProdutoFase.Definitivo);
        Cronograma([FaseQueConclui("AVALIACAO", 1, "RESULTADO", preliminar, definitivoDaOrigem, preliminarAberto), Fase("RESULTADO", 2, definitivoDaConcluinte)]);

        (await ItemDaFase(definitivoDaConcluinte.Id)).PodePublicar.Should().BeTrue();
    }

    [Fact(DisplayName = "A correção de uma lista publicada entra na cadeia e não substitui a primeira publicação")]
    public async Task Handle_Correcao_EntraNaCadeiaDoProduto()
    {
        (ProdutoDaFase preliminar, ProdutoDaFase definitivo) = Par();
        Cronograma([Fase("INSCRICAO", 1, preliminar, definitivo)]);
        AtoDoProcesso primeira = Publicar(Materia, preliminar.Id, registrado: true);
        AtoDoProcesso errata = Publicar("ERRATA", null, registrado: true, corrige: primeira.Id);
        AtoDoProcesso retificacao = Publicar("RETIFICACAO", null, registrado: false, corrige: errata.Id);

        ProdutoPrevistoDto doPreliminar = await ItemDaFase(preliminar.Id);

        doPreliminar.Ato!.Id.Should().Be(primeira.Id);
        doPreliminar.Situacao.Should().Be(SituacaoDoProdutoPrevisto.Publicado);
        doPreliminar.Correcoes.Select(c => c.Id).Should().Equal(errata.Id, retificacao.Id);
    }

    [Fact(DisplayName = "Os itens de etapa vêm sob a fase da etapa, e a etapa sem fase vem à parte")]
    public async Task Handle_ProdutosDeEtapa_AgrupadosSobAFase()
    {
        ProdutoDaEtapa gabarito = ProdutoDaEtapa.Criar(Materia, PapelProdutoFase.Preliminar);
        ProdutoDaEtapa semFase = ProdutoDaEtapa.Criar(OutraMateria, null);
        ProdutoDaFase daFase = ProdutoDaFase.Criar(OutraMateria, null);
        Cronograma(
            [Fase("AVALIACAO", 1, daFase), Fase("RESULTADO", 2)],
            [Etapa("Prova objetiva", "AVALIACAO", gabarito), Etapa("Entrevista", null, semFase)]);

        Result<ProdutosPrevistosDoProcessoDto> resultado = await Consultar();

        FaseComProdutosPrevistosDto avaliacao = resultado.Value!.Fases.Single(f => f.Codigo == "AVALIACAO");
        avaliacao.Produtos.Select(p => p.Id).Should().Equal(daFase.Id);
        avaliacao.Etapas.Single().Nome.Should().Be("Prova objetiva");
        avaliacao.Etapas.Single().Produtos.Single().Id.Should().Be(gabarito.Id);
        resultado.Value.Fases.Single(f => f.Codigo == "RESULTADO").Etapas.Should().BeEmpty();
        resultado.Value.EtapasSemFase.Single().Produtos.Single().Id.Should().Be(semFase.Id);
    }

    [Fact(DisplayName = "Tipo de ato sem versão vigente no catálogo impede a publicação")]
    public async Task Handle_TipoNaoVigente_Impede()
    {
        (ProdutoDaFase preliminar, ProdutoDaFase definitivo) = Par();
        Cronograma([Fase("INSCRICAO", 1, preliminar, definitivo)]);
        TiposVigentes();

        ProdutoPrevistoDto doPreliminar = await ItemDaFase(preliminar.Id);

        doPreliminar.PodePublicar.Should().BeFalse();
        doPreliminar.Impedimento!.Motivo.Should().Be(ImpedimentoDoProduto.TipoDeAtoNaoVigente);
        doPreliminar.TipoNome.Should().BeNull();
    }

    [Fact(DisplayName = "Duas versões vigentes do mesmo tipo no catálogo não derrubam a consulta")]
    public async Task Handle_TipoComDuasVersoesVigentes_NaoFalha()
    {
        (ProdutoDaFase preliminar, ProdutoDaFase definitivo) = Par();
        Cronograma([Fase("INSCRICAO", 1, preliminar, definitivo)]);
        _tipos.ListarVigentesAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns([
                new TipoAtoPublicadoView(Materia, "Primeira versão", false, true, false, true),
                new TipoAtoPublicadoView(Materia, "Segunda versão", false, true, false, true),
            ]);

        (await ItemDaFase(preliminar.Id)).TipoNome.Should().Be("Primeira versão");
    }

    [Fact(DisplayName = "Processo sem versão vigente devolve a recusa nomeada, e processo inexistente devolve não encontrado")]
    public async Task Handle_SemVersaoVigente_DistingueDeInexistente()
    {
        Result<ProdutosPrevistosDoProcessoDto> semVersao = await Consultar();
        _processos.ExisteAsync(_processoId, Arg.Any<CancellationToken>()).Returns(false);
        Result<ProdutosPrevistosDoProcessoDto> inexistente = await Consultar();

        semVersao.Error!.Code.Should().Be("Snapshot.VigenteAusente");
        inexistente.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }
}
