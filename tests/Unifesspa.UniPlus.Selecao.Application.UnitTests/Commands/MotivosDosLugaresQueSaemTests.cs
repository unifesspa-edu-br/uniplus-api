namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.MotivoDeTeste;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.PublicacaoDeTeste;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.CenariosDeDefinirEtapas;

/// <summary>
/// A fase ou etapa que sai do processo leva junto os motivos dela, tanto na substituição das etapas quanto na do
/// cronograma de fases.
/// </summary>
public sealed class MotivosDosLugaresQueSaemTests
{
    private readonly IMotivoDeDecisaoDoProcessoRepository _motivos = SemEscolha();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static EtapaProcessoInput EtapaNova(string nome) => new(
        nome, CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1, null, Produtos: [], Bancas: [], Recursos: []);

    [Fact(DisplayName = "A etapa substituída sem id sai do rascunho com os motivos escolhidos nela")]
    public async Task Etapas_EtapaQueSai_LevaOsMotivosPendentes()
    {
        (ProcessoSeletivo processo, MotivoDeDecisaoDoProcesso escolhido) = await ProcessoComUmaEtapaEUmMotivoAsync();

        Result resultado = await SubstituirEtapas(processo, EtapaNova("Redação"));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        _motivos.Received(1).Remover(escolhido);
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A etapa devolvida com o id continua no processo e conserva os motivos")]
    public async Task Etapas_EtapaQueContinua_ConservaOsMotivos()
    {
        (ProcessoSeletivo processo, _) = await ProcessoComUmaEtapaEUmMotivoAsync();

        Result resultado = await SubstituirEtapas(processo, EtapaNova("Prova Objetiva") with { Id = processo.Etapas.Single().Id });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        _motivos.DidNotReceive().Remover(Arg.Any<MotivoDeDecisaoDoProcesso>());
    }

    [Fact(DisplayName = "Sem motivo nos lugares que saem, a retirada não remove nada")]
    public async Task RetirarAsync_SemMotivos_NaoRemoveNada()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        LugaresDaDecisao antes = LugaresDaDecisao.Das(processo.CronogramaFases, processo.Etapas);

        await new MotivosDosLugaresQueSaem(_motivos).RetirarAsync(processo, antes, CancellationToken.None);

        _motivos.DidNotReceive().Remover(Arg.Any<MotivoDeDecisaoDoProcesso>());
    }

    private async Task<(ProcessoSeletivo Processo, MotivoDeDecisaoDoProcesso Escolhido)> ProcessoComUmaEtapaEUmMotivoAsync()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        (await SubstituirEtapas(processo, EtapaNova("Prova Objetiva"))).IsSuccess.Should().BeTrue();
        MotivoDeDecisaoDoProcesso escolhido = Escolhido(processo, LugarDaDecisao.DaEtapa(processo.Etapas.Single().Id), Motivo());
        Escolher(processo, escolhido);
        _unitOfWork.ClearReceivedCalls();

        return (processo, escolhido);
    }

    private void Escolher(ProcessoSeletivo processo, MotivoDeDecisaoDoProcesso escolhido) =>
        _motivos.ListarEscolhaAtualAsync(processo.Id, Arg.Any<CancellationToken>()).Returns([escolhido]);

    private async Task<Result> SubstituirEtapas(ProcessoSeletivo processo, EtapaProcessoInput etapa)
    {
        IProcessoSeletivoRepository repositorio = RepositorioDe(processo);

        Result resultado = await DefinirEtapasCommandHandler.Handle(
            new DefinirEtapasCommand(processo.Id, [etapa]),
            repositorio, DefinicaoDasEtapasComMotivos(repositorio, _motivos), _unitOfWork, TimeProvider.System, CancellationToken.None);
        return resultado;
    }

    private static IProcessoSeletivoRepository RepositorioDe(ProcessoSeletivo processo)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        return repository;
    }
}
