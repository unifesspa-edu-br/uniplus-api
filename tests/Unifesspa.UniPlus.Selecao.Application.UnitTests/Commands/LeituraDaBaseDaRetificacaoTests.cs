namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.PublicacaoDeTeste;

/// <summary>
/// O que a retificação de ato único confere sobre a cadeia de versões antes de canonicalizar: a ordem das checagens,
/// o motivo normalizado uma vez só e a versão reidratada uma vez só.
/// </summary>
public sealed class LeituraDaBaseDaRetificacaoTests
{
    [Fact(DisplayName = "Sem versão corrente, retificar é transição inválida e o ciclo recursal nem é conferido")]
    public async Task SemVersaoCorrente_RecusaAntesDoCicloRecursal()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        ServicosDoCongelamentoDeTeste servicos = ServicosDoCongelamentoDeTeste.Para(processo);

        Result<BaseDaRetificacao> resposta = await servicos.LeituraDaBase().DaVersaoPublicadaAsync(processo, "Correção do prazo", CancellationToken.None);

        resposta.Error!.Code.Should().Be("ProcessoSeletivo.TransicaoInvalida");
        resposta.Error.Message.Should().Contain("situação atual: Rascunho");
        await servicos.Atos.DidNotReceive().ListarDoProcessoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O motivo do pedido chega aparado e normalizado, e a base guarda a versão vigente")]
    public async Task MotivoDoPedido_ChegaAparadoENormalizado()
    {
        DependenciasDosHandlersQueCongelam publicado = DependenciasDosHandlersQueCongelam.ParaProcessoPublicado();
        VersaoConfiguracao vigente = (await publicado.Repositorio.ObterVersaoAtualAsync(publicado.Processo.Id, CancellationToken.None))!;

        Result<BaseDaRetificacao> resposta = await publicado.Servicos.LeituraDaBase()
            .DaVersaoPublicadaAsync(publicado.Processo, "  Correção do prazo  ", CancellationToken.None);

        resposta.Value!.Motivo.Should().Be("Correção do prazo");
        resposta.Value.VersaoVigente.Should().BeSameAs(vigente);
    }

    [Fact(DisplayName = "O motivo ausente chega à base como texto vazio, e quem recusa é a conferência do estado")]
    public async Task MotivoAusente_ChegaVazioABase()
    {
        DependenciasDosHandlersQueCongelam publicado = DependenciasDosHandlersQueCongelam.ParaProcessoPublicado();

        Result<BaseDaRetificacao> resposta = await publicado.Servicos.LeituraDaBase()
            .DaVersaoPublicadaAsync(publicado.Processo, null, CancellationToken.None);

        resposta.Value!.Motivo.Should().BeEmpty();
        new RetificacaoDeAtoUnico(resposta.Value).ConferirEstado(publicado.Processo).Errors
            .Should().ContainSingle().Which.Field.Should().Be("motivo");
    }

    [Fact(DisplayName = "O ciclo recursal aberto recusa com todos os erros da conferência, na ordem")]
    public async Task CicloRecursalAberto_RecusaComOsErrosDaConferencia()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado();
        cenario.ComPreliminarPublicadoEDefinitivoPendente();
        VersaoConfiguracao vigente = (await cenario.Servicos.Repositorio.ObterVersaoAtualAsync(cenario.Processo.Id, CancellationToken.None))!;
        Result esperado = await ConferenciaDoCicloRecursalAberto.ConferirAsync(
            cenario.Servicos.Atos, cenario.Servicos.Codecs, cenario.Processo, vigente, CancellationToken.None);

        Result<BaseDaRetificacao> resposta = await cenario.Servicos.LeituraDaBase()
            .DaVersaoPublicadaAsync(cenario.Processo, "Correção do prazo", CancellationToken.None);

        esperado.IsFailure.Should().BeTrue();
        resposta.Errors.Should().Equal(esperado.Errors);
    }

    [Fact(DisplayName = "A versão vigente é reidratada uma só vez no fluxo inteiro da retificação")]
    public async Task Retificacao_ReidrataAVersaoVigenteUmaSoVez()
    {
        DependenciasDosHandlersQueCongelam publicado = DependenciasDosHandlersQueCongelam.ParaProcessoPublicado();
        VersaoConfiguracao vigente = (await publicado.Repositorio.ObterVersaoAtualAsync(publicado.Processo.Id, CancellationToken.None))!;

        (Result resposta, _) = await publicado.RetificarAsync();

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        publicado.Codecs.Received(1).Reidratar(vigente);
    }
}
