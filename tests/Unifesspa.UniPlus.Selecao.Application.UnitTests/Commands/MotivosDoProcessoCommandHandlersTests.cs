namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDoProcesso;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.MotivoDeTeste;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.PublicacaoDeTeste;

/// <summary>
/// Inclusão e retirada de motivo numa fase ou etapa do processo (UNI-REQ-0122, UNI-REQ-0285): o que o handler carrega
/// e grava, e o que ele só lê quando a sessão de retificação está aberta.
/// </summary>
public sealed class MotivosDoProcessoCommandHandlersTests
{
    private readonly ProcessoSeletivo _processo = NovoProcessoConforme();
    private readonly IProcessoSeletivoRepository _processos = Substitute.For<IProcessoSeletivoRepository>();
    private readonly IMotivoDeDecisaoRepository _cadastro = Substitute.For<IMotivoDeDecisaoRepository>();
    private readonly IMotivoDoProcessoRepository _escolhidos = Substitute.For<IMotivoDoProcessoRepository>();
    private readonly IFasesIniciadasDoProcesso _fasesIniciadas = FasesIniciadasDeTeste.Nenhuma();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    public MotivosDoProcessoCommandHandlersTests()
    {
        _processos.ObterParaMutacaoAsync(_processo.Id, Arg.Any<CancellationToken>()).Returns(_processo);
        _escolhidos.ListarEscolhaAtualAsync(_processo.Id, Arg.Any<CancellationToken>()).Returns([]);
    }

    private LugarDaDecisao Lugar => LugarDaPrimeiraFase(_processo);

    [Fact(DisplayName = "Incluir no rascunho copia o motivo do cadastro no lugar e grava, sem ler o que já começou")]
    public async Task Incluir_NoRascunho_Grava()
    {
        MotivoDeDecisao origem = Motivo();
        _cadastro.ObterPorIdAsync(origem.Id, Arg.Any<CancellationToken>()).Returns(origem);

        Result<MutacaoAceita> resultado = await Incluir(origem.Id);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        await _escolhidos.Received(1).AdicionarAsync(
            Arg.Is<MotivoDoProcesso>(m => m.MotivoDeDecisaoOrigemId == origem.Id && m.Lugar == Lugar && m.ProcessoSeletivoId == _processo.Id),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        await _fasesIniciadas.DidNotReceiveWithAnyArgs().ObterAgoraAsync(default, default);
    }

    [Fact(DisplayName = "Incluir em processo inexistente devolve não encontrado")]
    public async Task Incluir_ProcessoInexistente_NaoEncontrado()
    {
        Result<MutacaoAceita> resultado = await IncluirMotivoNoProcessoCommandHandler.Handle(
            new IncluirMotivoNoProcessoCommand(Guid.CreateVersion7(), Lugar, Guid.CreateVersion7(), null, PrecondicaoIfMatch.Ausente),
            _processos, _cadastro, _escolhidos, _fasesIniciadas, _unitOfWork, CancellationToken.None);

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Incluir motivo inexistente no cadastro devolve não encontrado")]
    public async Task Incluir_MotivoInexistente_NaoEncontrado() =>
        (await Incluir(Guid.CreateVersion7())).Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.NaoEncontrado);

    [Fact(DisplayName = "Incluir numa fase que o processo não tem devolve fase ou etapa não encontrada e não grava")]
    public async Task Incluir_LugarInexistente_NaoGrava()
    {
        MotivoDeDecisao origem = Motivo();
        _cadastro.ObterPorIdAsync(origem.Id, Arg.Any<CancellationToken>()).Returns(origem);

        Result<MutacaoAceita> resultado = await Incluir(origem.Id, LugarDaDecisao.DaFase(Guid.CreateVersion7()));

        resultado.Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.FaseOuEtapaNaoEncontrada);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A recusa do domínio descarta o rastreamento e não grava")]
    public async Task Incluir_MotivoInativo_DescartaSemGravar()
    {
        MotivoDeDecisao origem = Motivo();
        origem.Desativar();
        _cadastro.ObterPorIdAsync(origem.Id, Arg.Any<CancellationToken>()).Returns(origem);

        Result<MutacaoAceita> resultado = await Incluir(origem.Id);

        resultado.Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.MotivoInativo);
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A escolha repetida em corrida volta como conflito, com o rastreamento descartado")]
    public async Task Incluir_ConflitoAoGravar_DevolveConflito()
    {
        MotivoDeDecisao origem = Motivo();
        _cadastro.ObterPorIdAsync(origem.Id, Arg.Any<CancellationToken>()).Returns(origem);
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new DbUpdateConcurrencyException("conflito sintético"));

        Result<MutacaoAceita> resultado = await Incluir(origem.Id);

        resultado.Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.SituacaoAlteradaConcorrentemente);
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "A escolha repetida em corrida, que o índice da escolha vigente recusa, volta como conflito retentável")]
    public async Task Incluir_EscolhaRepetidaEmCorrida_DevolveConflito()
    {
        MotivoDeDecisao origem = Motivo();
        _cadastro.ObterPorIdAsync(origem.Id, Arg.Any<CancellationToken>()).Returns(origem);
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(ExcecaoDeBancoDeTeste.ViolacaoDeUnicidade("ux_motivos_do_processo_vigente"));

        Result<MutacaoAceita> resultado = await Incluir(origem.Id);

        resultado.Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.SituacaoAlteradaConcorrentemente);
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "A violação de outro índice único não é engolida como conflito da escolha")]
    public async Task Incluir_ViolacaoDeOutroIndice_Propaga()
    {
        MotivoDeDecisao origem = Motivo();
        _cadastro.ObterPorIdAsync(origem.Id, Arg.Any<CancellationToken>()).Returns(origem);
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(ExcecaoDeBancoDeTeste.ViolacaoDeUnicidade("ux_outro_indice"));

        Func<Task> incluindo = () => Incluir(origem.Id);

        await incluindo.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact(DisplayName = "Na retificação, a avaliação que já começou no lugar recusa a inclusão")]
    public async Task Incluir_NaRetificacao_AvaliacaoIniciada_Recusa()
    {
        ProcessoSeletivo processo = ProcessoEmRetificacao();
        FasesIniciadas iniciadas = new([], null, null, [LugarDaPrimeiraFase(processo).FaseCanonicaOrigemId!.Value]);

        Result<MutacaoAceita> resultado = await IncluirNaRetificacao(processo, iniciadas);

        resultado.Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.AvaliacaoIniciada);
    }

    [Fact(DisplayName = "Na retificação, a inclusão no lugar em que a avaliação não começou grava")]
    public async Task Incluir_NaRetificacao_AvaliacaoNaoIniciada_Grava()
    {
        ProcessoSeletivo processo = ProcessoEmRetificacao();

        Result<MutacaoAceita> resultado = await IncluirNaRetificacao(processo, FasesIniciadas.Nenhuma);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Retirar motivo escolhido no rascunho remove e grava")]
    public async Task Retirar_Escolhido_Remove()
    {
        MotivoDoProcesso escolhido = Escolhido(_processo, Lugar, Motivo());
        _escolhidos.ListarEscolhaAtualAsync(_processo.Id, Arg.Any<CancellationToken>()).Returns([escolhido]);

        Result<MutacaoAceita> resultado = await Retirar(escolhido.MotivoDeDecisaoOrigemId);

        resultado.IsSuccess.Should().BeTrue();
        _escolhidos.Received(1).Remover(escolhido);
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Retirar motivo que o processo não escolheu no lugar devolve não escolhido")]
    public async Task Retirar_NaoEscolhido_Recusa() =>
        (await Retirar(Guid.CreateVersion7())).Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.NaoEscolhido);

    [Fact(DisplayName = "Retirar de lugar que o processo não tem devolve fase ou etapa não encontrada")]
    public async Task Retirar_LugarInexistente_Recusa() =>
        (await Retirar(Guid.CreateVersion7(), LugarDaDecisao.DaEtapa(Guid.CreateVersion7()))).Error!.Code
            .Should().Be(MotivoDeDecisaoErrorCodes.FaseOuEtapaNaoEncontrada);

    [Fact(DisplayName = "Retirar motivo que já consta de versão publicada é recusado sem remover")]
    public async Task Retirar_JaPublicado_Recusa()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        MotivoDoProcesso escolhido = Escolhido(processo, LugarDaPrimeiraFase(processo), Motivo());
        AbrirRetificacao(processo, Publicar(processo));
        Registrar(processo, escolhido);

        Result<MutacaoAceita> resultado = await RetirarMotivoDoProcessoCommandHandler.Handle(
            new RetirarMotivoDoProcessoCommand(processo.Id, escolhido.Lugar, escolhido.MotivoDeDecisaoOrigemId, PrecondicaoIfMatch.Curinga),
            _processos, _escolhidos, _unitOfWork, CancellationToken.None);

        resultado.Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.RetiradaAposPublicacao);
        _escolhidos.DidNotReceive().Remover(Arg.Any<MotivoDoProcesso>());
    }

    [Fact(DisplayName = "Retirar de processo inexistente devolve não encontrado")]
    public async Task Retirar_ProcessoInexistente_NaoEncontrado()
    {
        Result<MutacaoAceita> resultado = await RetirarMotivoDoProcessoCommandHandler.Handle(
            new RetirarMotivoDoProcessoCommand(Guid.CreateVersion7(), Lugar, Guid.CreateVersion7(), PrecondicaoIfMatch.Ausente),
            _processos, _escolhidos, _unitOfWork, CancellationToken.None);

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    private Task<Result<MutacaoAceita>> Incluir(Guid motivoId, LugarDaDecisao? lugar = null) =>
        IncluirMotivoNoProcessoCommandHandler.Handle(
            new IncluirMotivoNoProcessoCommand(_processo.Id, lugar ?? Lugar, motivoId, null, PrecondicaoIfMatch.Ausente),
            _processos, _cadastro, _escolhidos, _fasesIniciadas, _unitOfWork, CancellationToken.None);

    private Task<Result<MutacaoAceita>> Retirar(Guid motivoId, LugarDaDecisao? lugar = null) =>
        RetirarMotivoDoProcessoCommandHandler.Handle(
            new RetirarMotivoDoProcessoCommand(_processo.Id, lugar ?? Lugar, motivoId, PrecondicaoIfMatch.Ausente),
            _processos, _escolhidos, _unitOfWork, CancellationToken.None);

    private ProcessoSeletivo ProcessoEmRetificacao()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        AbrirRetificacao(processo, Publicar(processo));
        Registrar(processo);

        return processo;
    }

    private void Registrar(ProcessoSeletivo processo, params MotivoDoProcesso[] escolhidos)
    {
        _processos.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        _escolhidos.ListarEscolhaAtualAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(escolhidos);
    }

    private Task<Result<MutacaoAceita>> IncluirNaRetificacao(ProcessoSeletivo processo, FasesIniciadas iniciadas)
    {
        MotivoDeDecisao origem = Motivo();
        _cadastro.ObterPorIdAsync(origem.Id, Arg.Any<CancellationToken>()).Returns(origem);

        return IncluirMotivoNoProcessoCommandHandler.Handle(
            new IncluirMotivoNoProcessoCommand(processo.Id, LugarDaPrimeiraFase(processo), origem.Id, "Retorno do INEP exigido.", PrecondicaoIfMatch.Curinga),
            _processos, _cadastro, _escolhidos, FasesIniciadasDeTeste.Com(iniciadas), _unitOfWork, CancellationToken.None);
    }
}
