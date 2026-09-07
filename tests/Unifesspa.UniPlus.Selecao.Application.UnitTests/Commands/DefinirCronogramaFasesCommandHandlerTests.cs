namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

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
        ITipoBancaReader TipoBancaReader,
        ICategoriaDocumentoReader CategoriaDocumentoReader,
        IPrecedenciaFaseReader PrecedenciaFaseReader,
        IRegraCatalogoReader RegraCatalogoReader,
        ITipoAtoPublicadoReader TipoAtoPublicadoReader,
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
            Substitute.For<ITipoBancaReader>(),
            Substitute.For<ICategoriaDocumentoReader>(),
            precedenciaFaseReader,
            Substitute.For<IRegraCatalogoReader>(),
            Substitute.For<ITipoAtoPublicadoReader>(),
            Substitute.For<ISelecaoUnitOfWork>(),
            TimeProvider.System);
    }

    private static Task<Result<MutacaoAceita>> HandleAsync(Mocks mocks, DefinirCronogramaFasesCommand command) =>
        DefinirCronogramaFasesCommandHandler.Handle(
            command,
            mocks.Repository,
            mocks.FaseCanonicaReader,
            mocks.TipoBancaReader,
            mocks.CategoriaDocumentoReader,
            mocks.PrecedenciaFaseReader,
            mocks.RegraCatalogoReader,
            mocks.TipoAtoPublicadoReader,
            mocks.UnitOfWork,
            mocks.TimeProvider,
            CancellationToken.None);

    private static FaseCanonicaView FaseCanonicaResultado(Guid id) => new(
        id, "RESULTADO_FINAL", "Resultado Final", null, "CEPS",
        AgrupaEtapas: false, PermiteComplementacao: false, BaseLegal: null,
        ColetaInscricao: false, ColetaSolicitacaoIsencao: false, OrigemData: "PROPRIA");

    private static FaseCanonicaView FaseCanonicaRecorrivel(Guid id) => new(
        id, "RESULTADO_PRELIMINAR", "Resultado preliminar", null, "CEPS",
        AgrupaEtapas: false, PermiteComplementacao: false, BaseLegal: null,
        ColetaInscricao: false, ColetaSolicitacaoIsencao: false, OrigemData: "PROPRIA");

    private static FaseCronogramaInput InputResultado(Guid faseCanonicaId) => new(
        Ordem: 1,
        FaseCanonicaId: faseCanonicaId,
        Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
        Produtos: [new ProdutoDaFaseInput("RESULTADO_FINAL", PapelProdutoFaseCodigo.Definitivo)],
        FaseConcluinteCodigo: null,
        EmiteParecerIndividual: false,
        BancasRequeridas: [],
        RegraRecurso: null);

    [Fact(DisplayName = "Handle com lista de fases vazia devolve a causa de domínio, não a recusa de forma")]
    public async Task Handle_FasesVazias_DevolveCausaDeDominio()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
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
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.FaseCanonicaReader.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((FaseCanonicaView?)null);

        DefinirCronogramaFasesCommand command = new(
            processo.Id, [InputResultado(Guid.CreateVersion7())], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FaseCronograma.FaseCanonicaNaoEncontrada");
    }

    [Fact(DisplayName = "CA-02: produto cujo tipo de ato não tem versão vigente no catálogo é recusado com ProdutoDaFase.AtoNaoEncontradoNoCatalogo")]
    public async Task Handle_ProdutoSemVersaoVigenteNoCatalogo_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
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
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid faseCanonicaId = Guid.CreateVersion7();
        mocks.FaseCanonicaReader.ObterPorIdAsync(faseCanonicaId, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaResultado(faseCanonicaId));
        mocks.TipoAtoPublicadoReader.ObterVigenteAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView("RESULTADO_FINAL", "Resultado Final", CongelaConfiguracao: false, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: true));

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
            AtoAncoraCodigo: "RESULTADO_FINAL",
            SuspensividadePrimeiraInstanciaValor: null,
            SuspensividadePrimeiraInstanciaUnidade: null,
            SuspensividadeSegundaInstanciaValor: null,
            SuspensividadeSegundaInstanciaUnidade: null);

        FaseCronogramaInput input = InputResultado(faseCanonicaId) with { RegraRecurso = regraRecursoInput };
        DefinirCronogramaFasesCommand command = new(processo.Id, [input], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraRecursoFase.RegraCatalogoInvalida");
    }

    [Fact(DisplayName = "CA-18: âncora cujo tipo de ato CONGELA configuração é recusada com RegraRecursoFase.AncoraEmAtoCongelante")]
    public async Task Handle_AncoraEmAtoCongelante_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid faseCanonicaId = Guid.CreateVersion7();
        mocks.FaseCanonicaReader.ObterPorIdAsync(faseCanonicaId, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaRecorrivel(faseCanonicaId));
        mocks.TipoAtoPublicadoReader.ObterVigenteAsync("RESULTADO_PRELIMINAR", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView("RESULTADO_PRELIMINAR", "Resultado preliminar", CongelaConfiguracao: true, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: true));

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
            AtoAncoraCodigo: "RESULTADO_PRELIMINAR",
            SuspensividadePrimeiraInstanciaValor: null,
            SuspensividadePrimeiraInstanciaUnidade: null,
            SuspensividadeSegundaInstanciaValor: null,
            SuspensividadeSegundaInstanciaUnidade: null);

        FaseCronogramaInput input = InputResultado(faseCanonicaId) with
        {
            Produtos = [new ProdutoDaFaseInput("RESULTADO_PRELIMINAR", PapelProdutoFaseCodigo.Preliminar)],
            RegraRecurso = regraRecursoInput,
        };
        DefinirCronogramaFasesCommand command = new(processo.Id, [input], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraRecursoFase.AncoraEmAtoCongelante");
    }

    [Fact(DisplayName = "Handle com fase conforme resolve e persiste — devolve o ETag da sessão (ou null em rascunho)")]
    public async Task Handle_FaseConforme_PersisteERetornaSucesso()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente);

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid faseCanonicaId = Guid.CreateVersion7();
        mocks.FaseCanonicaReader.ObterPorIdAsync(faseCanonicaId, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaResultado(faseCanonicaId));
        mocks.TipoAtoPublicadoReader.ObterVigenteAsync("RESULTADO_FINAL", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView("RESULTADO_FINAL", "Resultado Final", CongelaConfiguracao: false, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: true));

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
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid faseCanonicaId1 = Guid.CreateVersion7();
        Guid faseCanonicaId2 = Guid.CreateVersion7();
        mocks.FaseCanonicaReader.ObterPorIdAsync(faseCanonicaId1, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaRecorrivel(faseCanonicaId1));
        mocks.FaseCanonicaReader.ObterPorIdAsync(faseCanonicaId2, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaResultado(faseCanonicaId2));
        mocks.TipoAtoPublicadoReader.ObterVigenteAsync("RESULTADO_PRELIMINAR", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView("RESULTADO_PRELIMINAR", "Resultado preliminar", CongelaConfiguracao: false, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: true));
        mocks.TipoAtoPublicadoReader.ObterVigenteAsync("COMUNICADO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView("COMUNICADO", "Comunicado", CongelaConfiguracao: false, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: false));

        // A 2ª fase declara papel num ato que o catálogo não classifica como resultado — o
        // field precisa levar o índice da fase E o do produto dentro dela.
        FaseCronogramaInput fase1 = InputResultado(faseCanonicaId1) with
        {
            Ordem = 1,
            Produtos = [new ProdutoDaFaseInput("RESULTADO_PRELIMINAR", PapelProdutoFaseCodigo.Definitivo)],
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

    [Fact(DisplayName = "ADR-0125: erro sem field próprio (JanelaObrigatoriaEmDataPropria) é prefixado só com fases[i], nunca fica com field null")]
    public async Task Handle_ErroSemFieldProprio_PrefixaComIndiceDaFaseSemDeixarFieldNulo()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid faseCanonicaId = Guid.CreateVersion7();
        mocks.FaseCanonicaReader.ObterPorIdAsync(faseCanonicaId, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaResultado(faseCanonicaId));

        // OrigemData=PROPRIA (via FaseCanonicaResultado) sem Inicio/Fim dispara
        // JanelaObrigatoriaEmDataPropria (sem field próprio — afeta os dois lados da
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
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
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
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid faseCanonicaId = Guid.CreateVersion7();
        mocks.FaseCanonicaReader.ObterPorIdAsync(faseCanonicaId, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaResultado(faseCanonicaId));

        FaseCronogramaInput input = InputResultado(faseCanonicaId) with
        {
            Produtos = [new ProdutoDaFaseInput("RESULTADO_FINAL", "PRELIMINARY")],
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
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
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
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid faseCanonicaId1 = Guid.CreateVersion7();
        Guid faseCanonicaId2 = Guid.CreateVersion7();
        mocks.FaseCanonicaReader.ObterPorIdAsync(faseCanonicaId1, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaResultado(faseCanonicaId1));
        mocks.FaseCanonicaReader.ObterPorIdAsync(faseCanonicaId2, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaResultado(faseCanonicaId2));
        mocks.TipoAtoPublicadoReader.ObterVigenteAsync("RESULTADO_FINAL", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView("RESULTADO_FINAL", "Resultado Final", CongelaConfiguracao: false, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: true));

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
            .ObterVigenteAsync("RESULTADO_FINAL", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    // ── CA-08 — recurso ancorado em ato de efeito irreversível ──

    [Fact(DisplayName = "CA-08: âncora cujo tipo de ato tem efeito IRREVERSÍVEL é recusada com RegraRecursoFase.AncoraEmAtoIrreversivel")]
    public async Task Handle_AncoraEmAtoIrreversivel_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid faseCanonicaId = Guid.CreateVersion7();
        mocks.FaseCanonicaReader.ObterPorIdAsync(faseCanonicaId, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaRecorrivel(faseCanonicaId));
        mocks.TipoAtoPublicadoReader.ObterVigenteAsync("RESULTADO_FINAL", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView("RESULTADO_FINAL", "Resultado final", CongelaConfiguracao: false, UnicoPorObjeto: false, EfeitoIrreversivel: true, EhResultado: true));

        RegraCatalogo regra = RegraCatalogo.Criar(
            RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", TipoRegra.RegraPrazoRecurso,
            JsonDocument.Parse("{}").RootElement, JsonDocument.Parse("[]").RootElement, "Lei 9.784/1999 art. 56").Value!;
        mocks.RegraCatalogoReader.ObterAsync(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", Arg.Any<CancellationToken>())
            .Returns(regra);

        FaseCronogramaInput input = InputResultado(faseCanonicaId) with
        {
            Produtos = [new ProdutoDaFaseInput("RESULTADO_FINAL", PapelProdutoFaseCodigo.Preliminar)],
            RegraRecurso = new RegraRecursoFaseInput(
                RegraCodigo: RegraPrazoRecursoCodigo.AncoradoEmAto,
                RegraVersao: "v1",
                PrazoValor: 48m,
                PrazoUnidade: UnidadePrazo.Horas,
                AtoAncoraCodigo: "RESULTADO_FINAL",
                SuspensividadePrimeiraInstanciaValor: null,
                SuspensividadePrimeiraInstanciaUnidade: null,
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
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid faseCanonicaId = Guid.CreateVersion7();
        mocks.FaseCanonicaReader.ObterPorIdAsync(faseCanonicaId, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaRecorrivel(faseCanonicaId));
        mocks.TipoAtoPublicadoReader.ObterVigenteAsync("RESULTADO_PRELIMINAR", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView("RESULTADO_PRELIMINAR", "Resultado preliminar", CongelaConfiguracao: false, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: true));
        mocks.TipoAtoPublicadoReader.ObterVigenteAsync("RESULTADO_DEFINITIVO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView("RESULTADO_DEFINITIVO", "Resultado definitivo", CongelaConfiguracao: false, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: true));

        RegraCatalogo regra = RegraCatalogo.Criar(
            RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", TipoRegra.RegraPrazoRecurso,
            JsonDocument.Parse("{}").RootElement, JsonDocument.Parse("[]").RootElement, "Lei 9.784/1999 art. 56").Value!;
        mocks.RegraCatalogoReader.ObterAsync(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", Arg.Any<CancellationToken>())
            .Returns(regra);

        FaseCronogramaInput input = InputResultado(faseCanonicaId) with
        {
            Produtos =
            [
                new ProdutoDaFaseInput("RESULTADO_PRELIMINAR", PapelProdutoFaseCodigo.Preliminar),
                new ProdutoDaFaseInput("RESULTADO_DEFINITIVO", PapelProdutoFaseCodigo.Definitivo),
            ],
            RegraRecurso = new RegraRecursoFaseInput(
                RegraCodigo: RegraPrazoRecursoCodigo.AncoradoEmAto,
                RegraVersao: "v1",
                PrazoValor: 48m,
                PrazoUnidade: UnidadePrazo.Horas,
                AtoAncoraCodigo: "RESULTADO_PRELIMINAR",
                SuspensividadePrimeiraInstanciaValor: null,
                SuspensividadePrimeiraInstanciaUnidade: null,
                SuspensividadeSegundaInstanciaValor: null,
                SuspensividadeSegundaInstanciaUnidade: null),
        };
        DefinirCronogramaFasesCommand command = new(processo.Id, [input], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.CronogramaFases.Should().ContainSingle()
            .Which.Produtos.Should().HaveCount(2);
    }

    [Fact(DisplayName = "CA-01: duas fases que publicam o MESMO tipo de ato ancoram cada uma no produto da sua própria fase")]
    public async Task Handle_DuasFasesComOMesmoAtoAncora_ResolveCadaAncoraNaPropriaFase()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
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
        mocks.TipoAtoPublicadoReader.ObterVigenteAsync("RESULTADO_PRELIMINAR", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView("RESULTADO_PRELIMINAR", "Resultado preliminar", CongelaConfiguracao: false, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: true));
        mocks.TipoAtoPublicadoReader.ObterVigenteAsync("RESULTADO_FINAL", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView("RESULTADO_FINAL", "Resultado final", CongelaConfiguracao: false, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: true));
        mocks.RegraCatalogoReader.ObterAsync(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", Arg.Any<CancellationToken>())
            .Returns(RegraCatalogo.Criar(
                RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", TipoRegra.RegraPrazoRecurso,
                JsonDocument.Parse("{}").RootElement, JsonDocument.Parse("[]").RootElement, "Lei 9.784/1999 art. 56").Value!);

        RegraRecursoFaseInput recurso = new(
            RegraCodigo: RegraPrazoRecursoCodigo.AncoradoEmAto,
            RegraVersao: "v1",
            PrazoValor: 48m,
            PrazoUnidade: UnidadePrazo.Horas,
            AtoAncoraCodigo: "RESULTADO_PRELIMINAR",
            SuspensividadePrimeiraInstanciaValor: null,
            SuspensividadePrimeiraInstanciaUnidade: null,
            SuspensividadeSegundaInstanciaValor: null,
            SuspensividadeSegundaInstanciaUnidade: null);

        // As duas primeiras fases publicam RESULTADO_PRELIMINAR e admitem recurso — a
        // situação observada em homologação, em que o código do ato sozinho não diz de qual
        // das duas publicações o prazo do candidato conta.
        DefinirCronogramaFasesCommand command = new(
            processo.Id,
            [
                InputResultado(biopsicossocialId) with
                {
                    Ordem = 1,
                    Produtos = [new ProdutoDaFaseInput("RESULTADO_PRELIMINAR", PapelProdutoFaseCodigo.Preliminar)],
                    FaseConcluinteCodigo = "RESULTADO_FINAL",
                    RegraRecurso = recurso,
                },
                InputResultado(preliminarId) with
                {
                    Ordem = 2,
                    Produtos = [new ProdutoDaFaseInput("RESULTADO_PRELIMINAR", PapelProdutoFaseCodigo.Preliminar)],
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
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid faseCanonicaId = Guid.CreateVersion7();
        Guid tipoBancaId = Guid.CreateVersion7();
        Guid categoriaViva = Guid.CreateVersion7();
        Guid categoriaRemovida = Guid.CreateVersion7();

        mocks.FaseCanonicaReader.ObterPorIdAsync(faseCanonicaId, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaResultado(faseCanonicaId));
        mocks.TipoAtoPublicadoReader.ObterVigenteAsync("RESULTADO_FINAL", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView("RESULTADO_FINAL", "Resultado final", CongelaConfiguracao: false, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: true));
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
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
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
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid faseCanonicaId = Guid.CreateVersion7();
        mocks.FaseCanonicaReader.ObterPorIdAsync(faseCanonicaId, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaResultado(faseCanonicaId));
        mocks.TipoAtoPublicadoReader.ObterVigenteAsync("RESULTADO_FINAL", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView("RESULTADO_FINAL", "Resultado Final", CongelaConfiguracao: false, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: true));
        mocks.RegraCatalogoReader.ObterAsync("REGRA_INEXISTENTE", "v1", Arg.Any<CancellationToken>())
            .Returns((RegraCatalogo?)null);

        FaseCronogramaInput input = InputResultado(faseCanonicaId) with
        {
            RegraRecurso = new RegraRecursoFaseInput(
                RegraCodigo: "REGRA_INEXISTENTE",
                RegraVersao: "v1",
                PrazoValor: 48m,
                PrazoUnidade: UnidadePrazo.Horas,
                AtoAncoraCodigo: "RESULTADO_FINAL",
                SuspensividadePrimeiraInstanciaValor: null,
                SuspensividadePrimeiraInstanciaUnidade: null,
                SuspensividadeSegundaInstanciaValor: null,
                SuspensividadeSegundaInstanciaUnidade: null),
        };
        DefinirCronogramaFasesCommand command = new(processo.Id, [input], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("fases[0].regraRecurso.regraCodigo");
    }
}
