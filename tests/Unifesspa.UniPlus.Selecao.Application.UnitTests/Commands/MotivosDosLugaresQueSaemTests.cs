namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.MotivoDeTeste;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.PublicacaoDeTeste;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.CenariosDeDefinirEtapas;

/// <summary>
/// A fase ou etapa que sai do processo leva junto os motivos pendentes dela, e o motivo já publicado recusa a saída
/// (UNI-REQ-0124), tanto na substituição das etapas quanto na do cronograma de fases.
/// </summary>
public sealed class MotivosDosLugaresQueSaemTests
{
    private readonly IMotivoDoProcessoRepository _motivos = SemEscolha();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static EtapaProcessoInput EtapaNova(string nome) => new(
        nome, CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1, null, Produtos: [], Bancas: [], Recursos: []);

    [Fact(DisplayName = "A etapa substituída sem id sai do rascunho com os motivos escolhidos nela")]
    public async Task Etapas_EtapaQueSai_LevaOsMotivosPendentes()
    {
        (ProcessoSeletivo processo, MotivoDoProcesso escolhido) = await ProcessoComUmaEtapaEUmMotivoAsync();

        Result<MutacaoAceita> resultado = await SubstituirEtapas(processo, EtapaNova("Redação"));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        _motivos.Received(1).Remover(escolhido);
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A etapa devolvida com o id continua no processo e conserva os motivos")]
    public async Task Etapas_EtapaQueContinua_ConservaOsMotivos()
    {
        (ProcessoSeletivo processo, _) = await ProcessoComUmaEtapaEUmMotivoAsync();

        Result<MutacaoAceita> resultado = await SubstituirEtapas(processo, EtapaNova("Prova Objetiva") with { Id = processo.Etapas.Single().Id });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        _motivos.DidNotReceive().Remover(Arg.Any<MotivoDoProcesso>());
    }

    [Fact(DisplayName = "Na retificação, a etapa que sai com motivo já publicado recusa a substituição, descarta o rastreamento e não grava")]
    public async Task Etapas_EtapaComMotivoPublicado_Recusa()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        MotivoDoProcesso escolhido = Escolhido(processo, LugarDaDecisao.DaEtapa(processo.Etapas.First().Id), Motivo());
        AbrirRetificacao(processo, Publicar(processo));
        Escolher(processo, escolhido);

        Result<MutacaoAceita> resultado = await SubstituirEtapas(processo, EtapaNova("Redação"), PrecondicaoIfMatch.Curinga);

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(MotivoDeDecisaoErrorCodes.FaseOuEtapaComMotivoPublicado);
        _motivos.DidNotReceive().Remover(Arg.Any<MotivoDoProcesso>());
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Na retificação, o motivo pendente da etapa que sai vai com ela")]
    public async Task Etapas_EtapaComMotivoPendente_NaRetificacao_Remove()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        AbrirRetificacao(processo, Publicar(processo));
        MotivoDoProcesso pendente = Escolhido(processo, LugarDaDecisao.DaEtapa(processo.Etapas.First().Id), Motivo(), PrecondicaoIfMatch.Curinga);
        Escolher(processo, pendente);

        Result<MutacaoAceita> resultado = await SubstituirEtapas(processo, EtapaNova("Redação"), PrecondicaoIfMatch.Curinga);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        _motivos.Received(1).Remover(pendente);
    }

    [Fact(DisplayName = "Sem motivo nos lugares que saem, a retirada não remove nada")]
    public async Task RetirarAsync_SemMotivos_NaoRemoveNada()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        LugaresDaDecisao antes = LugaresDaDecisao.Do(processo);

        Result resultado = await new MotivosDosLugaresQueSaem(_motivos).RetirarAsync(processo, antes, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        _motivos.DidNotReceive().Remover(Arg.Any<MotivoDoProcesso>());
    }

    private async Task<(ProcessoSeletivo Processo, MotivoDoProcesso Escolhido)> ProcessoComUmaEtapaEUmMotivoAsync()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        (await SubstituirEtapas(processo, EtapaNova("Prova Objetiva"))).IsSuccess.Should().BeTrue();
        MotivoDoProcesso escolhido = Escolhido(processo, LugarDaDecisao.DaEtapa(processo.Etapas.Single().Id), Motivo());
        Escolher(processo, escolhido);
        _unitOfWork.ClearReceivedCalls();

        return (processo, escolhido);
    }

    private void Escolher(ProcessoSeletivo processo, MotivoDoProcesso escolhido) =>
        _motivos.ListarEscolhaAtualAsync(processo.Id, Arg.Any<CancellationToken>()).Returns([escolhido]);

    private Task<Result<MutacaoAceita>> SubstituirEtapas(ProcessoSeletivo processo, EtapaProcessoInput etapa, PrecondicaoIfMatch? precondicao = null)
    {
        IProcessoSeletivoRepository repositorio = RepositorioDe(processo);

        return DefinirEtapasCommandHandler.Handle(
            new DefinirEtapasCommand(processo.Id, [etapa], precondicao ?? PrecondicaoIfMatch.Ausente),
            repositorio, DefinicaoDasEtapasComMotivos(repositorio, _motivos), _unitOfWork, TimeProvider.System, CancellationToken.None);
    }

    private static IProcessoSeletivoRepository RepositorioDe(ProcessoSeletivo processo)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        return repository;
    }
}
