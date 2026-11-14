namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDeRecusaNoCongelamento;

/// <summary>
/// O caminho do handler até a gravação em cada ato que congela uma versão: o rascunho de publicação só morre depois do
/// flush que gravou a versão, e o flush que viola a unicidade ou falha o preserva.
/// </summary>
public sealed class CaminhoDaGravacaoNosHandlersTests
{
    private const string IndiceDoNumeroDaVersao = "ux_versoes_configuracao_processo_numero";

    public static TheoryData<OperacaoQueCongela> Atos() =>
        [OperacaoQueCongela.Publicar, OperacaoQueCongela.Retificar, OperacaoQueCongela.FecharRetificacao];

    [Theory(DisplayName = "No sucesso o handler salva e só então apaga o rascunho do processo")]
    [MemberData(nameof(Atos))]
    public async Task Sucesso_ApagaORascunhoDepoisDoFlush(OperacaoQueCongela operacao)
    {
        CenarioDeRecusaNoCongelamento cenario = Para(operacao);

        (Result resposta, _) = await cenario.ExecutarAsync();

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        Received.InOrder(() =>
        {
            _ = cenario.Servicos.UnitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
            _ = cenario.Servicos.Rascunhos.ApagarDoProcessoAsync(cenario.Processo.Id, Arg.Any<CancellationToken>());
        });
    }

    [Theory(DisplayName = "No sucesso a resposta traz o registro do ato da versão gravada e a cópia dos documentos do mesmo ato, e só eles nos eventos do ato")]
    [MemberData(nameof(Atos))]
    public async Task Sucesso_RespondeComORegistroDoAtoDaVersaoGravada(OperacaoQueCongela operacao)
    {
        CenarioDeRecusaNoCongelamento cenario = Para(operacao);
        VersaoConfiguracao? gravada = null;
        cenario.Servicos.Repositorio.AdicionarVersaoConfiguracaoAsync(Arg.Do<VersaoConfiguracao>(v => gravada = v), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        (_, IEnumerable<object> eventos) = await cenario.ExecutarAsync();

        RegistrarAtoNormativoRequisicao requisicao = eventos.Should().ContainSingle(static e => e is RegistrarAtoNormativoRequisicao)
            .Which.Should().BeOfType<RegistrarAtoNormativoRequisicao>().Subject;
        eventos.Last().Should().Be(new CopiarDocumentosDoAtoAoAcervo(cenario.Processo.Id, gravada!.AtoCriadorId));
        requisicao.AtoId.Should().Be(gravada.AtoCriadorId);
        (requisicao.AtoRetificadoId is null).Should().Be(operacao == OperacaoQueCongela.Publicar);
    }

    [Theory(DisplayName = "O flush que viola a unicidade do número da versão recusa e não apaga o rascunho")]
    [MemberData(nameof(Atos))]
    public async Task UnicidadeNoFlush_NaoApagaORascunho(OperacaoQueCongela operacao)
    {
        CenarioDeRecusaNoCongelamento cenario = Para(operacao);
        cenario.Servicos.UnitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(ExcecaoDeBancoDeTeste.ViolacaoDeUnicidade(IndiceDoNumeroDaVersao));

        (Result resposta, IEnumerable<object> eventos) = await cenario.ExecutarAsync();

        resposta.Error!.Code.Should().Be("VersaoConfiguracao.NumeroDuplicado");
        eventos.Should().BeEmpty();
        await cenario.Servicos.Rascunhos.DidNotReceive().ApagarDoProcessoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Theory(DisplayName = "A falha do flush propaga ao Wolverine e não apaga o rascunho")]
    [MemberData(nameof(Atos))]
    public async Task FalhaNoFlush_NaoApagaORascunho(OperacaoQueCongela operacao)
    {
        CenarioDeRecusaNoCongelamento cenario = Para(operacao);
        cenario.Servicos.UnitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("Conexão perdida."));

        Func<Task> executar = cenario.ExecutarAsync;

        await executar.Should().ThrowAsync<InvalidOperationException>();
        await cenario.Servicos.Rascunhos.DidNotReceive().ApagarDoProcessoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
