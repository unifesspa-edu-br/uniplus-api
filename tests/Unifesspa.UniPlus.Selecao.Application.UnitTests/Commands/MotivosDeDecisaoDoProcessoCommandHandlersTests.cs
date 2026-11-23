namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDeDecisaoDoProcesso;
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
/// Inclusão e retirada de motivo numa fase ou etapa do processo (UNI-REQ-0122, UNI-REQ-0285), só em rascunho: o que o
/// handler carrega e grava.
/// </summary>
public sealed class MotivosDeDecisaoDoProcessoCommandHandlersTests
{
    private readonly ProcessoSeletivo _processo = NovoProcessoConforme();
    private readonly IProcessoSeletivoRepository _processos = Substitute.For<IProcessoSeletivoRepository>();
    private readonly IMotivoDeDecisaoRepository _cadastro = Substitute.For<IMotivoDeDecisaoRepository>();
    private readonly IMotivoDeDecisaoDoProcessoRepository _escolhidos = Substitute.For<IMotivoDeDecisaoDoProcessoRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    public MotivosDeDecisaoDoProcessoCommandHandlersTests()
    {
        _processos.ObterParaMutacaoAsync(_processo.Id, Arg.Any<CancellationToken>()).Returns(_processo);
        _escolhidos.ListarEscolhaAtualAsync(_processo.Id, Arg.Any<CancellationToken>()).Returns([]);
    }

    private LugarDaDecisao Lugar => LugarDaPrimeiraFase(_processo);

    [Fact(DisplayName = "Incluir no rascunho copia o motivo do cadastro no lugar e grava")]
    public async Task Incluir_NoRascunho_Grava()
    {
        MotivoDeDecisao origem = Motivo();
        _cadastro.ObterPorIdAsync(origem.Id, Arg.Any<CancellationToken>()).Returns(origem);

        Result resultado = await Incluir(origem.Id);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        await _escolhidos.Received(1).AdicionarAsync(
            Arg.Is<MotivoDeDecisaoDoProcesso>(m => m.MotivoDeDecisaoOrigemId == origem.Id && m.Lugar == Lugar && m.ProcessoSeletivoId == _processo.Id),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Incluir em processo inexistente devolve não encontrado")]
    public async Task Incluir_ProcessoInexistente_NaoEncontrado()
    {
        Result resultado = await IncluirMotivoDeDecisaoNoProcessoCommandHandler.Handle(
            new IncluirMotivoDeDecisaoNoProcessoCommand(Guid.CreateVersion7(), Lugar, Guid.CreateVersion7()),
            _processos, _cadastro, _escolhidos, _unitOfWork, CancellationToken.None);

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

        Result resultado = await Incluir(origem.Id, LugarDaDecisao.DaFase(Guid.CreateVersion7()));

        resultado.Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.FaseOuEtapaNaoEncontrada);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A recusa do domínio descarta o rastreamento e não grava")]
    public async Task Incluir_MotivoInativo_DescartaSemGravar()
    {
        MotivoDeDecisao origem = Motivo();
        origem.Desativar();
        _cadastro.ObterPorIdAsync(origem.Id, Arg.Any<CancellationToken>()).Returns(origem);

        Result resultado = await Incluir(origem.Id);

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

        Result resultado = await Incluir(origem.Id);

        resultado.Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.SituacaoAlteradaConcorrentemente);
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "A escolha repetida em corrida, que o índice da escolha vigente recusa, volta como conflito retentável")]
    public async Task Incluir_EscolhaRepetidaEmCorrida_DevolveConflito()
    {
        MotivoDeDecisao origem = Motivo();
        _cadastro.ObterPorIdAsync(origem.Id, Arg.Any<CancellationToken>()).Returns(origem);
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(ExcecaoDeBancoDeTeste.ViolacaoDeUnicidade("ux_motivos_de_decisao_do_processo_vigente"));

        Result resultado = await Incluir(origem.Id);

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

    [Fact(DisplayName = "Retirar motivo escolhido no rascunho remove e grava")]
    public async Task Retirar_Escolhido_Remove()
    {
        MotivoDeDecisaoDoProcesso escolhido = Escolhido(_processo, Lugar, Motivo());
        _escolhidos.ListarEscolhaAtualAsync(_processo.Id, Arg.Any<CancellationToken>()).Returns([escolhido]);

        Result resultado = await Retirar(escolhido.MotivoDeDecisaoOrigemId);

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

    [Fact(DisplayName = "Retirar motivo de processo publicado é recusado sem remover")]
    public async Task Retirar_ProcessoPublicado_Recusa()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        MotivoDeDecisaoDoProcesso escolhido = Escolhido(processo, LugarDaPrimeiraFase(processo), Motivo());
        Publicar(processo);
        Registrar(processo, escolhido);

        Result resultado = await RetirarMotivoDeDecisaoDoProcessoCommandHandler.Handle(
            new RetirarMotivoDeDecisaoDoProcessoCommand(processo.Id, escolhido.Lugar, escolhido.MotivoDeDecisaoOrigemId),
            _processos, _escolhidos, _unitOfWork, CancellationToken.None);

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
        _escolhidos.DidNotReceive().Remover(Arg.Any<MotivoDeDecisaoDoProcesso>());
    }

    [Fact(DisplayName = "Incluir motivo em processo publicado é recusado sem gravar")]
    public async Task Incluir_ProcessoPublicado_Recusa()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        Publicar(processo);
        Registrar(processo);
        MotivoDeDecisao origem = Motivo();
        _cadastro.ObterPorIdAsync(origem.Id, Arg.Any<CancellationToken>()).Returns(origem);

        Result resultado = await IncluirMotivoDeDecisaoNoProcessoCommandHandler.Handle(
            new IncluirMotivoDeDecisaoNoProcessoCommand(processo.Id, LugarDaPrimeiraFase(processo), origem.Id),
            _processos, _cadastro, _escolhidos, _unitOfWork, CancellationToken.None);

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
        await _escolhidos.DidNotReceive().AdicionarAsync(Arg.Any<MotivoDeDecisaoDoProcesso>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Incluir motivo inexistente em processo publicado devolve o bloqueio, sem ler o cadastro")]
    public async Task Incluir_ProcessoPublicadoEMotivoInexistente_BloqueioPrecedeOCadastro()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        Publicar(processo);
        Registrar(processo);

        Result resultado = await IncluirMotivoDeDecisaoNoProcessoCommandHandler.Handle(
            new IncluirMotivoDeDecisaoNoProcessoCommand(processo.Id, LugarDaPrimeiraFase(processo), Guid.CreateVersion7()),
            _processos, _cadastro, _escolhidos, _unitOfWork, CancellationToken.None);

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
        await _cadastro.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Incluir motivo inexistente numa fase que o processo não tem devolve o lugar, sem ler o cadastro")]
    public async Task Incluir_LugarInexistenteEMotivoInexistente_LugarPrecedeOCadastro()
    {
        Result resultado = await Incluir(Guid.CreateVersion7(), LugarDaDecisao.DaFase(Guid.CreateVersion7()));

        resultado.Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.FaseOuEtapaNaoEncontrada);
        await _cadastro.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Retirar motivo não escolhido de processo publicado devolve o bloqueio")]
    public async Task Retirar_ProcessoPublicadoENaoEscolhido_Bloqueio()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        Publicar(processo);
        Registrar(processo);

        Result resultado = await RetirarMotivoDeDecisaoDoProcessoCommandHandler.Handle(
            new RetirarMotivoDeDecisaoDoProcessoCommand(processo.Id, LugarDaPrimeiraFase(processo), Guid.CreateVersion7()),
            _processos, _escolhidos, _unitOfWork, CancellationToken.None);

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
    }

    [Fact(DisplayName = "Retirar de processo inexistente devolve não encontrado")]
    public async Task Retirar_ProcessoInexistente_NaoEncontrado()
    {
        Result resultado = await RetirarMotivoDeDecisaoDoProcessoCommandHandler.Handle(
            new RetirarMotivoDeDecisaoDoProcessoCommand(Guid.CreateVersion7(), Lugar, Guid.CreateVersion7()),
            _processos, _escolhidos, _unitOfWork, CancellationToken.None);

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    private Task<Result> Incluir(Guid motivoId, LugarDaDecisao? lugar = null) =>
        IncluirMotivoDeDecisaoNoProcessoCommandHandler.Handle(
            new IncluirMotivoDeDecisaoNoProcessoCommand(_processo.Id, lugar ?? Lugar, motivoId),
            _processos, _cadastro, _escolhidos, _unitOfWork, CancellationToken.None);

    private Task<Result> Retirar(Guid motivoId, LugarDaDecisao? lugar = null) =>
        RetirarMotivoDeDecisaoDoProcessoCommandHandler.Handle(
            new RetirarMotivoDeDecisaoDoProcessoCommand(_processo.Id, lugar ?? Lugar, motivoId),
            _processos, _escolhidos, _unitOfWork, CancellationToken.None);

    private void Registrar(ProcessoSeletivo processo, params MotivoDeDecisaoDoProcesso[] escolhidos)
    {
        _processos.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        _escolhidos.ListarEscolhaAtualAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(escolhidos);
    }
}
