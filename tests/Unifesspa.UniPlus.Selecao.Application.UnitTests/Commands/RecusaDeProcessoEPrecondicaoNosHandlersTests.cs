namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDeRecusaNoCongelamento;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.PublicacaoDeTeste;

/// <summary>
/// O processo que não existe é recusado com a mesma mensagem pelos três handlers que congelam, antes de qualquer
/// leitura; no fechamento, a precondição e a sessão aberta são conferidas antes do documento e do ato.
/// </summary>
public sealed class RecusaDeProcessoEPrecondicaoNosHandlersTests
{
    [Theory(DisplayName = "O processo inexistente é recusado com a mesma mensagem, antes de ler documento, ato ou catálogo")]
    [InlineData(OperacaoQueCongela.Publicar)]
    [InlineData(OperacaoQueCongela.Retificar)]
    [InlineData(OperacaoQueCongela.FecharRetificacao)]
    public async Task ProcessoInexistente_RecusaComAMesmaMensagem(OperacaoQueCongela operacao)
    {
        Guid processoId = Guid.CreateVersion7();
        ServicosDoCongelamentoDeTeste servicos = new();

        (Result resposta, IEnumerable<object> eventos) = await ExecutarAsync(servicos, operacao, processoId);

        resposta.Errors.Should().ContainSingle().Which.Field.Should().BeNull();
        resposta.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
        resposta.Error.Message.Should().Be($"Processo Seletivo {processoId} não encontrado.");
        eventos.Should().BeEmpty();
        await servicos.Documentos.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
        await servicos.NadaFoiGravadoAsync();
    }

    [Fact(DisplayName = "Fechar sem If-Match recusa a precondição antes de conferir o documento do ato")]
    public async Task Fechar_SemIfMatch_RecusaAntesDoAto()
    {
        CenarioDeRecusaNoCongelamento cenario = Para(OperacaoQueCongela.FecharRetificacao).Com(DefeitoDoCongelamento.Documento);

        (Result resposta, IEnumerable<object> eventos) = await FecharComAsync(cenario, PrecondicaoIfMatch.Ausente);

        resposta.Error!.Code.Should().Be("Precondicao.Requerida");
        resposta.Errors.Should().ContainSingle().Which.Field.Should().BeNull();
        eventos.Should().BeEmpty();
        await cenario.Servicos.Documentos.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
    }

    [Fact(DisplayName = "Fechar sem sessão aberta recusa antes de conferir o documento do ato")]
    public async Task Fechar_SemSessao_RecusaAntesDoAto()
    {
        CenarioDeRecusaNoCongelamento cenario = Para(OperacaoQueCongela.Retificar).Com(DefeitoDoCongelamento.Documento);

        (Result resposta, IEnumerable<object> eventos) = await FecharComAsync(cenario, PrecondicaoIfMatch.Curinga);

        resposta.Error!.Code.Should().Be("RascunhoRetificacao.NaoAberta");
        eventos.Should().BeEmpty();
        await cenario.Servicos.Documentos.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
    }

    private static Task<(Result Resposta, IEnumerable<object> Eventos)> ExecutarAsync(
        ServicosDoCongelamentoDeTeste servicos, OperacaoQueCongela operacao, Guid processoId) => operacao switch
        {
            OperacaoQueCongela.Publicar => servicos.PublicarAsync(
                new PublicarProcessoSeletivoCommand(processoId, "001/2026", null, null, DocumentoEditalId: Guid.CreateVersion7(), Ato: NovoAto())),
            OperacaoQueCongela.Retificar => servicos.RetificarAsync(
                new RetificarProcessoSeletivoCommand(processoId, "Correção do prazo", "001/2026-R1", null, null, DocumentoEditalId: Guid.CreateVersion7(), Ato: NovoAto())),
            _ => servicos.FecharRetificacaoAsync(
                new FecharRetificacaoCommand(processoId, "001/2026-R1", null, null, DocumentoEditalId: Guid.CreateVersion7(), Ato: NovoAto(), Precondicao: PrecondicaoIfMatch.Curinga)),
        };

    private static Task<(Result Resposta, IEnumerable<object> Eventos)> FecharComAsync(CenarioDeRecusaNoCongelamento cenario, PrecondicaoIfMatch precondicao) =>
        cenario.Servicos.FecharRetificacaoAsync(
            new FecharRetificacaoCommand(
                cenario.Processo.Id, "001/2026-R1", null, null, DocumentoEditalId: Guid.CreateVersion7(), Ato: NovoAto(), Precondicao: precondicao));
}
