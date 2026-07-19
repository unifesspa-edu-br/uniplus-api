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
/// Story #853: o gate legal — segunda dimensão de conformidade, ao lado da estrutural
/// (<see cref="PublicarProcessoSeletivoGateTests"/>) — nos <b>três</b> handlers que
/// congelam (CA-12, CA-14). Prova, por handler, que uma regra vigente reprovada recusa
/// ANTES da canonicalização, e que a aprovação repassa o <see cref="ResultadoConformidade"/>
/// no campo novo de <see cref="EntradaCanonicalizacao"/> (CA-13).
/// </summary>
public sealed class ConformidadeLegalGateTests
{
    private static readonly string HashFixo = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];
    private static readonly DateTimeOffset Agora = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static ObrigatoriedadeLegal NovaRegra(string regraCodigo, PredicadoObrigatoriedade predicado) =>
        ObrigatoriedadeLegal.Criar(
            tipoProcessoCodigo: ObrigatoriedadeLegal.TipoProcessoUniversal,
            categoria: CategoriaObrigatoriedade.Outros,
            regraCodigo: regraCodigo,
            predicado: predicado,
            descricaoHumana: "Regra de teste do gate",
            baseLegal: "Lei de teste",
            vigenciaInicio: new DateOnly(2020, 1, 1)).Value!;

    // A "Prova Objetiva" é a única etapa de NovoProcessoConforme() — exigir OUTRA etapa
    // reprova; exigir a MESMA aprova. Suficiente para testar o fio do gate sem repetir
    // as 7 variantes já cobertas por AvaliadorConformidadeLegalTests (Domain).
    private static ObrigatoriedadeLegal RegraQueReprova() =>
        NovaRegra("GATE-REPROVA", new EtapaObrigatoria("Etapa Que Não Existe"));

    private static ObrigatoriedadeLegal RegraQueAprova() =>
        NovaRegra("GATE-APROVA", new EtapaObrigatoria("Prova Objetiva"));

    [Fact(DisplayName = "CA-12 (Publicar): regra legal vigente reprovada recusa ANTES da canonicalização")]
    public async Task Publicar_ComRegraLegalReprovada_RecusaSemCanonicalizar()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        IObrigatoriedadeLegalRepository obrigatoriedadeLegalRepository = RepositorioCom(RegraQueReprova());
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
            obrigatoriedadeLegalRepository,
            Substitute.For<IFatoCandidatoReader>(),
            new RelogioFixo(Agora),
            CancellationToken.None);

        resposta.IsFailure.Should().BeTrue();
        resposta.Error!.Code.Should().Be("ProcessoSeletivo.ConformidadeLegalInsuficiente");
        _ = canonicalizer.DidNotReceive().Canonicalizar(Arg.Any<EntradaCanonicalizacao>());
        eventos.Should().BeEmpty();
        processo.Status.Should().Be(StatusProcesso.Rascunho, "a publicação recusada não transita o processo");
    }

    [Fact(DisplayName = "CA-13 (Publicar): regra legal aprovada congela ResultadoConformidade no campo novo da entrada")]
    public async Task Publicar_ComRegraLegalAprovada_RepassaConformidadeParaCanonicalizador()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        ObrigatoriedadeLegal regra = RegraQueAprova();
        IObrigatoriedadeLegalRepository obrigatoriedadeLegalRepository = RepositorioCom(regra);

        EntradaCanonicalizacao? entradaCapturada = null;
        ISnapshotPublicacaoCanonicalizer canonicalizer = CanonicalizerSubstituto(e => entradaCapturada = e);

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
            obrigatoriedadeLegalRepository,
            Substitute.For<IFatoCandidatoReader>(),
            new RelogioFixo(Agora),
            CancellationToken.None);

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        entradaCapturada.Should().NotBeNull();
        entradaCapturada!.Conformidade.Should().NotBeNull();
        RegraAvaliada regraCongelada = entradaCapturada.Conformidade!.Regras.Should().ContainSingle().Subject;
        regraCongelada.RegraId.Should().Be(regra.Id);
        regraCongelada.RegraCodigo.Should().Be("GATE-APROVA");
        regraCongelada.Aprovada.Should().BeTrue();
        regraCongelada.Hash.Should().Be(regra.Hash);
    }

    [Fact(DisplayName = "CA-14 (Retificar): regra legal vigente reprovada recusa ANTES da canonicalização")]
    public async Task Retificar_ComRegraLegalReprovada_RecusaSemCanonicalizar()
    {
        (ProcessoSeletivo processo, VersaoConfiguracao versaoAtual) = ProcessoPublicado();
        IObrigatoriedadeLegalRepository obrigatoriedadeLegalRepository = RepositorioCom(RegraQueReprova());
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
            obrigatoriedadeLegalRepository,
            Substitute.For<IFatoCandidatoReader>(),
            new RelogioFixo(Agora),
            CancellationToken.None);

        resposta.IsFailure.Should().BeTrue();
        resposta.Error!.Code.Should().Be("ProcessoSeletivo.ConformidadeLegalInsuficiente");
        _ = canonicalizer.DidNotReceive().Canonicalizar(Arg.Any<EntradaCanonicalizacao>());
        eventos.Should().BeEmpty();
    }

    [Fact(DisplayName = "CA-14 (FecharRetificacao): regra legal vigente reprovada recusa e a sessão editorial permanece aberta")]
    public async Task FecharRetificacao_ComRegraLegalReprovada_RecusaESessaoPermaneceAberta()
    {
        (ProcessoSeletivo processo, VersaoConfiguracao versaoAtual) = ProcessoPublicado();

        Result<RascunhoRetificacao> rascunho = processo.AbrirRetificacao(
            "Correção do prazo", versaoAtual, "user-sub-1", Agora);
        rascunho.IsSuccess.Should().BeTrue(rascunho.Error?.Message);
        processo.DequeueDomainEvents();

        IObrigatoriedadeLegalRepository obrigatoriedadeLegalRepository = RepositorioCom(RegraQueReprova());
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
            obrigatoriedadeLegalRepository,
            Substitute.For<IFatoCandidatoReader>(),
            new RelogioFixo(Agora),
            CancellationToken.None);

        resposta.IsFailure.Should().BeTrue();
        resposta.Error!.Code.Should().Be("ProcessoSeletivo.ConformidadeLegalInsuficiente");
        _ = canonicalizer.DidNotReceive().Canonicalizar(Arg.Any<EntradaCanonicalizacao>());
        eventos.Should().BeEmpty();
        processo.Rascunho.Should().NotBeNull(
            "uma recusa de conformidade legal não destrói a sessão editorial — o administrador corrige e tenta de novo");
    }

    // ══════════════════════════════════════════════════════════════════════════════

    private static IObrigatoriedadeLegalRepository RepositorioCom(params ObrigatoriedadeLegal[] regras)
    {
        IObrigatoriedadeLegalRepository repositorio = Substitute.For<IObrigatoriedadeLegalRepository>();
        repositorio.ObterVigentesParaTipoProcessoAsync(
            Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(regras);
        return repositorio;
    }

    private static ISnapshotPublicacaoCanonicalizer CanonicalizerSubstituto(Action<EntradaCanonicalizacao>? captura = null)
    {
        ISnapshotPublicacaoCanonicalizer canonicalizer = Substitute.For<ISnapshotPublicacaoCanonicalizer>();
        canonicalizer.Canonicalizar(Arg.Do<EntradaCanonicalizacao>(e => captura?.Invoke(e)))
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

    /// <summary>Processo publicado (para os cenários de Retificar/FecharRetificacao), com a mesma configuração conforme.</summary>
    private static (ProcessoSeletivo Processo, VersaoConfiguracao VersaoAtual) ProcessoPublicado()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
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
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria);

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
                regrasEliminacao: []).Value!,
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
