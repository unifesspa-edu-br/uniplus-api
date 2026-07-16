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
            mocks.PrecedenciaFaseReader,
            mocks.RegraCatalogoReader,
            mocks.TipoAtoPublicadoReader,
            mocks.UnitOfWork,
            mocks.TimeProvider,
            CancellationToken.None);

    private static FaseCanonicaView FaseCanonicaResultado(Guid id) => new(
        id, "RESULTADO_FINAL", "Resultado Final", null, "CEPS",
        AgrupaEtapas: false, PermiteComplementacao: false, BaseLegal: null,
        ProduzResultado: true, ResultadoDefinitivo: true, ColetaInscricao: false, OrigemData: "PROPRIA");

    private static FaseCanonicaView FaseCanonicaRecorrivel(Guid id) => new(
        id, "RESULTADO_PRELIMINAR", "Resultado preliminar", null, "CEPS",
        AgrupaEtapas: false, PermiteComplementacao: false, BaseLegal: null,
        ProduzResultado: true, ResultadoDefinitivo: false, ColetaInscricao: false, OrigemData: "PROPRIA");

    private static FaseCronogramaInput InputResultado(Guid faseCanonicaId) => new(
        Ordem: 1,
        FaseCanonicaId: faseCanonicaId,
        Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
        AtoProduzidoCodigo: "RESULTADO_FINAL",
        TiposBancaIds: [],
        RegraRecurso: null);

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
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna);
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.FaseCanonicaReader.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((FaseCanonicaView?)null);

        DefinirCronogramaFasesCommand command = new(
            processo.Id, [InputResultado(Guid.CreateVersion7())], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FaseCronograma.FaseCanonicaNaoEncontrada");
    }

    [Fact(DisplayName = "Handle com o ato produzido sem versão vigente no catálogo de Publicações retorna FaseCronograma.AtoProduzidoNaoEncontradoNoCatalogo")]
    public async Task Handle_AtoProduzidoSemVersaoVigente_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna);
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
        resultado.Error!.Code.Should().Be("FaseCronograma.AtoProduzidoNaoEncontradoNoCatalogo");
    }

    [Fact(DisplayName = "CA-02/D9: referenciar uma regra de OUTRO TipoRegra em RegraRecurso é recusado com RegraRecursoFase.RegraCatalogoInvalida")]
    public async Task Handle_RegraRecursoDeTipoIncompativel_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna);
        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid faseCanonicaId = Guid.CreateVersion7();
        mocks.FaseCanonicaReader.ObterPorIdAsync(faseCanonicaId, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaResultado(faseCanonicaId));
        mocks.TipoAtoPublicadoReader.ObterVigenteAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView("RESULTADO_FINAL", "Resultado Final", CongelaConfiguracao: false, UnicoPorObjeto: false, EfeitoIrreversivel: false));

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
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna);
        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid faseCanonicaId = Guid.CreateVersion7();
        mocks.FaseCanonicaReader.ObterPorIdAsync(faseCanonicaId, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaRecorrivel(faseCanonicaId));
        mocks.TipoAtoPublicadoReader.ObterVigenteAsync("RESULTADO_PRELIMINAR", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView("RESULTADO_PRELIMINAR", "Resultado preliminar", CongelaConfiguracao: true, UnicoPorObjeto: false, EfeitoIrreversivel: false));

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
            AtoProduzidoCodigo = "RESULTADO_PRELIMINAR",
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
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna);
        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente);

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid faseCanonicaId = Guid.CreateVersion7();
        mocks.FaseCanonicaReader.ObterPorIdAsync(faseCanonicaId, Arg.Any<CancellationToken>())
            .Returns(FaseCanonicaResultado(faseCanonicaId));
        mocks.TipoAtoPublicadoReader.ObterVigenteAsync("RESULTADO_FINAL", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView("RESULTADO_FINAL", "Resultado Final", CongelaConfiguracao: false, UnicoPorObjeto: false, EfeitoIrreversivel: false));

        DefinirCronogramaFasesCommand command = new(
            processo.Id, [InputResultado(faseCanonicaId)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.CronogramaFases.Should().ContainSingle().Which.Codigo.Should().Be("RESULTADO_FINAL");
        await mocks.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
