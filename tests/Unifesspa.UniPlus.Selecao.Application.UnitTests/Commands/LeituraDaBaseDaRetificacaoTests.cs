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
/// O que o fechamento e a retificação de ato único conferem sobre a cadeia de versões antes de canonicalizar: a ordem
/// das checagens, o motivo normalizado uma vez só, a versão reidratada uma vez só e o instante em que a
/// fase iniciada é lida.
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
        resposta.Error.Message.Should().Contain(processo.Status.ToString());
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

    [Fact(DisplayName = "A versão vigente é reidratada uma só vez no fluxo inteiro do fechamento")]
    public async Task Fechamento_ReidrataAVersaoVigenteUmaSoVez()
    {
        DependenciasDosHandlersQueCongelam emSessao = DependenciasDosHandlersQueCongelam.ParaProcessoEmRetificacao();
        VersaoConfiguracao vigente = (await emSessao.Repositorio.ObterVersaoAtualAsync(emSessao.Processo.Id, CancellationToken.None))!;

        (Result resposta, _) = await emSessao.FecharRetificacaoAsync();

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        emSessao.Codecs.Received(1).Reidratar(vigente);
    }

    [Fact(DisplayName = "Sem versão corrente, o fechamento recusa a base desatualizada")]
    public async Task Sessao_SemVersaoCorrente_RecusaABaseDesatualizada()
    {
        DependenciasDosHandlersQueCongelam emSessao = DependenciasDosHandlersQueCongelam.ParaProcessoEmRetificacao();
        emSessao.Repositorio.ObterVersaoAtualAsync(emSessao.Processo.Id, Arg.Any<CancellationToken>()).Returns((VersaoConfiguracao?)null);

        Result<BaseDaRetificacao> resposta = await emSessao.Servicos.LeituraDaBase().DaSessaoAbertaAsync(emSessao.Processo, CancellationToken.None);

        resposta.Error!.Code.Should().Be("RascunhoRetificacao.BaseDesatualizada");
    }

    [Fact(DisplayName = "A versão corrente diferente da base da sessão recusa antes de conferir o ciclo recursal")]
    public async Task Sessao_BaseDiferenteDaCorrente_RecusaAntesDoCicloRecursal()
    {
        DependenciasDosHandlersQueCongelam emSessao = DependenciasDosHandlersQueCongelam.ParaProcessoEmRetificacao();
        (_, VersaoConfiguracao outra) = ProcessoPublicado();
        emSessao.Repositorio.ObterVersaoAtualAsync(emSessao.Processo.Id, Arg.Any<CancellationToken>()).Returns(outra);

        Result<BaseDaRetificacao> resposta = await emSessao.Servicos.LeituraDaBase().DaSessaoAbertaAsync(emSessao.Processo, CancellationToken.None);

        resposta.Error!.Code.Should().Be("RascunhoRetificacao.BaseDesatualizada");
        await emSessao.Servicos.Atos.DidNotReceive().ListarDoProcessoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O motivo da base da sessão é o do rascunho, declarado na abertura")]
    public async Task Sessao_OMotivoEODoRascunho()
    {
        DependenciasDosHandlersQueCongelam emSessao = DependenciasDosHandlersQueCongelam.ParaProcessoEmRetificacao();

        Result<BaseDaRetificacao> resposta = await emSessao.Servicos.LeituraDaBase().DaSessaoAbertaAsync(emSessao.Processo, CancellationToken.None);

        resposta.Value!.Motivo.Should().Be(emSessao.Processo.Rascunho!.Motivo);
    }

    [Fact(DisplayName = "A fase iniciada é lida no instante em que o fechamento publica, e sem recusa devolve nulo")]
    public async Task FaseIniciada_ELidaNoInstanteRecebido()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        ServicosDoCongelamentoDeTeste servicos = ServicosDoCongelamentoDeTeste.Para(processo);

        DomainError? recusa = await servicos.LeituraDaBase().RecusaPorFaseIniciadaAsync(processo, Agora, CancellationToken.None);

        recusa.Should().BeNull();
        await servicos.FasesIniciadas.Received(1).ObterAsync(processo.Id, Agora, Arg.Any<CancellationToken>());
    }
}
