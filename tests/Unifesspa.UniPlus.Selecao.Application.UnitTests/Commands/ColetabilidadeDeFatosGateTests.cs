namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O catálogo de fatos do candidato pode reclassificar a <c>Origem</c> de um fato depois que
/// ele já virou <see cref="FatoColetado"/> — a migration que reclassificou MODALIDADE de
/// DECLARADO para DERIVADO é o caso real. Prova, nos <b>três</b> handlers que congelam
/// (ao lado de <see cref="ConformidadeLegalGateTests"/>), que um <c>FatoColetado</c> cujo fato
/// deixou de ser coletável recusa ANTES da canonicalização, e que um fato ainda coletável não
/// bloqueia a publicação.
/// </summary>
public sealed class ColetabilidadeDeFatosGateTests
{
    private static readonly string HashFixo = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];
    private static readonly DateTimeOffset Agora = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static FatoCandidatoView FatoModalidadeReclassificadaComoDerivada() => new(
        Id: Guid.CreateVersion7(),
        Codigo: "MODALIDADE",
        Nome: "Modalidade de concorrência",
        Descricao: null,
        Dominio: "CATEGORICO",
        Origem: "DERIVADO",
        Cardinalidade: "MULTIVALORADO",
        ValoresDominio: null,
        PontoResolucao: "INSCRICAO",
        Binding: "REGRA_DERIVACAO:MODALIDADE",
        ValoresDominioDeclarados: null);

    private static FatoCandidatoView FatoCorRacaAindaDeclarado() => new(
        Id: Guid.CreateVersion7(),
        Codigo: "COR_RACA",
        Nome: "Cor ou raça",
        Descricao: null,
        Dominio: "CATEGORICO",
        Origem: "DECLARADO",
        Cardinalidade: "ESCALAR",
        ValoresDominio: ["BRANCA", "PRETA", "PARDA", "AMARELA", "INDIGENA"],
        PontoResolucao: "INSCRICAO",
        Binding: "CAMPO_INSCRICAO:COR_RACA",
        ValoresDominioDeclarados: null);

    private static FatoColetado FatoColetadoModalidade() =>
        FatoColetado.Criar("MODALIDADE", 0, "Modalidade", TipoRenderizacao.SelecaoUnica, obrigatorio: true, null).Value!;

    private static FatoColetado FatoColetadoCorRaca() =>
        FatoColetado.Criar("COR_RACA", 0, "Cor ou raça", TipoRenderizacao.SelecaoUnica, obrigatorio: true, null).Value!;

    [Fact(DisplayName = "Publicar_ComFatoColetadoNaoMaisDeclarado_RecusaSemCanonicalizar — o gate precede a canonicalização")]
    public async Task Publicar_ComFatoColetadoNaoMaisDeclarado_RecusaSemCanonicalizar()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        processo.DefinirFatosColetados([FatoColetadoModalidade()], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        IFatoCandidatoReader fatoCandidatoReader = ReaderCom(FatoModalidadeReclassificadaComoDerivada());
        ISnapshotPublicacaoCanonicalizer canonicalizer = CanonicalizerSubstituto();

        (Result resposta, IEnumerable<object> eventos) = await PublicarProcessoSeletivoCommandHandler.Handle(
            new PublicarProcessoSeletivoCommand(
                processo.Id, "001/2026", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31),
                DocumentoEditalId: Guid.CreateVersion7(), Ato: NovoAto()),
            RepositorioDoProcesso(processo),
            RepositorioDeDocumento(processo.Id),
            canonicalizer,
            Substitute.For<ISelecaoUnitOfWork>(),
            UsuarioAutenticado(),
            TipoDeAtoReader(),
            Substitute.For<IVagaDeLinhagemReader>(),
            Substitute.For<IObrigatoriedadeLegalRepository>(),
            fatoCandidatoReader,
            new RelogioFixo(Agora),
            CancellationToken.None);

        resposta.IsFailure.Should().BeTrue();
        resposta.Error!.Code.Should().Be("ProcessoSeletivo.FatoColetadoNaoMaisDeclarado");
        _ = canonicalizer.DidNotReceive().Canonicalizar(Arg.Any<EntradaCanonicalizacao>());
        eventos.Should().BeEmpty();
        processo.Status.Should().Be(StatusProcesso.Rascunho, "a publicação recusada não transita o processo");
    }

    [Fact(DisplayName = "Retificar_ComFatoColetadoNaoMaisDeclarado_RecusaSemCanonicalizar — mesmo gate na retificação")]
    public async Task Retificar_ComFatoColetadoNaoMaisDeclarado_RecusaSemCanonicalizar()
    {
        (ProcessoSeletivo processo, VersaoConfiguracao versaoAtual) = ProcessoPublicadoComFatoColetado();

        IFatoCandidatoReader fatoCandidatoReader = ReaderCom(FatoModalidadeReclassificadaComoDerivada());
        ISnapshotPublicacaoCanonicalizer canonicalizer = CanonicalizerSubstituto();

        IProcessoSeletivoRepository repositorio = RepositorioDoProcesso(processo);
        repositorio.ObterVersaoAtualAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(versaoAtual);

        (Result resposta, IEnumerable<object> eventos) = await RetificarProcessoSeletivoCommandHandler.Handle(
            new RetificarProcessoSeletivoCommand(
                processo.Id, "Correção do prazo", "001/2026-R1",
                new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31),
                DocumentoEditalId: Guid.CreateVersion7(), Ato: NovoAto()),
            repositorio,
            RepositorioDeDocumento(processo.Id),
            canonicalizer,
            Substitute.For<ISelecaoUnitOfWork>(),
            UsuarioAutenticado(),
            TipoDeAtoReader(),
            Substitute.For<IVagaDeLinhagemReader>(),
            Substitute.For<IObrigatoriedadeLegalRepository>(),
            fatoCandidatoReader,
            new RelogioFixo(Agora),
            CancellationToken.None);

        resposta.IsFailure.Should().BeTrue();
        resposta.Error!.Code.Should().Be("ProcessoSeletivo.FatoColetadoNaoMaisDeclarado");
        _ = canonicalizer.DidNotReceive().Canonicalizar(Arg.Any<EntradaCanonicalizacao>());
        eventos.Should().BeEmpty();
    }

    [Fact(DisplayName = "FecharRetificacao_ComFatoColetadoNaoMaisDeclarado_RecusaESessaoPermaneceAberta — recusa não destrói a sessão editorial")]
    public async Task FecharRetificacao_ComFatoColetadoNaoMaisDeclarado_RecusaESessaoPermaneceAberta()
    {
        (ProcessoSeletivo processo, VersaoConfiguracao versaoAtual) = ProcessoPublicadoComFatoColetado();

        Result<RascunhoRetificacao> rascunho = processo.AbrirRetificacao(
            "Correção do prazo", versaoAtual, "user-sub-1", Agora);
        rascunho.IsSuccess.Should().BeTrue(rascunho.Error?.Message);
        processo.DequeueDomainEvents();

        IFatoCandidatoReader fatoCandidatoReader = ReaderCom(FatoModalidadeReclassificadaComoDerivada());
        ISnapshotPublicacaoCanonicalizer canonicalizer = CanonicalizerSubstituto();

        IProcessoSeletivoRepository repositorio = RepositorioDoProcesso(processo);
        repositorio.ObterVersaoAtualAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(versaoAtual);

        (Result resposta, IEnumerable<object> eventos) = await FecharRetificacaoCommandHandler.Handle(
            new FecharRetificacaoCommand(
                processo.Id, "001/2026-R1", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31),
                DocumentoEditalId: Guid.CreateVersion7(), Ato: NovoAto(), Precondicao: PrecondicaoIfMatch.Curinga),
            repositorio,
            RepositorioDeDocumento(processo.Id),
            canonicalizer,
            Substitute.For<ISelecaoUnitOfWork>(),
            UsuarioAutenticado(),
            TipoDeAtoReader(),
            Substitute.For<IVagaDeLinhagemReader>(),
            Substitute.For<IObrigatoriedadeLegalRepository>(),
            fatoCandidatoReader,
            new RelogioFixo(Agora),
            CancellationToken.None);

        resposta.IsFailure.Should().BeTrue();
        resposta.Error!.Code.Should().Be("ProcessoSeletivo.FatoColetadoNaoMaisDeclarado");
        _ = canonicalizer.DidNotReceive().Canonicalizar(Arg.Any<EntradaCanonicalizacao>());
        eventos.Should().BeEmpty();
        processo.Rascunho.Should().NotBeNull(
            "uma recusa de coletabilidade não destrói a sessão editorial — o administrador corrige e tenta de novo");
    }

    [Fact(DisplayName = "Publicar_ComFatoColetadoAindaDeclarado_Aprova — um fato ainda coletável não bloqueia a publicação")]
    public async Task Publicar_ComFatoColetadoAindaDeclarado_Aprova()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        processo.DefinirFatosColetados([FatoColetadoCorRaca()], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        IFatoCandidatoReader fatoCandidatoReader = ReaderCom(FatoCorRacaAindaDeclarado());
        ISnapshotPublicacaoCanonicalizer canonicalizer = CanonicalizerSubstituto();

        (Result resposta, IEnumerable<object> _) = await PublicarProcessoSeletivoCommandHandler.Handle(
            new PublicarProcessoSeletivoCommand(
                processo.Id, "001/2026", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31),
                DocumentoEditalId: Guid.CreateVersion7(), Ato: NovoAto()),
            RepositorioDoProcesso(processo),
            RepositorioDeDocumento(processo.Id),
            canonicalizer,
            Substitute.For<ISelecaoUnitOfWork>(),
            UsuarioAutenticado(),
            TipoDeAtoReader(),
            Substitute.For<IVagaDeLinhagemReader>(),
            Substitute.For<IObrigatoriedadeLegalRepository>(),
            fatoCandidatoReader,
            new RelogioFixo(Agora),
            CancellationToken.None);

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        _ = canonicalizer.Received(1).Canonicalizar(Arg.Any<EntradaCanonicalizacao>());
    }

    // ══════════════════════════════════════════════════════════════════════════════

    private static IFatoCandidatoReader ReaderCom(params FatoCandidatoView[] catalogo)
    {
        IFatoCandidatoReader reader = Substitute.For<IFatoCandidatoReader>();
        reader.ListarAsync(Arg.Any<CancellationToken>()).Returns((IReadOnlyList<FatoCandidatoView>)catalogo);
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

    private static IDocumentoEditalRepository RepositorioDeDocumento(Guid processoSeletivoId)
    {
        DocumentoEdital documento = DocumentoEdital.IniciarPendente(
            processoSeletivoId, TimeProvider.System, TimeSpan.FromMinutes(15));
        documento.Confirmar(1024, HashFixo, TimeProvider.System).IsSuccess.Should().BeTrue();

        IDocumentoEditalRepository repositorio = Substitute.For<IDocumentoEditalRepository>();
        repositorio.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(documento);
        return repositorio;
    }

    private static ITipoAtoPublicadoReader TipoDeAtoReader()
    {
        ITipoAtoPublicadoReader reader = Substitute.For<ITipoAtoPublicadoReader>();
        reader.ObterVigenteAsync("EDITAL_ABERTURA", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView(
                Codigo: "EDITAL_ABERTURA",
                Nome: "Edital de abertura",
                CongelaConfiguracao: true,
                UnicoPorObjeto: false,
                EfeitoIrreversivel: false));
        return reader;
    }

    private static IUserContext UsuarioAutenticado()
    {
        IUserContext contexto = Substitute.For<IUserContext>();
        contexto.UserId.Returns("user-sub-1");
        return contexto;
    }

    private static DadosDoAto NovoAto() => new(
        Orgao: "CEPS",
        Serie: "EDITAL",
        Ano: 2026,
        DataPublicacao: new DateOnly(2026, 1, 1),
        Assinante: "Diretor do CEPS",
        TipoAtoCodigo: "EDITAL_ABERTURA");

    /// <summary>Processo publicado com um FatoColetado (MODALIDADE) — para os cenários de Retificar/FecharRetificacao.</summary>
    private static (ProcessoSeletivo Processo, VersaoConfiguracao VersaoAtual) ProcessoPublicadoComFatoColetado()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        processo.DefinirFatosColetados([FatoColetadoModalidade()], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        DadosEdital dados = DadosEdital.Criar(
            "001/2026", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), Guid.CreateVersion7()).Value!;

        VersaoConfiguracao versao = processo.Publicar(
            dados, "{}"u8.ToArray(), "1.1", "canonical-json/sha256@v1", HashFixo, "user-sub-1",
            new RelogioFixo(Agora)).Value!;
        processo.DequeueDomainEvents();

        return (processo, versao);
    }

    private static ProcessoSeletivo NovoProcessoConforme()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Gate Coletabilidade", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!);

        processo.DefinirEtapas(
            [EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1)],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            modalidadeOrigemId: Guid.CreateVersion7(),
            codigo: "AC",
            descricao: null,
            naturezaLegal: NaturezaLegalModalidade.Ampla,
            composicaoVagas: ComposicaoVagasModalidade.ResidualDoVo,
            composicaoOrigemCodigo: null,
            regraRemanejamento: RegraRemanejamentoModalidade.Nenhuma,
            remanejamentoDestino: null,
            remanejamentoPar: null,
            remanejamentoFallback: null,
            criteriosCumulativos: [],
            acaoQuandoIndeferido: null,
            baseLegal: "Res. Unifesspa 532/2021",
            quantidadeDeclarada: 40).Value!;

        processo.DefinirDistribuicaoVagas(
            [ConfiguracaoDistribuicaoVagas.Criar(
                ofertaCursoOrigemId: Guid.CreateVersion7(),
                voBase: 40,
                pr: 1m,
                regraDistribuicao: ReferenciaRegra.Criar(RegraDistribuicaoVagasCodigo.Institucional, "v1", HashFixo).Value!,
                regraAjuste: null,
                referenciaDemografica: null,
                modalidades: [modalidade]).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirClassificacao(
            ConfiguracaoClassificacao.Criar(
                regraCalculo: ReferenciaRegra.Criar(RegraCalculoCodigo.ClassificacaoImportada, "v1", HashFixo).Value!,
                regraArredondamento: null,
                casasArredondamento: null,
                regraOrdemAlocacao: ReferenciaRegra.Criar(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", HashFixo).Value!,
                nOpcoesAlocacao: 1,
                regrasEliminacao: [], baseadoEmEnem: false).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        FaseCronograma faseConforme = FaseCronograma.Criar(
            ordem: 1,
            faseCanonicaOrigemId: Guid.CreateVersion7(),
            codigo: "RESULTADO_FINAL",
            donoInstitucional: "CEPS",
            origemData: OrigemDataFase.Propria,
            agrupaEtapas: true,
            permiteComplementacao: false,
            produzResultado: true,
            resultadoDefinitivo: true,
            coletaInscricao: true,
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            atoProduzidoCodigo: "RESULTADO_FINAL",
            atoProduzidoEfeitoIrreversivel: false,
            bancasRequeridas: [],
            regraRecurso: null).Value!;
        processo.DefinirCronogramaFases([faseConforme], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        return processo;
    }

    private sealed class RelogioFixo(DateTimeOffset instante) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instante;
    }
}
