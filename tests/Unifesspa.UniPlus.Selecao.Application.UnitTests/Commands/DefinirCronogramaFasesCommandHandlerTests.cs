namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Cobertura do <see cref="DefinirCronogramaFasesCommandHandler"/> (Story #851): a
/// resolução das dependências cross-módulo (FaseCanonica/TipoBanca/precedência —
/// Configuração; tipo de ato — Publicações; regra — <c>rol_de_regras</c>) e os erros
/// nomeados que cada resolução malsucedida produz.
/// </summary>
public sealed class DefinirCronogramaFasesCommandHandlerTests
{
    private sealed record Mocks(
        IProcessoSeletivoRepository Repository,
        IFaseCanonicaReader FaseCanonicaReader,
        IResolvedorDaUnidadeResponsavel UnidadeResponsavel,
        ITipoBancaReader TipoBancaReader,
        ICategoriaDocumentoReader CategoriaDocumentoReader,
        IPrecedenciaFaseReader PrecedenciaFaseReader,
        IRegraCatalogoReader RegraCatalogoReader,
        ITipoAtoPublicadoReader TipoAtoPublicadoReader,
        IRegistroCodecsEnvelope Codecs,
        ISelecaoUnitOfWork UnitOfWork,
        TimeProvider TimeProvider);

    private static Mocks NovosMocks(ProcessoSeletivo? processo, Guid processoId)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processoId, Arg.Any<CancellationToken>()).Returns(processo);

        IPrecedenciaFaseReader precedenciaFaseReader = Substitute.For<IPrecedenciaFaseReader>();
        precedenciaFaseReader.ListarVivasAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<PrecedenciaFaseView>)[]);

        return new Mocks(
            repository,
            Substitute.For<IFaseCanonicaReader>(),
            Substitute.For<IResolvedorDaUnidadeResponsavel>(),
            Substitute.For<ITipoBancaReader>(),
            Substitute.For<ICategoriaDocumentoReader>(),
            precedenciaFaseReader,
            Substitute.For<IRegraCatalogoReader>(),
            Substitute.For<ITipoAtoPublicadoReader>(),
            Substitute.For<IRegistroCodecsEnvelope>(),
            Substitute.For<ISelecaoUnitOfWork>(),
            TimeProvider.System);
    }

    private static Task<Result<MutacaoAceita>> HandleAsync(Mocks mocks, DefinirCronogramaFasesCommand command) =>
        DefinirCronogramaFasesCommandHandler.Handle(
            command,
            mocks.Repository,
            mocks.FaseCanonicaReader,
            mocks.UnidadeResponsavel,
            mocks.TipoBancaReader,
            mocks.CategoriaDocumentoReader,
            mocks.PrecedenciaFaseReader,
            mocks.RegraCatalogoReader,
            mocks.TipoAtoPublicadoReader,
            mocks.Codecs,
            mocks.UnitOfWork,
            mocks.TimeProvider,
            CancellationToken.None);

    private static FaseCanonicaView FaseCanonicaResultado(Guid id) => new(
        id, "RESULTADO_FINAL", "Resultado Final", null,
        AgrupaEtapas: false, PermiteComplementacao: false, BaseLegal: null,
        ColetaInscricao: false, ColetaSolicitacaoIsencao: false, OrigemData: "NO_EDITAL");

    private static FaseCanonicaView FaseCanonicaRecorrivel(Guid id) => new(
        id, "RESULTADO_PRELIMINAR", "Resultado preliminar", null,
        AgrupaEtapas: false, PermiteComplementacao: false, BaseLegal: null,
        ColetaInscricao: false, ColetaSolicitacaoIsencao: false, OrigemData: "NO_EDITAL");

    private static FaseCronogramaInput InputResultado(Guid faseCanonicaId) => new(
        Ordem: 1,
        FaseCanonicaId: faseCanonicaId,
        Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
        Produtos: [new ProdutoDaFaseInput("RESULTADO_CLASSIFICACAO", PapelProdutoFaseCodigo.Definitivo)],
        FaseConcluinteCodigo: null,
        EmiteParecerIndividual: false,
        BancasRequeridas: [],
        RegraRecurso: null);

    [Fact(DisplayName = "Handle com lista de fases vazia devolve a causa de domínio, não a recusa de forma")]
    public async Task Handle_FasesVazias_DevolveCausaDeDominio()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirCronogramaFasesCommand command = new(processo.Id, [], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.CronogramaFasesVazio",
            "a regra de forma no validator tornava esta causa inalcançável por um cliente HTTP");
    }

    [Fact(DisplayName = "Handle com processo inexistente retorna ProcessoSeletivo.NaoEncontrado")]
    public async Task Handle_ProcessoInexistente_RetornaNaoEncontrado()
    {
        Mocks mocks = NovosMocks(null, Guid.CreateVersion7());
        DefinirCronogramaFasesCommand command = new(Guid.CreateVersion7(), [], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Handle com FaseCanonicaId que não resolve no cadastro retorna FaseCronograma.FaseCanonicaNaoEncontrada")]
    public async Task Handle_FaseCanonicaNaoEncontrada_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.FaseCanonicaReader.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((FaseCanonicaView?)null);

        DefinirCronogramaFasesCommand command = new(
            processo.Id, [InputResultado(Guid.CreateVersion7())], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FaseCronograma.FaseCanonicaNaoEncontrada");
    }

    [Fact(DisplayName = "A unidade responsável informada é resolvida no cadastro e gravada na fase, com id, sigla e nome")]
    public async Task Handle_UnidadeResponsavelInformada_GravaOSnapshotNaFase()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid faseCanonicaId = Guid.CreateVersion7();
        mocks.FaseCanonicaReader.ObterPorIdAsync(faseCanonicaId, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaResultado(faseCanonicaId));
        mocks.TipoAtoPublicadoReader.ObterVigenteAsync("RESULTADO_CLASSIFICACAO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView("RESULTADO_CLASSIFICACAO", "Resultado da classificação", CongelaConfiguracao: false, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: true));
        mocks.UnidadeResponsavel.ResolverAsync(UnidadesDeTeste.Cga.OrigemId, Arg.Any<CancellationToken>())
            .Returns(UnidadesDeTeste.Cga);

        DefinirCronogramaFasesCommand command = new(
            processo.Id,
            [InputResultado(faseCanonicaId) with { UnidadeResponsavelId = UnidadesDeTeste.Cga.OrigemId }],
            PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.CronogramaFases.Single().UnidadeResponsavel.Should().Be(UnidadesDeTeste.Cga);
    }

    [Fact(DisplayName = "O rascunho sem unidade responsável não consulta o cadastro e grava a fase sem ela")]
    public async Task Handle_SemUnidadeResponsavel_NaoConsultaOCadastroEAceita()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid faseCanonicaId = Guid.CreateVersion7();
        mocks.FaseCanonicaReader.ObterPorIdAsync(faseCanonicaId, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaResultado(faseCanonicaId));
        mocks.TipoAtoPublicadoReader.ObterVigenteAsync("RESULTADO_CLASSIFICACAO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView("RESULTADO_CLASSIFICACAO", "Resultado da classificação", CongelaConfiguracao: false, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: true));

        DefinirCronogramaFasesCommand command = new(
            processo.Id, [InputResultado(faseCanonicaId)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.CronogramaFases.Single().UnidadeResponsavel.Should().BeNull();
        await mocks.UnidadeResponsavel.DidNotReceiveWithAnyArgs().ResolverAsync(default, default);
    }

    [Fact(DisplayName = "No rascunho, a unidade gravada é resolvida de novo no cadastro, e a que saiu dele é recusada")]
    public async Task Handle_RascunhoComUnidadeGravadaQueSaiuDoCadastro_Recusa()
    {
        (ProcessoSeletivo processo, Guid faseCanonicaId) = ProcessoComFaseGravada(publicado: false);
        Mocks mocks = MocksDoProcessoComFaseGravada(processo, faseCanonicaId);
        mocks.UnidadeResponsavel.ResolverAsync(UnidadesDeTeste.Cga.OrigemId, Arg.Any<CancellationToken>())
            .Returns((UnidadeResponsavelSnapshot?)null);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, ComandoDaFaseGravada(processo, faseCanonicaId));

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be("FaseCronograma.UnidadeResponsavelNaoEncontrada");
    }

    [Fact(DisplayName = "Na retificação, a unidade que saiu do cadastro é recusada citando a fase, como no rascunho")]
    public async Task Handle_RetificacaoComUnidadeQueSaiuDoCadastro_Recusa()
    {
        (ProcessoSeletivo processo, Guid faseCanonicaId) = ProcessoComFaseGravada(publicado: true);
        Mocks mocks = MocksDoProcessoComFaseGravada(processo, faseCanonicaId);
        mocks.UnidadeResponsavel.ResolverAsync(UnidadesDeTeste.Cga.OrigemId, Arg.Any<CancellationToken>())
            .Returns((UnidadeResponsavelSnapshot?)null);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, ComandoDaFaseGravada(processo, faseCanonicaId));

        resultado.IsFailure.Should().BeTrue();
        FieldError recusa = resultado.Errors.Should().ContainSingle().Subject;
        recusa.Field.Should().Be("fases[0].unidadeResponsavelId");
        recusa.Error.Code.Should().Be("FaseCronograma.UnidadeResponsavelNaoEncontrada");
    }

    private static (ProcessoSeletivo Processo, Guid FaseCanonicaId) ProcessoComFaseGravada(bool publicado)
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS unidade gravada", out _);
        FaseCronograma conforme = processo.CronogramaFases.Single();
        FaseCronograma comCga = FaseCronograma.Criar(
            conforme.Ordem, conforme.FaseCanonicaOrigemId, conforme.Codigo, conforme.OrigemData,
            conforme.AgrupaEtapas, conforme.PermiteComplementacao, conforme.ColetaInscricao, conforme.ColetaSolicitacaoIsencao,
            conforme.Inicio, conforme.Fim, [.. conforme.Produtos], conforme.FaseConcluinteCodigo, conforme.EmiteParecerIndividual,
            [.. conforme.BancasRequeridas], conforme.RegraRecurso, UnidadesDeTeste.Cga).Value!;
        processo.DefinirCronogramaFases([comCga], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        if (publicado)
        {
            DadosEdital dados = DadosEdital.Criar(
                "001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
                new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)), Guid.CreateVersion7()).Value!;
            Result<VersaoConfiguracao> versao = processo.Publicar(
                dados, EnvelopeCongeladoDeTeste.SemAnexos(), "1.1", "canonical-json/sha256@v1", ProcessoSeletivoConformeBuilder.HashFixo, "user-sub-1",
                TimeProvider.System, ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);
            versao.IsSuccess.Should().BeTrue(versao.Error?.Message);
            processo.AbrirRetificacao(
                "Correção do cronograma", versao.Value!, identificadorDaVersaoBase: null,
                VersoesPublicadasDeTeste.SoAVigente(processo), "user-sub-1", DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();
        }

        return (processo, conforme.FaseCanonicaOrigemId);
    }

    private static Mocks MocksDoProcessoComFaseGravada(ProcessoSeletivo processo, Guid faseCanonicaId)
    {
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.FaseCanonicaReader.ObterPorIdAsync(faseCanonicaId, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaResultado(faseCanonicaId));
        mocks.TipoAtoPublicadoReader.ObterVigenteAsync("RESULTADO_CLASSIFICACAO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView("RESULTADO_CLASSIFICACAO", "Resultado da classificação", CongelaConfiguracao: false, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: true));
        return mocks;
    }

    private static DefinirCronogramaFasesCommand ComandoDaFaseGravada(ProcessoSeletivo processo, Guid faseCanonicaId) => new(
        processo.Id,
        [InputResultado(faseCanonicaId) with { UnidadeResponsavelId = UnidadesDeTeste.Cga.OrigemId }],
        PrecondicaoIfMatch.Curinga);

    [Fact(DisplayName = "Duas fases sob a mesma unidade nova resolvem o cadastro uma vez só")]
    public async Task Handle_MesmaUnidadeEmDuasFases_ResolveUmaVez()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid primeira = Guid.CreateVersion7();
        Guid segunda = Guid.CreateVersion7();
        mocks.FaseCanonicaReader.ObterPorIdAsync(primeira, Arg.Any<CancellationToken>()).Returns(FaseCanonicaResultado(primeira));
        mocks.FaseCanonicaReader.ObterPorIdAsync(segunda, Arg.Any<CancellationToken>()).Returns(FaseCanonicaRecorrivel(segunda));
        mocks.UnidadeResponsavel.ResolverAsync(UnidadesDeTeste.Cga.OrigemId, Arg.Any<CancellationToken>())
            .Returns(UnidadesDeTeste.Cga);

        FaseCronogramaInput SemProdutos(int ordem, Guid faseCanonicaId) => InputResultado(faseCanonicaId) with
        {
            Ordem = ordem,
            Produtos = [],
            UnidadeResponsavelId = UnidadesDeTeste.Cga.OrigemId,
        };

        await HandleAsync(mocks, new DefinirCronogramaFasesCommand(
            processo.Id, [SemProdutos(1, primeira), SemProdutos(2, segunda)], PrecondicaoIfMatch.Ausente));

        await mocks.UnidadeResponsavel.Received(1).ResolverAsync(UnidadesDeTeste.Cga.OrigemId, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Unidade responsável que não existe no cadastro é recusada no campo da fase, citando a fase")]
    public async Task Handle_UnidadeResponsavelNaoEncontrada_RecusaNoCampoDaFase()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid faseCanonicaId = Guid.CreateVersion7();
        Guid unidadeExcluida = Guid.CreateVersion7();
        mocks.FaseCanonicaReader.ObterPorIdAsync(faseCanonicaId, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaResultado(faseCanonicaId));
        mocks.UnidadeResponsavel.ResolverAsync(unidadeExcluida, Arg.Any<CancellationToken>())
            .Returns((UnidadeResponsavelSnapshot?)null);

        DefinirCronogramaFasesCommand command = new(
            processo.Id,
            [InputResultado(faseCanonicaId) with { UnidadeResponsavelId = unidadeExcluida }],
            PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        FieldError recusa = resultado.Errors.Should().ContainSingle().Subject;
        recusa.Field.Should().Be("fases[0].unidadeResponsavelId");
        recusa.Error.Code.Should().Be("FaseCronograma.UnidadeResponsavelNaoEncontrada");
        recusa.Error.Message.Should().Contain("RESULTADO_FINAL");
        processo.CronogramaFases.Should().BeEmpty("a recusa não grava o cronograma");
    }

    [Fact(DisplayName = "CA-02: produto cujo tipo de ato não tem versão vigente no catálogo é recusado com ProdutoDaFase.AtoNaoEncontradoNoCatalogo")]
    public async Task Handle_ProdutoSemVersaoVigenteNoCatalogo_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid faseCanonicaId = Guid.CreateVersion7();
        mocks.FaseCanonicaReader.ObterPorIdAsync(faseCanonicaId, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaResultado(faseCanonicaId));
        mocks.TipoAtoPublicadoReader.ObterVigenteAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((TipoAtoPublicadoView?)null);

        DefinirCronogramaFasesCommand command = new(
            processo.Id, [InputResultado(faseCanonicaId)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle()
            .Which.Should().Match<FieldError>(e =>
                e.Field == "fases[0].produtos[0].atoCodigo"
                && e.Error.Code == "ProdutoDaFase.AtoNaoEncontradoNoCatalogo");
    }

    [Fact(DisplayName = "CA-02/D9: referenciar uma regra de OUTRO TipoRegra em RegraRecurso é recusado com RegraRecursoFase.RegraCatalogoInvalida")]
    public async Task Handle_RegraRecursoDeTipoIncompativel_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid faseCanonicaId = Guid.CreateVersion7();
        mocks.FaseCanonicaReader.ObterPorIdAsync(faseCanonicaId, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaResultado(faseCanonicaId));
        mocks.TipoAtoPublicadoReader.ObterVigenteAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView("RESULTADO_CLASSIFICACAO", "Resultado da classificação", CongelaConfiguracao: false, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: true));

        // Regra existe, mas é de outro TipoRegra (regra_bonus, não regra_prazo_recurso).
        RegraCatalogo regraErrada = RegraCatalogo.Criar(
            "BONUS-MULTIPLICATIVO", "v1", TipoRegra.RegraBonus,
            JsonDocument.Parse("{}").RootElement, JsonDocument.Parse("[]").RootElement, "base legal").Value!;
        mocks.RegraCatalogoReader.ObterAsync("BONUS-MULTIPLICATIVO", "v1", Arg.Any<CancellationToken>())
            .Returns(regraErrada);

        RegraRecursoFaseInput regraRecursoInput = new(
            RegraCodigo: "BONUS-MULTIPLICATIVO",
            RegraVersao: "v1",
            PrazoValor: 48m,
            PrazoUnidade: UnidadePrazo.Horas,
            AtoAncoraCodigo: "RESULTADO_CLASSIFICACAO",
            SuspensividadePrimeiraInstanciaValor: null,
            SuspensividadePrimeiraInstanciaUnidade: null,
            AdmiteInstanciaSuperior: false,
            SuspensividadeSegundaInstanciaValor: null,
            SuspensividadeSegundaInstanciaUnidade: null);

        FaseCronogramaInput input = InputResultado(faseCanonicaId) with { RegraRecurso = regraRecursoInput };
        DefinirCronogramaFasesCommand command = new(processo.Id, [input], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraRecursoFase.RegraCatalogoInvalida");
    }

    [Fact(DisplayName = "CA-18: âncora cujo tipo de ato CONGELA configuração é recusada com ProcessoSeletivo.TipoQueCongelaDeclaradoComoResultado")]
    public async Task Handle_AncoraEmAtoCongelante_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid faseCanonicaId = Guid.CreateVersion7();
        mocks.FaseCanonicaReader.ObterPorIdAsync(faseCanonicaId, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaRecorrivel(faseCanonicaId));
        mocks.TipoAtoPublicadoReader.ObterVigenteAsync("RESULTADO_CLASSIFICACAO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView("RESULTADO_CLASSIFICACAO", "Resultado da classificação", CongelaConfiguracao: true, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: true));

        RegraCatalogo regra = RegraCatalogo.Criar(
            RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", TipoRegra.RegraPrazoRecurso,
            JsonDocument.Parse("{}").RootElement, JsonDocument.Parse("[]").RootElement, "Lei 9.784/1999 art. 56").Value!;
        mocks.RegraCatalogoReader.ObterAsync(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", Arg.Any<CancellationToken>())
            .Returns(regra);

        RegraRecursoFaseInput regraRecursoInput = new(
            RegraCodigo: RegraPrazoRecursoCodigo.AncoradoEmAto,
            RegraVersao: "v1",
            PrazoValor: 48m,
            PrazoUnidade: UnidadePrazo.Horas,
            AtoAncoraCodigo: "RESULTADO_CLASSIFICACAO",
            SuspensividadePrimeiraInstanciaValor: null,
            SuspensividadePrimeiraInstanciaUnidade: null,
            AdmiteInstanciaSuperior: false,
            SuspensividadeSegundaInstanciaValor: null,
            SuspensividadeSegundaInstanciaUnidade: null);

        FaseCronogramaInput input = InputResultado(faseCanonicaId) with
        {
            Produtos = [new ProdutoDaFaseInput("RESULTADO_CLASSIFICACAO", PapelProdutoFaseCodigo.Preliminar)],
            RegraRecurso = regraRecursoInput,
        };
        DefinirCronogramaFasesCommand command = new(processo.Id, [input], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.TipoQueCongelaDeclaradoComoResultado");
    }

    [Fact(DisplayName = "Tipo já declarado na versão publicada vale pela classificação congelada: o catálogo que passou a congelar não recusa a edição")]
    public async Task Handle_TipoJaDeclaradoQueOCatalogoPassouACongelar_AceitaPelaClassificacaoCongelada()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);
        VersaoPublicadaDeTeste.Publicar(
            processo, mocks.Repository, mocks.Codecs,
            new TipoDeAtoCongelado("RESULTADO_CLASSIFICACAO", UnicoPorObjeto: false, EhResultado: true, CongelaConfiguracao: false));
        Guid faseCanonicaId = Guid.CreateVersion7();
        mocks.FaseCanonicaReader.ObterPorIdAsync(faseCanonicaId, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaResultado(faseCanonicaId));
        mocks.TipoAtoPublicadoReader.ObterVigenteAsync("RESULTADO_CLASSIFICACAO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView("RESULTADO_CLASSIFICACAO", "Resultado da classificação", CongelaConfiguracao: true, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: true));

        FaseCronogramaInput input = InputResultado(faseCanonicaId) with
        {
            Produtos = [new ProdutoDaFaseInput("RESULTADO_CLASSIFICACAO", PapelProdutoFaseCodigo.Definitivo)],
        };

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, new DefinirCronogramaFasesCommand(processo.Id, [input], PrecondicaoIfMatch.Ausente));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Tipo declarado pela primeira vez continua conferido pelo catálogo, mesmo com versão publicada")]
    public async Task Handle_TipoNovoEmProcessoPublicado_ConfereOCatalogo()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);
        VersaoPublicadaDeTeste.Publicar(
            processo, mocks.Repository, mocks.Codecs,
            new TipoDeAtoCongelado("OUTRO_ATO", UnicoPorObjeto: false, EhResultado: false, CongelaConfiguracao: false));
        Guid faseCanonicaId = Guid.CreateVersion7();
        mocks.FaseCanonicaReader.ObterPorIdAsync(faseCanonicaId, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaResultado(faseCanonicaId));
        mocks.TipoAtoPublicadoReader.ObterVigenteAsync("RESULTADO_CLASSIFICACAO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView("RESULTADO_CLASSIFICACAO", "Resultado da classificação", CongelaConfiguracao: true, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: true));

        FaseCronogramaInput input = InputResultado(faseCanonicaId) with
        {
            Produtos = [new ProdutoDaFaseInput("RESULTADO_CLASSIFICACAO", PapelProdutoFaseCodigo.Definitivo)],
        };

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, new DefinirCronogramaFasesCommand(processo.Id, [input], PrecondicaoIfMatch.Ausente));

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be("ProcessoSeletivo.TipoQueCongelaDeclaradoComoResultado");
    }

    [Fact(DisplayName = "Versão publicada que o codec não lê é recusada com a falha do codec, sem cair no catálogo")]
    public async Task Handle_VersaoPublicadaIlegivel_RecusaComAFalhaDoCodec()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);
        VersaoPublicadaDeTeste.PublicarIlegivel(
            processo, mocks.Repository, mocks.Codecs, new DomainError("Envelope.CodecDesconhecido", "O codec não lê esta versão."));
        Guid faseCanonicaId = Guid.CreateVersion7();
        mocks.FaseCanonicaReader.ObterPorIdAsync(faseCanonicaId, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaResultado(faseCanonicaId));
        mocks.TipoAtoPublicadoReader.ObterVigenteAsync("RESULTADO_CLASSIFICACAO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView("RESULTADO_CLASSIFICACAO", "Resultado da classificação", CongelaConfiguracao: false, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: true));

        FaseCronogramaInput input = InputResultado(faseCanonicaId) with
        {
            Produtos = [new ProdutoDaFaseInput("RESULTADO_CLASSIFICACAO", PapelProdutoFaseCodigo.Definitivo)],
        };

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, new DefinirCronogramaFasesCommand(processo.Id, [input], PrecondicaoIfMatch.Ausente));

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be("Envelope.CodecDesconhecido");
    }

    [Theory(DisplayName = "Com o tipo fora do catálogo hoje, a âncora nova é recusada e a que o agregado já tinha segue aceita")]
    [InlineData(false, "ProdutoDaFase.AtoNaoEncontradoNoCatalogo")]
    [InlineData(true, null)]
    public async Task Handle_AncoraEmTipoSemVersaoVigenteNoCatalogo_RecusaSoANovaEmRelacaoAoAgregado(bool ancoraJaNoAgregado, string? codigoEsperado)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid faseCanonicaId = Guid.CreateVersion7();
        mocks.FaseCanonicaReader.ObterPorIdAsync(faseCanonicaId, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaRecorrivel(faseCanonicaId));
        mocks.RegraCatalogoReader.ObterAsync(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", Arg.Any<CancellationToken>())
            .Returns(RegraCatalogo.Criar(
                RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", TipoRegra.RegraPrazoRecurso,
                JsonDocument.Parse("{}").RootElement, JsonDocument.Parse("[]").RootElement, "Lei 9.784/1999 art. 56").Value!);
        FaseCronogramaInput semAncora = InputResultado(faseCanonicaId) with
        {
            Produtos =
            [
                new ProdutoDaFaseInput("RESULTADO_CLASSIFICACAO", PapelProdutoFaseCodigo.Preliminar),
                new ProdutoDaFaseInput("RESULTADO_CLASSIFICACAO", PapelProdutoFaseCodigo.Definitivo),
            ],
        };
        FaseCronogramaInput comAncora = semAncora with
        {
            RegraRecurso = new RegraRecursoFaseInput(
                RegraCodigo: RegraPrazoRecursoCodigo.AncoradoEmAto,
                RegraVersao: "v1",
                PrazoValor: 48m,
                PrazoUnidade: UnidadePrazo.Horas,
                AtoAncoraCodigo: "RESULTADO_CLASSIFICACAO",
                SuspensividadePrimeiraInstanciaValor: null,
                SuspensividadePrimeiraInstanciaUnidade: null,
                AdmiteInstanciaSuperior: false,
                SuspensividadeSegundaInstanciaValor: null,
                SuspensividadeSegundaInstanciaUnidade: null),
        };

        mocks.TipoAtoPublicadoReader.ObterVigenteAsync("RESULTADO_CLASSIFICACAO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView("RESULTADO_CLASSIFICACAO", "Resultado da classificação", CongelaConfiguracao: false, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: true));
        (await HandleAsync(mocks, new DefinirCronogramaFasesCommand(processo.Id, [ancoraJaNoAgregado ? comAncora : semAncora], PrecondicaoIfMatch.Ausente)))
            .IsSuccess.Should().BeTrue();
        VersaoPublicadaDeTeste.Publicar(
            processo, mocks.Repository, mocks.Codecs,
            new TipoDeAtoCongelado("RESULTADO_CLASSIFICACAO", UnicoPorObjeto: false, EhResultado: true, CongelaConfiguracao: false));

        // O catálogo deixa de ter versão vigente do tipo: a irreversibilidade dele passa a ser desconhecida.
        mocks.TipoAtoPublicadoReader.ObterVigenteAsync("RESULTADO_CLASSIFICACAO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((TipoAtoPublicadoView?)null);
        Result<MutacaoAceita> resultado = await HandleAsync(mocks, new DefinirCronogramaFasesCommand(processo.Id, [comAncora], PrecondicaoIfMatch.Ausente));

        if (codigoEsperado is null)
        {
            resultado.IsSuccess.Should().BeTrue(resultado.Errors.Count > 0 ? resultado.Errors[0].Error.Message : resultado.Error?.Message);
        }
        else
        {
            resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(codigoEsperado);
        }
    }

    [Fact(DisplayName = "A âncora que só a versão publicada tem segue aceita quando o catálogo perde o tipo e o agregado não a tem mais")]
    public async Task Handle_AncoraSoNaVersaoPublicada_CatalogoSemOTipo_Aceita()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid faseCanonicaId = Guid.CreateVersion7();
        mocks.FaseCanonicaReader.ObterPorIdAsync(faseCanonicaId, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaRecorrivel(faseCanonicaId));
        mocks.RegraCatalogoReader.ObterAsync(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", Arg.Any<CancellationToken>())
            .Returns(RegraCatalogo.Criar(
                RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", TipoRegra.RegraPrazoRecurso,
                JsonDocument.Parse("{}").RootElement, JsonDocument.Parse("[]").RootElement, "Lei 9.784/1999 art. 56").Value!);
        FaseCronogramaInput semAncora = InputResultado(faseCanonicaId) with
        {
            Produtos =
            [
                new ProdutoDaFaseInput("RESULTADO_CLASSIFICACAO", PapelProdutoFaseCodigo.Preliminar),
                new ProdutoDaFaseInput("RESULTADO_CLASSIFICACAO", PapelProdutoFaseCodigo.Definitivo),
            ],
        };
        FaseCronogramaInput comAncora = semAncora with
        {
            RegraRecurso = new RegraRecursoFaseInput(
                RegraCodigo: RegraPrazoRecursoCodigo.AncoradoEmAto,
                RegraVersao: "v1",
                PrazoValor: 48m,
                PrazoUnidade: UnidadePrazo.Horas,
                AtoAncoraCodigo: "RESULTADO_CLASSIFICACAO",
                SuspensividadePrimeiraInstanciaValor: null,
                SuspensividadePrimeiraInstanciaUnidade: null,
                AdmiteInstanciaSuperior: false,
                SuspensividadeSegundaInstanciaValor: null,
                SuspensividadeSegundaInstanciaUnidade: null),
        };
        mocks.TipoAtoPublicadoReader.ObterVigenteAsync("RESULTADO_CLASSIFICACAO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView("RESULTADO_CLASSIFICACAO", "Resultado da classificação", CongelaConfiguracao: false, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: true));

        // A versão publicada tem a âncora. O grafo vem de uma instância à parte, porque a gravação seguinte
        // reconcilia a fase no lugar.
        ProcessoSeletivo publicado = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        IProcessoSeletivoRepository repositorioDoPublicado = Substitute.For<IProcessoSeletivoRepository>();
        repositorioDoPublicado.ObterParaMutacaoAsync(publicado.Id, Arg.Any<CancellationToken>()).Returns(publicado);
        (await HandleAsync(mocks with { Repository = repositorioDoPublicado }, new DefinirCronogramaFasesCommand(publicado.Id, [comAncora], PrecondicaoIfMatch.Ausente)))
            .IsSuccess.Should().BeTrue();
        (await HandleAsync(mocks, new DefinirCronogramaFasesCommand(processo.Id, [comAncora], PrecondicaoIfMatch.Ausente)))
            .IsSuccess.Should().BeTrue();
        VersaoPublicadaDeTeste.Publicar(
            processo, publicado, mocks.Repository, mocks.Codecs,
            new TipoDeAtoCongelado("RESULTADO_CLASSIFICACAO", UnicoPorObjeto: false, EhResultado: true, CongelaConfiguracao: false));
        (await HandleAsync(mocks, new DefinirCronogramaFasesCommand(processo.Id, [semAncora], PrecondicaoIfMatch.Ausente)))
            .IsSuccess.Should().BeTrue();

        mocks.TipoAtoPublicadoReader.ObterVigenteAsync("RESULTADO_CLASSIFICACAO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((TipoAtoPublicadoView?)null);
        Result<MutacaoAceita> resultado = await HandleAsync(mocks, new DefinirCronogramaFasesCommand(processo.Id, [comAncora], PrecondicaoIfMatch.Ausente));

        resultado.IsSuccess.Should().BeTrue(resultado.Errors.Count > 0 ? resultado.Errors[0].Error.Message : resultado.Error?.Message);
    }

    [Fact(DisplayName = "Na sessão de retificação, a âncora declarada numa edição anterior segue aceita quando o catálogo perde o tipo")]
    public async Task Handle_AncoraDeclaradaNaSessao_CatalogoSemOTipo_Aceita()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid faseCanonicaId = Guid.CreateVersion7();
        mocks.FaseCanonicaReader.ObterPorIdAsync(faseCanonicaId, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaRecorrivel(faseCanonicaId));
        mocks.RegraCatalogoReader.ObterAsync(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", Arg.Any<CancellationToken>())
            .Returns(RegraCatalogo.Criar(
                RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", TipoRegra.RegraPrazoRecurso,
                JsonDocument.Parse("{}").RootElement, JsonDocument.Parse("[]").RootElement, "Lei 9.784/1999 art. 56").Value!);
        FaseCronogramaInput comAncora = InputResultado(faseCanonicaId) with
        {
            Produtos =
            [
                new ProdutoDaFaseInput("RESULTADO_CLASSIFICACAO", PapelProdutoFaseCodigo.Preliminar),
                new ProdutoDaFaseInput("RESULTADO_CLASSIFICACAO", PapelProdutoFaseCodigo.Definitivo),
            ],
            RegraRecurso = new RegraRecursoFaseInput(
                RegraCodigo: RegraPrazoRecursoCodigo.AncoradoEmAto,
                RegraVersao: "v1",
                PrazoValor: 48m,
                PrazoUnidade: UnidadePrazo.Horas,
                AtoAncoraCodigo: "RESULTADO_CLASSIFICACAO",
                SuspensividadePrimeiraInstanciaValor: null,
                SuspensividadePrimeiraInstanciaUnidade: null,
                AdmiteInstanciaSuperior: false,
                SuspensividadeSegundaInstanciaValor: null,
                SuspensividadeSegundaInstanciaUnidade: null),
        };

        // A versão publicada congelou o tipo, mas ainda não tinha a âncora: ela entra na sessão.
        VersaoPublicadaDeTeste.Publicar(
            processo, mocks.Repository, mocks.Codecs,
            new TipoDeAtoCongelado("RESULTADO_CLASSIFICACAO", UnicoPorObjeto: false, EhResultado: true, CongelaConfiguracao: false));
        mocks.TipoAtoPublicadoReader.ObterVigenteAsync("RESULTADO_CLASSIFICACAO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView("RESULTADO_CLASSIFICACAO", "Resultado da classificação", CongelaConfiguracao: false, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: true));
        Result<MutacaoAceita> primeira = await HandleAsync(mocks, new DefinirCronogramaFasesCommand(processo.Id, [comAncora], PrecondicaoIfMatch.Ausente));
        primeira.IsSuccess.Should().BeTrue(primeira.Error?.Message);

        mocks.TipoAtoPublicadoReader.ObterVigenteAsync("RESULTADO_CLASSIFICACAO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((TipoAtoPublicadoView?)null);
        Result<MutacaoAceita> resultado = await HandleAsync(mocks, new DefinirCronogramaFasesCommand(processo.Id, [comAncora], PrecondicaoIfMatch.Ausente));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Tipo classificado como resultado declarado sem papel pela fase é recusado com ProcessoSeletivo.ResultadoDeclaradoSemPapel")]
    public async Task Handle_ResultadoSemPapel_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid faseCanonicaId = Guid.CreateVersion7();
        mocks.FaseCanonicaReader.ObterPorIdAsync(faseCanonicaId, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaResultado(faseCanonicaId));
        mocks.TipoAtoPublicadoReader.ObterVigenteAsync("RESULTADO_CLASSIFICACAO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView("RESULTADO_CLASSIFICACAO", "Resultado da classificação", CongelaConfiguracao: false, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: true));

        FaseCronogramaInput input = InputResultado(faseCanonicaId) with
        {
            Produtos = [new ProdutoDaFaseInput("RESULTADO_CLASSIFICACAO", null)],
        };
        DefinirCronogramaFasesCommand command = new(processo.Id, [input], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(e =>
            e.Field == "fases[0].produtos[0].papel"
            && e.Error.Code == "ProcessoSeletivo.ResultadoDeclaradoSemPapel");
    }

    [Fact(DisplayName = "Handle com fase conforme resolve e persiste — devolve o ETag da sessão (ou null em rascunho)")]
    public async Task Handle_FaseConforme_PersisteERetornaSucesso()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente);

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid faseCanonicaId = Guid.CreateVersion7();
        mocks.FaseCanonicaReader.ObterPorIdAsync(faseCanonicaId, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaResultado(faseCanonicaId));
        mocks.TipoAtoPublicadoReader.ObterVigenteAsync("RESULTADO_CLASSIFICACAO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView("RESULTADO_CLASSIFICACAO", "Resultado da classificação", CongelaConfiguracao: false, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: true));

        DefinirCronogramaFasesCommand command = new(
            processo.Id, [InputResultado(faseCanonicaId)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.CronogramaFases.Should().ContainSingle().Which.Codigo.Should().Be("RESULTADO_FINAL");
        await mocks.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "ADR-0125: recusa de produto na SEGUNDA fase do payload carrega o índice da fase e o do item no field")]
    public async Task Handle_SegundaFaseComPapelEmAtoQueNaoEhResultado_PrefixaIndiceNoField()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid faseCanonicaId1 = Guid.CreateVersion7();
        Guid faseCanonicaId2 = Guid.CreateVersion7();
        mocks.FaseCanonicaReader.ObterPorIdAsync(faseCanonicaId1, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaRecorrivel(faseCanonicaId1));
        mocks.FaseCanonicaReader.ObterPorIdAsync(faseCanonicaId2, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaResultado(faseCanonicaId2));
        mocks.TipoAtoPublicadoReader.ObterVigenteAsync("RESULTADO_CLASSIFICACAO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView("RESULTADO_CLASSIFICACAO", "Resultado da classificação", CongelaConfiguracao: false, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: true));
        mocks.TipoAtoPublicadoReader.ObterVigenteAsync("COMUNICADO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView("COMUNICADO", "Comunicado", CongelaConfiguracao: false, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: false));

        // A 2ª fase declara papel num ato que o catálogo não classifica como resultado — o
        // field precisa levar o índice da fase E o do produto dentro dela.
        FaseCronogramaInput fase1 = InputResultado(faseCanonicaId1) with
        {
            Ordem = 1,
            Produtos = [new ProdutoDaFaseInput("RESULTADO_CLASSIFICACAO", PapelProdutoFaseCodigo.Definitivo)],
        };
        FaseCronogramaInput fase2 = InputResultado(faseCanonicaId2) with
        {
            Ordem = 2,
            Produtos = [new ProdutoDaFaseInput("COMUNICADO", PapelProdutoFaseCodigo.Definitivo)],
        };
        DefinirCronogramaFasesCommand command = new(processo.Id, [fase1, fase2], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Field).Should().BeEquivalentTo(["fases[1].produtos[0].papel"]);
        resultado.Errors.Should().ContainSingle()
            .Which.Error.Code.Should().Be("ProdutoDaFase.PapelEmAtoQueNaoEhResultado");
    }

    [Fact(DisplayName = "ADR-0125: erro sem field próprio (JanelaObrigatoriaEmDataNoEdital) é prefixado só com fases[i], nunca fica com field null")]
    public async Task Handle_ErroSemFieldProprio_PrefixaComIndiceDaFaseSemDeixarFieldNulo()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid faseCanonicaId = Guid.CreateVersion7();
        mocks.FaseCanonicaReader.ObterPorIdAsync(faseCanonicaId, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaResultado(faseCanonicaId));

        // OrigemData=NO_EDITAL (via FaseCanonicaResultado) sem Inicio/Fim dispara
        // JanelaObrigatoriaEmDataNoEdital (sem field próprio — afeta os dois lados da
        // janela); a promessa de parecer individual sem produto nenhum dispara
        // ParecerIndividualSemResultado (field "emiteParecerIndividual") na MESMA fase —
        // os dois precisam ficar rastreáveis à fase de índice 0, mesmo o que não tem field
        // de campo específico.
        FaseCronogramaInput fase = InputResultado(faseCanonicaId) with
        {
            Inicio = null,
            Fim = null,
            Produtos = [],
            EmiteParecerIndividual = true,
        };
        DefinirCronogramaFasesCommand command = new(processo.Id, [fase], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Field).Should().BeEquivalentTo(["fases[0]", "fases[0].emiteParecerIndividual"]);
        resultado.Errors.Should().NotContain(e => e.Field == null);
    }

    // ── CA-02 — o papel só cabe em ato que o catálogo classifica como resultado ──

    [Fact(DisplayName = "CA-02 (contraprova): produto SEM papel num ato que não é resultado é aceito")]
    public async Task Handle_ProdutoSemPapelEmAtoQueNaoEhResultado_Aceita()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid faseCanonicaId = Guid.CreateVersion7();
        mocks.FaseCanonicaReader.ObterPorIdAsync(faseCanonicaId, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaResultado(faseCanonicaId));
        mocks.TipoAtoPublicadoReader.ObterVigenteAsync("COMUNICADO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView("COMUNICADO", "Comunicado", CongelaConfiguracao: false, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: false));

        FaseCronogramaInput input = InputResultado(faseCanonicaId) with
        {
            Produtos = [new ProdutoDaFaseInput("COMUNICADO", null)],
        };
        DefinirCronogramaFasesCommand command = new(processo.Id, [input], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.CronogramaFases.Should().ContainSingle()
            .Which.ProduzResultado.Should().BeFalse("publicar aviso não torna a fase produtora de resultado");
    }

    [Fact(DisplayName = "CA-02: papel fora do vocabulário é recusado com ProdutoDaFase.PapelDesconhecido, sem virar produto sem papel")]
    public async Task Handle_PapelForaDoVocabulario_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid faseCanonicaId = Guid.CreateVersion7();
        mocks.FaseCanonicaReader.ObterPorIdAsync(faseCanonicaId, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaResultado(faseCanonicaId));

        FaseCronogramaInput input = InputResultado(faseCanonicaId) with
        {
            Produtos = [new ProdutoDaFaseInput("RESULTADO_CLASSIFICACAO", "PRELIMINARY")],
        };
        DefinirCronogramaFasesCommand command = new(processo.Id, [input], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue(
            "aceitar o token desconhecido como ausência de papel transformaria erro de digitação em fase que deixa de produzir resultado, em silêncio");
        resultado.Errors.Should().ContainSingle()
            .Which.Should().Match<FieldError>(e =>
                e.Field == "fases[0].produtos[0].papel"
                && e.Error.Code == "ProdutoDaFase.PapelDesconhecido");
    }

    [Fact(DisplayName = "ADR-0125: dois produtos com problemas distintos acumulam as duas recusas, cada uma com o índice do item")]
    public async Task Handle_DoisProdutosComProblemasDistintos_AcumulaAsDuasRecusas()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid faseCanonicaId = Guid.CreateVersion7();
        mocks.FaseCanonicaReader.ObterPorIdAsync(faseCanonicaId, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaResultado(faseCanonicaId));
        mocks.TipoAtoPublicadoReader.ObterVigenteAsync("COMUNICADO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView("COMUNICADO", "Comunicado", CongelaConfiguracao: false, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: false));
        mocks.TipoAtoPublicadoReader.ObterVigenteAsync("ATO_INEXISTENTE", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((TipoAtoPublicadoView?)null);

        FaseCronogramaInput input = InputResultado(faseCanonicaId) with
        {
            Produtos =
            [
                new ProdutoDaFaseInput("COMUNICADO", PapelProdutoFaseCodigo.Preliminar),
                new ProdutoDaFaseInput("ATO_INEXISTENTE", null),
            ],
        };
        DefinirCronogramaFasesCommand command = new(processo.Id, [input], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Field).Should().BeEquivalentTo(
            ["fases[0].produtos[0].papel", "fases[0].produtos[1].atoCodigo"],
            "uma coleção mal declarada erra em vários itens ao mesmo tempo, e devolver só o primeiro faria o operador descobrir os demais numa sequência de tentativas");
    }

    [Fact(DisplayName = "O catálogo é lido UMA vez por código, mesmo quando duas fases publicam o mesmo ato")]
    public async Task Handle_MesmoAtoEmDuasFases_ResolveOCatalogoUmaVez()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid faseCanonicaId1 = Guid.CreateVersion7();
        Guid faseCanonicaId2 = Guid.CreateVersion7();
        mocks.FaseCanonicaReader.ObterPorIdAsync(faseCanonicaId1, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaResultado(faseCanonicaId1));
        mocks.FaseCanonicaReader.ObterPorIdAsync(faseCanonicaId2, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaResultado(faseCanonicaId2));
        mocks.TipoAtoPublicadoReader.ObterVigenteAsync("RESULTADO_CLASSIFICACAO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView("RESULTADO_CLASSIFICACAO", "Resultado da classificação", CongelaConfiguracao: false, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: true));

        DefinirCronogramaFasesCommand command = new(
            processo.Id,
            [
                InputResultado(faseCanonicaId1) with { Ordem = 1 },
                InputResultado(faseCanonicaId2) with { Ordem = 2 },
            ],
            PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        await mocks.TipoAtoPublicadoReader.Received(1)
            .ObterVigenteAsync("RESULTADO_CLASSIFICACAO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    // ── CA-08 — recurso ancorado em ato de efeito irreversível ──

    [Fact(DisplayName = "CA-08: âncora cujo tipo de ato tem efeito IRREVERSÍVEL é recusada com RegraRecursoFase.AncoraEmAtoIrreversivel")]
    public async Task Handle_AncoraEmAtoIrreversivel_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid faseCanonicaId = Guid.CreateVersion7();
        mocks.FaseCanonicaReader.ObterPorIdAsync(faseCanonicaId, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaRecorrivel(faseCanonicaId));
        mocks.TipoAtoPublicadoReader.ObterVigenteAsync("RESULTADO_CLASSIFICACAO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView("RESULTADO_CLASSIFICACAO", "Resultado da classificação", CongelaConfiguracao: false, UnicoPorObjeto: false, EfeitoIrreversivel: true, EhResultado: true));

        RegraCatalogo regra = RegraCatalogo.Criar(
            RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", TipoRegra.RegraPrazoRecurso,
            JsonDocument.Parse("{}").RootElement, JsonDocument.Parse("[]").RootElement, "Lei 9.784/1999 art. 56").Value!;
        mocks.RegraCatalogoReader.ObterAsync(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", Arg.Any<CancellationToken>())
            .Returns(regra);

        FaseCronogramaInput input = InputResultado(faseCanonicaId) with
        {
            Produtos = [new ProdutoDaFaseInput("RESULTADO_CLASSIFICACAO", PapelProdutoFaseCodigo.Preliminar)],
            RegraRecurso = new RegraRecursoFaseInput(
                RegraCodigo: RegraPrazoRecursoCodigo.AncoradoEmAto,
                RegraVersao: "v1",
                PrazoValor: 48m,
                PrazoUnidade: UnidadePrazo.Horas,
                AtoAncoraCodigo: "RESULTADO_CLASSIFICACAO",
                SuspensividadePrimeiraInstanciaValor: null,
                SuspensividadePrimeiraInstanciaUnidade: null,
                AdmiteInstanciaSuperior: false,
                SuspensividadeSegundaInstanciaValor: null,
                SuspensividadeSegundaInstanciaUnidade: null),
        };
        DefinirCronogramaFasesCommand command = new(processo.Id, [input], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraRecursoFase.AncoraEmAtoIrreversivel",
            "o ato que encerra a matéria não se desfaz, e o recurso cabível é contra o resultado que o fundamenta");
    }

    [Fact(DisplayName = "CA-08 (contraprova): âncora em ato reversível e não congelante é aceita")]
    public async Task Handle_AncoraEmAtoReversivel_Aceita()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid faseCanonicaId = Guid.CreateVersion7();
        mocks.FaseCanonicaReader.ObterPorIdAsync(faseCanonicaId, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaRecorrivel(faseCanonicaId));
        mocks.TipoAtoPublicadoReader.ObterVigenteAsync("RESULTADO_CLASSIFICACAO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView("RESULTADO_CLASSIFICACAO", "Resultado da classificação", CongelaConfiguracao: false, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: true));

        RegraCatalogo regra = RegraCatalogo.Criar(
            RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", TipoRegra.RegraPrazoRecurso,
            JsonDocument.Parse("{}").RootElement, JsonDocument.Parse("[]").RootElement, "Lei 9.784/1999 art. 56").Value!;
        mocks.RegraCatalogoReader.ObterAsync(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", Arg.Any<CancellationToken>())
            .Returns(regra);

        FaseCronogramaInput input = InputResultado(faseCanonicaId) with
        {
            Produtos =
            [
                new ProdutoDaFaseInput("RESULTADO_CLASSIFICACAO", PapelProdutoFaseCodigo.Preliminar),
                new ProdutoDaFaseInput("RESULTADO_CLASSIFICACAO", PapelProdutoFaseCodigo.Definitivo),
            ],
            RegraRecurso = new RegraRecursoFaseInput(
                RegraCodigo: RegraPrazoRecursoCodigo.AncoradoEmAto,
                RegraVersao: "v1",
                PrazoValor: 48m,
                PrazoUnidade: UnidadePrazo.Horas,
                AtoAncoraCodigo: "RESULTADO_CLASSIFICACAO",
                SuspensividadePrimeiraInstanciaValor: null,
                SuspensividadePrimeiraInstanciaUnidade: null,
                AdmiteInstanciaSuperior: true,
                SuspensividadeSegundaInstanciaValor: null,
                SuspensividadeSegundaInstanciaUnidade: null),
        };
        DefinirCronogramaFasesCommand command = new(processo.Id, [input], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.CronogramaFases.Should().ContainSingle()
            .Which.Produtos.Should().HaveCount(2);
        processo.CronogramaFases.Single().RegraRecurso!.Args.AdmiteInstanciaSuperior.Should().BeTrue(
            "a declaração é da janela, e o handler a leva do payload, não a fixa");
    }

    [Fact(DisplayName = "CA-01: duas fases que publicam o MESMO tipo de ato ancoram cada uma no produto da sua própria fase")]
    public async Task Handle_DuasFasesComOMesmoAtoAncora_ResolveCadaAncoraNaPropriaFase()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid biopsicossocialId = Guid.CreateVersion7();
        Guid preliminarId = Guid.CreateVersion7();
        Guid finalId = Guid.CreateVersion7();
        mocks.FaseCanonicaReader.ObterPorIdAsync(biopsicossocialId, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaRecorrivel(biopsicossocialId) with { Codigo = "AVALIACAO_BIOPSICOSSOCIAL" });
        mocks.FaseCanonicaReader.ObterPorIdAsync(preliminarId, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaRecorrivel(preliminarId));
        mocks.FaseCanonicaReader.ObterPorIdAsync(finalId, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaResultado(finalId));
        mocks.TipoAtoPublicadoReader.ObterVigenteAsync("RESULTADO_CLASSIFICACAO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView("RESULTADO_CLASSIFICACAO", "Resultado da classificação", CongelaConfiguracao: false, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: true));
        mocks.TipoAtoPublicadoReader.ObterVigenteAsync("RESULTADO_CLASSIFICACAO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView("RESULTADO_CLASSIFICACAO", "Resultado da classificação", CongelaConfiguracao: false, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: true));
        mocks.RegraCatalogoReader.ObterAsync(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", Arg.Any<CancellationToken>())
            .Returns(RegraCatalogo.Criar(
                RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", TipoRegra.RegraPrazoRecurso,
                JsonDocument.Parse("{}").RootElement, JsonDocument.Parse("[]").RootElement, "Lei 9.784/1999 art. 56").Value!);

        RegraRecursoFaseInput recurso = new(
            RegraCodigo: RegraPrazoRecursoCodigo.AncoradoEmAto,
            RegraVersao: "v1",
            PrazoValor: 48m,
            PrazoUnidade: UnidadePrazo.Horas,
            AtoAncoraCodigo: "RESULTADO_CLASSIFICACAO",
            SuspensividadePrimeiraInstanciaValor: null,
            SuspensividadePrimeiraInstanciaUnidade: null,
            AdmiteInstanciaSuperior: false,
            SuspensividadeSegundaInstanciaValor: null,
            SuspensividadeSegundaInstanciaUnidade: null);

        // As duas primeiras fases publicam RESULTADO_CLASSIFICACAO e admitem recurso — a
        // situação observada em homologação, em que o código do ato sozinho não diz de qual
        // das duas publicações o prazo do candidato conta.
        DefinirCronogramaFasesCommand command = new(
            processo.Id,
            [
                InputResultado(biopsicossocialId) with
                {
                    Ordem = 1,
                    Produtos = [new ProdutoDaFaseInput("RESULTADO_CLASSIFICACAO", PapelProdutoFaseCodigo.Preliminar)],
                    FaseConcluinteCodigo = "RESULTADO_FINAL",
                    RegraRecurso = recurso,
                },
                InputResultado(preliminarId) with
                {
                    Ordem = 2,
                    Produtos = [new ProdutoDaFaseInput("RESULTADO_CLASSIFICACAO", PapelProdutoFaseCodigo.Preliminar)],
                    FaseConcluinteCodigo = "RESULTADO_FINAL",
                    RegraRecurso = recurso,
                },
                InputResultado(finalId) with { Ordem = 3 },
            ],
            PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);

        FaseCronograma[] recorriveis = [.. processo.CronogramaFases
            .Where(f => f.RegraRecurso is not null)
            .OrderBy(f => f.Ordem)];
        recorriveis.Should().HaveCount(2);
        foreach (FaseCronograma fase in recorriveis)
        {
            fase.RegraRecurso!.ProdutoAncoraId.Should().Be(fase.Produtos.Single().Id,
                "cada regra resolve para o produto da SUA fase, e nenhuma para a publicação da outra");
        }

        recorriveis[0].RegraRecurso!.ProdutoAncoraId.Should()
            .NotBe(recorriveis[1].RegraRecurso!.ProdutoAncoraId);
    }

    [Fact(DisplayName = "Handle com categoria de documento fora do cadastro recusa nomeando o recorte da banca")]
    public async Task Handle_CategoriaDoRecorteForaDoCadastro_RecusaComErroNomeado()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid faseCanonicaId = Guid.CreateVersion7();
        Guid tipoBancaId = Guid.CreateVersion7();
        Guid categoriaViva = Guid.CreateVersion7();
        Guid categoriaRemovida = Guid.CreateVersion7();

        mocks.FaseCanonicaReader.ObterPorIdAsync(faseCanonicaId, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaResultado(faseCanonicaId));
        mocks.TipoAtoPublicadoReader.ObterVigenteAsync("RESULTADO_CLASSIFICACAO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView("RESULTADO_CLASSIFICACAO", "Resultado da classificação", CongelaConfiguracao: false, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: true));
        mocks.TipoBancaReader.ObterPorIdAsync(tipoBancaId, Arg.Any<CancellationToken>())
            .Returns(new TipoBancaView(tipoBancaId, "BANCA_ANALISE_DOCUMENTAL", "Banca de análise documental", null, null));
        mocks.CategoriaDocumentoReader.ObterPorIdAsync(categoriaViva, Arg.Any<CancellationToken>())
            .Returns(new CategoriaDocumentoView(categoriaViva, "RENDA", "Renda", null, 0));
        mocks.CategoriaDocumentoReader.ObterPorIdAsync(categoriaRemovida, Arg.Any<CancellationToken>())
            .Returns((CategoriaDocumentoView?)null);

        FaseCronogramaInput fase = InputResultado(faseCanonicaId) with
        {
            BancasRequeridas = [new BancaRequeridaInput(tipoBancaId, [categoriaViva, categoriaRemovida])],
        };
        DefinirCronogramaFasesCommand command = new(processo.Id, [fase], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle()
            .Which.Should().Match<FieldError>(e =>
                e.Error.Code == "FaseCronograma.CategoriaDocumentoNaoEncontrada"
                && e.Field == "fases[0].bancasRequeridas[0].categoriasDocumentoIds");
        processo.CronogramaFases.Should().BeEmpty("uma categoria fora do cadastro não pode entrar no edital congelado");
    }

    [Fact(DisplayName = "A parada numa resolução cross-módulo LEVA JUNTO as recusas de produto já acumuladas")]
    public async Task Handle_ParadaCrossModulo_PreservaRecusasAcumuladas()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid faseCanonicaId1 = Guid.CreateVersion7();
        Guid faseCanonicaId2 = Guid.CreateVersion7();
        mocks.FaseCanonicaReader.ObterPorIdAsync(faseCanonicaId1, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaResultado(faseCanonicaId1));
        mocks.FaseCanonicaReader.ObterPorIdAsync(faseCanonicaId2, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaResultado(faseCanonicaId2));
        mocks.TipoAtoPublicadoReader.ObterVigenteAsync("COMUNICADO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView("COMUNICADO", "Comunicado", CongelaConfiguracao: false, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: false));
        mocks.TipoBancaReader.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((TipoBancaView?)null);

        // A 1ª fase erra o papel de um produto; a 2ª referencia um tipo de banca morto — a
        // segunda interrompe a passada, e a primeira já tinha diagnóstico produzido.
        FaseCronogramaInput fase1 = InputResultado(faseCanonicaId1) with
        {
            Ordem = 1,
            Produtos = [new ProdutoDaFaseInput("COMUNICADO", PapelProdutoFaseCodigo.Definitivo)],
        };
        FaseCronogramaInput fase2 = InputResultado(faseCanonicaId2) with
        {
            Ordem = 2,
            BancasRequeridas = [new BancaRequeridaInput(Guid.CreateVersion7(), [])],
        };
        DefinirCronogramaFasesCommand command = new(processo.Id, [fase1, fase2], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
            ["ProdutoDaFase.PapelEmAtoQueNaoEhResultado", "FaseCronograma.TipoBancaNaoEncontrado"],
            "parar na primeira resolução cross-módulo é decisão sobre o que ainda dá para avaliar, " +
            "não licença para apagar defeito já diagnosticado");
        resultado.Errors.Select(e => e.Field).Should().BeEquivalentTo(
            ["fases[0].produtos[0].papel", "fases[1].bancasRequeridas[0].tipoBancaId"]);
        resultado.Errors.Should().NotContain(e => e.Field == null,
            "errors[] é montado sobre TODOS os itens do lote assim que um deles tem campo — " +
            "um FieldError sem campo sairia no wire como field: null, que a ADR-0023 não admite");
    }

    [Fact(DisplayName = "A interrupção cross-módulo carrega o campo que a localiza, mesmo sem recusa acumulada junto")]
    public async Task Handle_InterrupcaoCrossModulo_CarregaOCampoQueALocaliza()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid faseCanonicaId = Guid.CreateVersion7();
        mocks.FaseCanonicaReader.ObterPorIdAsync(faseCanonicaId, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaResultado(faseCanonicaId));
        mocks.TipoAtoPublicadoReader.ObterVigenteAsync("RESULTADO_CLASSIFICACAO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView("RESULTADO_CLASSIFICACAO", "Resultado da classificação", CongelaConfiguracao: false, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: true));
        mocks.RegraCatalogoReader.ObterAsync("REGRA_INEXISTENTE", "v1", Arg.Any<CancellationToken>())
            .Returns((RegraCatalogo?)null);

        FaseCronogramaInput input = InputResultado(faseCanonicaId) with
        {
            RegraRecurso = new RegraRecursoFaseInput(
                RegraCodigo: "REGRA_INEXISTENTE",
                RegraVersao: "v1",
                PrazoValor: 48m,
                PrazoUnidade: UnidadePrazo.Horas,
                AtoAncoraCodigo: "RESULTADO_CLASSIFICACAO",
                SuspensividadePrimeiraInstanciaValor: null,
                SuspensividadePrimeiraInstanciaUnidade: null,
                AdmiteInstanciaSuperior: false,
                SuspensividadeSegundaInstanciaValor: null,
                SuspensividadeSegundaInstanciaUnidade: null),
        };
        DefinirCronogramaFasesCommand command = new(processo.Id, [input], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("fases[0].regraRecurso.regraCodigo");
    }
}
