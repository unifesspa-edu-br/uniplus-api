namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O fato que decide quem recebe o acréscimo é conhecido até a fase da inscrição (ADR-0142). O
/// checklist confere a fase pelo formulário; o ponto de resolução que o catálogo dá ao fato só os
/// três handlers que congelam leem, e é o que cada um destes testes prova.
/// </summary>
public sealed class FatoDeElegibilidadeGateTests
{
    private const string Fato = "PEDE_O_ACRESCIMO";
    private static readonly string HashFixo = ProcessoSeletivoConformeBuilder.HashFixo;
    private static readonly DateTimeOffset Agora = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Publicar recusa, antes de canonicalizar, o fato de elegibilidade que o catálogo só resolve na habilitação")]
    public async Task Publicar_FatoResolvidoNaHabilitacao_Recusa()
    {
        ProcessoSeletivo processo = ProcessoQueAplicaOAcrescimo();
        ISnapshotPublicacaoCanonicalizer canonicalizer = CanonicalizerSubstituto();

        (Result resposta, IEnumerable<object> eventos) = await PublicarProcessoSeletivoCommandHandler.Handle(
            new PublicarProcessoSeletivoCommand(
                processo.Id, "001/2026", null, null,
                DocumentoEditalId: Guid.CreateVersion7(), Ato: NovoAto()),
            RepositorioDoProcesso(processo),
            RepositorioDeDocumento(processo.Id),
            Substitute.For<IRascunhoDePublicacaoRepository>(),
            canonicalizer, new ResolvedorFusoDeTeste(),
            Substitute.For<ISelecaoUnitOfWork>(),
            UsuarioAutenticado(),
            TipoDeAtoReader(),
            Substitute.For<IVagaDeLinhagemReader>(),
            Substitute.For<IObrigatoriedadeLegalRepository>(),
            CadastrosVivos.Modalidades(),
            CadastrosVivos.TiposDocumento(),
            CadastrosVivos.TiposEtapa(),
            CadastrosVivos.TiposDeficiencia(),
            CadastrosVivos.RegrasDesempate(),
            CatalogoQueResolveNaHabilitacao(),
            CalendarioVigenteReaderDeTeste.SemVigente(),
            new RelogioFixo(Agora),
            CancellationToken.None);

        resposta.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.FatoElegibilidadeConhecidoDepoisDaInscricao);
        _ = canonicalizer.DidNotReceive().Canonicalizar(Arg.Any<EntradaCanonicalizacao>());
        eventos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Retificar recusa, antes de canonicalizar, o fato de elegibilidade que o catálogo só resolve na habilitação")]
    public async Task Retificar_FatoResolvidoNaHabilitacao_Recusa()
    {
        (ProcessoSeletivo processo, VersaoConfiguracao versaoAtual) = ProcessoPublicado();
        ISnapshotPublicacaoCanonicalizer canonicalizer = CanonicalizerSubstituto();
        IProcessoSeletivoRepository repositorio = RepositorioDoProcesso(processo);
        repositorio.ObterVersaoAtualAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(versaoAtual);

        (Result resposta, IEnumerable<object> eventos) = await RetificarProcessoSeletivoCommandHandler.Handle(
            new RetificarProcessoSeletivoCommand(
                processo.Id, "Correção do prazo", "001/2026-R1",
                null, null,
                DocumentoEditalId: Guid.CreateVersion7(), Ato: NovoAto()),
            repositorio,
            RepositorioDeDocumento(processo.Id),
            Substitute.For<IRascunhoDePublicacaoRepository>(),
            canonicalizer, new ResolvedorFusoDeTeste(),
            Substitute.For<ISelecaoUnitOfWork>(),
            UsuarioAutenticado(),
            TipoDeAtoReader(),
            Substitute.For<IVagaDeLinhagemReader>(),
            Substitute.For<IObrigatoriedadeLegalRepository>(),
            CadastrosVivos.Modalidades(),
            CadastrosVivos.TiposDocumento(),
            CadastrosVivos.TiposEtapa(),
            CadastrosVivos.TiposDeficiencia(),
            CadastrosVivos.RegrasDesempate(),
            CatalogoQueResolveNaHabilitacao(),
            CalendarioVigenteReaderDeTeste.SemVigente(),
            Substitute.For<IRegistroCodecsEnvelope>(),
            new RelogioFixo(Agora),
            CancellationToken.None);

        resposta.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.FatoElegibilidadeConhecidoDepoisDaInscricao);
        _ = canonicalizer.DidNotReceive().Canonicalizar(Arg.Any<EntradaCanonicalizacao>());
        eventos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Fechar a retificação recusa, antes de canonicalizar, o fato de elegibilidade que o catálogo só resolve na habilitação")]
    public async Task FecharRetificacao_FatoResolvidoNaHabilitacao_Recusa()
    {
        (ProcessoSeletivo processo, VersaoConfiguracao versaoAtual) = ProcessoPublicado();
        processo.AbrirRetificacao(
            "Correção do prazo", versaoAtual, identificadorDaVersaoBase: null, VersoesPublicadasDeTeste.SoAVigente(processo), "user-sub-1", Agora)
            .IsSuccess.Should().BeTrue();
        processo.DequeueDomainEvents();
        ISnapshotPublicacaoCanonicalizer canonicalizer = CanonicalizerSubstituto();
        IProcessoSeletivoRepository repositorio = RepositorioDoProcesso(processo);
        repositorio.ObterVersaoAtualAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(versaoAtual);

        (Result resposta, IEnumerable<object> eventos) = await FecharRetificacaoCommandHandler.Handle(
            new FecharRetificacaoCommand(
                processo.Id, "001/2026-R1", null, null,
                DocumentoEditalId: Guid.CreateVersion7(), Ato: NovoAto(), Precondicao: PrecondicaoIfMatch.Curinga),
            repositorio,
            RepositorioDeDocumento(processo.Id),
            Substitute.For<IRascunhoDePublicacaoRepository>(),
            canonicalizer, new ResolvedorFusoDeTeste(),
            Substitute.For<ISelecaoUnitOfWork>(),
            UsuarioAutenticado(),
            TipoDeAtoReader(),
            Substitute.For<IVagaDeLinhagemReader>(),
            Substitute.For<IObrigatoriedadeLegalRepository>(),
            CadastrosVivos.Modalidades(),
            CadastrosVivos.TiposDocumento(),
            CadastrosVivos.TiposEtapa(),
            CadastrosVivos.TiposDeficiencia(),
            CadastrosVivos.RegrasDesempate(),
            CatalogoQueResolveNaHabilitacao(),
            CalendarioVigenteReaderDeTeste.SemVigente(),
            new RelogioFixo(Agora),
            CancellationToken.None);

        resposta.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.FatoElegibilidadeConhecidoDepoisDaInscricao);
        _ = canonicalizer.DidNotReceive().Canonicalizar(Arg.Any<EntradaCanonicalizacao>());
        eventos.Should().BeEmpty();
        processo.Rascunho.Should().NotBeNull("a recusa não destrói a sessão editorial");
    }

    /// <summary>
    /// Processo conforme com a habilitação depois da inscrição e o acréscimo citando um campo booleano
    /// do formulário de inscrição: sem o catálogo, o fato é da inscrição e o checklist o aceita.
    /// </summary>
    private static ProcessoSeletivo ProcessoQueAplicaOAcrescimo()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Gate Elegibilidade");
        FaseCronograma habilitacao = FaseCronograma.Criar(
            ordem: 2, faseCanonicaOrigemId: Guid.CreateVersion7(), codigo: FormularioProcesso.CodigoFaseHabilitacao,
            donoInstitucional: "CEPS", origemData: OrigemDataFase.Propria, agrupaEtapas: false, permiteComplementacao: false,
            coletaInscricao: false, coletaSolicitacaoIsencao: false,
            inicio: new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero), fim: new DateTimeOffset(2026, 2, 28, 0, 0, 0, TimeSpan.Zero),
            produtos: [], faseConcluinteCodigo: null, emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null).Value!;
        FormularioDeTeste.TrocarCronograma(processo, [.. processo.CronogramaFases, habilitacao], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        Result itens = processo.DefinirItens(
            [FatoColetado.Criar(Fato, 0, "Deseja solicitar o acréscimo?", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL").Value!]);
        itens.IsSuccess.Should().BeTrue(itens.Error?.Message);

        processo.DefinirBonusRegional(
            aplica: true,
            ConfiguracaoBonusRegional.Criar(
                ReferenciaRegra.Criar(RegraBonusCodigo.Multiplicativo, "v1", HashFixo).Value!,
                1.20m, null, Fato, Guid.CreateVersion7(), "EDITAL", "Edital nº 1/2026", "Fator de Atenuação das Desigualdades de Acesso",
                [("1504208", "Marabá", "PA")]).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.RecusaDoFatoDeElegibilidade(new Dictionary<string, string>(), new Dictionary<string, string>())
            .Should().BeNull("sem o catálogo, o campo da inscrição é conhecido na inscrição");
        return processo;
    }

    private static (ProcessoSeletivo Processo, VersaoConfiguracao VersaoAtual) ProcessoPublicado()
    {
        ProcessoSeletivo processo = ProcessoQueAplicaOAcrescimo();
        DadosEdital dados = DadosEdital.Criar(
            "001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
            new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)), Guid.CreateVersion7()).Value!;
        Result<VersaoConfiguracao> versao = processo.Publicar(
            dados, EnvelopeCongeladoDeTeste.SemAnexos(), "1.1", "canonical-json/sha256@v1", HashFixo, "user-sub-1",
            new RelogioFixo(Agora), ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo);
        versao.IsSuccess.Should().BeTrue(versao.Error?.Message);
        processo.DequeueDomainEvents();
        return (processo, versao.Value!);
    }

    /// <summary>O catálogo resolve o campo citado na habilitação, depois da inscrição.</summary>
    private static IFatoCandidatoReader CatalogoQueResolveNaHabilitacao()
    {
        FatoCandidatoView pedido = new(
            Guid.CreateVersion7(), Fato, "Pede o acréscimo", null, "BOOLEANO", "DECLARADO", "ESCALAR", null,
            FormularioProcesso.CodigoFaseHabilitacao, $"CAMPO_FORMULARIO:{Fato}", null, null, Ativo: true, ClassificacaoProtecao: "PESSOAL");
        IFatoCandidatoReader reader = Substitute.For<IFatoCandidatoReader>();
        reader.ListarAsync(Arg.Any<CancellationToken>()).Returns(CatalogoDoConjuntoBasico.Com((IReadOnlyList<FatoCandidatoView>)[pedido]));
        return reader;
    }

    private static ISnapshotPublicacaoCanonicalizer CanonicalizerSubstituto()
    {
        ISnapshotPublicacaoCanonicalizer canonicalizer = Substitute.For<ISnapshotPublicacaoCanonicalizer>();
        canonicalizer.Canonicalizar(Arg.Any<EntradaCanonicalizacao>())
            .Returns(new SnapshotCanonico("{}"u8.ToArray(), "1.1", "canonical-json/sha256@v1"));
        return canonicalizer;
    }

    private static IProcessoSeletivoRepository RepositorioDoProcesso(ProcessoSeletivo processo)
    {
        IProcessoSeletivoRepository repositorio = Substitute.For<IProcessoSeletivoRepository>();
        repositorio.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        return repositorio;
    }

    private static IDocumentoAPublicarRepository RepositorioDeDocumento(Guid processoSeletivoId)
    {
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(processoSeletivoId, "edital.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        documento.Confirmar(1024, HashFixo, TimeProvider.System).IsSuccess.Should().BeTrue();

        IDocumentoAPublicarRepository repositorio = Substitute.For<IDocumentoAPublicarRepository>();
        repositorio.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(documento);
        return repositorio;
    }

    private static ITipoAtoPublicadoReader TipoDeAtoReader()
    {
        ITipoAtoPublicadoReader reader = Substitute.For<ITipoAtoPublicadoReader>();
        reader.ObterVigenteAsync("EDITAL_ABERTURA", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView(
                Codigo: "EDITAL_ABERTURA", Nome: "Edital de abertura", CongelaConfiguracao: true,
                UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: false));
        return reader;
    }

    private static IUserContext UsuarioAutenticado()
    {
        IUserContext contexto = Substitute.For<IUserContext>();
        contexto.UserId.Returns("user-sub-1");
        return contexto;
    }

    private static DadosDoAto NovoAto() => new(
        Orgao: "CEPS", Serie: "EDITAL", Ano: 2026, DataPublicacao: new DateOnly(2026, 1, 1),
        Assinante: "Diretor do CEPS", TipoAtoCodigo: "EDITAL_ABERTURA");

    private sealed class RelogioFixo(DateTimeOffset instante) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instante;
    }
}
