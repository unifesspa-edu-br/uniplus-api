namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.Rascunhos;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.Rascunhos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class ExcluirRascunhoDeProcessoSeletivoCommandHandlerTests
{
    private readonly CenarioDoRascunho _cenario = new();

    private Task<Result> Excluir(Guid id, PrecondicaoIfMatch precondicao) =>
        ExcluirRascunhoDeProcessoSeletivoCommandHandler.Handle(
            new ExcluirRascunhoDeProcessoSeletivoCommand(id, precondicao),
            _cenario.Bloqueio,
            _cenario.Rascunhos,
            _cenario.UnitOfWork,
            CancellationToken.None);

    private async Task NadaFoiApagado()
    {
        _cenario.Rascunhos.DidNotReceiveWithAnyArgs().Remover(default!);
        await _cenario.UnitOfWork.DidNotReceiveWithAnyArgs().SalvarAlteracoesAsync(default);
    }

    [Fact(DisplayName = "Com o ETag da revisão atual remove o rascunho e grava")]
    public async Task ETagQueCasa_Remove()
    {
        RascunhoDeProcessoSeletivo rascunho = _cenario.ExisteUmRascunhoNaRevisao1(CenarioDoRascunho.Ceps);

        Result resultado = await Excluir(rascunho.Id, CenarioDoRascunho.Precondicao(rascunho));

        resultado.IsSuccess.Should().BeTrue();
        _cenario.Rascunhos.Received(1).Remover(rascunho);
        await _cenario.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Sem If-Match recusa 428 antes de bloquear a linha")]
    public async Task SemIfMatch_428AntesDoBloqueio()
    {
        Result resultado = await Excluir(Guid.CreateVersion7(), PrecondicaoIfMatch.Ausente);

        resultado.Error!.Code.Should().Be(PrecondicaoErrorCodes.Requerida);
        await _cenario.Rascunhos.DidNotReceiveWithAnyArgs().ObterParaMutacaoAsync(default, default);
        await NadaFoiApagado();
    }

    [Fact(DisplayName = "A linha ausente com processo de mesmo id recusa 409 ja_publicado")]
    public async Task LinhaAusenteComProcesso_JaPublicado()
    {
        Guid id = Guid.CreateVersion7();
        _cenario.Processos.ExisteAsync(id, Arg.Any<CancellationToken>()).Returns(true);

        Result resultado = await Excluir(id, PrecondicaoIfMatch.Curinga);

        resultado.Error!.Code.Should().Be(RascunhoDeProcessoSeletivoErrorCodes.JaPublicado);
        await NadaFoiApagado();
    }

    [Fact(DisplayName = "A linha ausente sem processo recusa 404")]
    public async Task LinhaAusenteSemProcesso_NaoEncontrado()
    {
        Result resultado = await Excluir(Guid.CreateVersion7(), PrecondicaoIfMatch.Curinga);

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "O ETag obsoleto recusa 412 e não apaga")]
    public async Task ETagObsoleto_412()
    {
        RascunhoDeProcessoSeletivo rascunho = _cenario.ExisteUmRascunhoNaRevisao1(CenarioDoRascunho.Ceps);

        Result resultado = await Excluir(rascunho.Id, PrecondicaoIfMatch.DeTags(["\"4\""]));

        resultado.Error!.Code.Should().Be(PrecondicaoErrorCodes.Falhou);
        await NadaFoiApagado();
    }

    [Fact(DisplayName = "A unidade que a linha tem agora, onde a pessoa não configura, recusa 404 como a linha ausente e não apaga")]
    public async Task UnidadeAtualSemPermissao_404ENadaApagado()
    {
        RascunhoDeProcessoSeletivo rascunho = _cenario.ExisteUmRascunhoNaRevisao1(CenarioDoRascunho.Cga);

        Result resultado = await Excluir(rascunho.Id, CenarioDoRascunho.Precondicao(rascunho));

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
        await NadaFoiApagado();
    }

    [Fact(DisplayName = "A unidade atual sem permissão recusa 404 antes do 412 do ETag obsoleto")]
    public async Task UnidadeAtualSemPermissao_404AntesDo412()
    {
        RascunhoDeProcessoSeletivo rascunho = _cenario.ExisteUmRascunhoNaRevisao1(CenarioDoRascunho.Cga);

        Result resultado = await Excluir(rascunho.Id, PrecondicaoIfMatch.DeTags(["\"4\""]));

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "A unidade atual onde a pessoa configura, ainda que não seja a da autorização da rota, apaga")]
    public async Task UnidadeAtualComPermissao_Apaga()
    {
        _cenario.PodeConfigurarEm(CenarioDoRascunho.Cga);
        RascunhoDeProcessoSeletivo rascunho = _cenario.ExisteUmRascunhoNaRevisao1(CenarioDoRascunho.Cga);

        Result resultado = await Excluir(rascunho.Id, CenarioDoRascunho.Precondicao(rascunho));

        resultado.IsSuccess.Should().BeTrue();
        _cenario.Rascunhos.Received(1).Remover(rascunho);
    }

    [Fact(DisplayName = "Os argumentos nulos são recusados")]
    public async Task ArgumentosNulos_Lancam()
    {
        Func<Task> semComando = () => ExcluirRascunhoDeProcessoSeletivoCommandHandler.Handle(
            null!, _cenario.Bloqueio, _cenario.Rascunhos, _cenario.UnitOfWork, CancellationToken.None);

        await semComando.Should().ThrowAsync<ArgumentNullException>();
    }
}
