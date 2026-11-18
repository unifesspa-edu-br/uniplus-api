namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.PublicacaoDeTeste;

/// <summary>O processo que não existe é recusado com a mesma mensagem pelos handlers que congelam, antes de qualquer leitura.</summary>
public sealed class RecusaDeProcessoInexistenteNosHandlersTests
{
    [Theory(DisplayName = "O processo inexistente é recusado com a mesma mensagem, antes de ler documento, ato ou catálogo")]
    [InlineData(OperacaoQueCongela.Publicar)]
    [InlineData(OperacaoQueCongela.Retificar)]
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

    private static Task<(Result Resposta, IEnumerable<object> Eventos)> ExecutarAsync(
        ServicosDoCongelamentoDeTeste servicos, OperacaoQueCongela operacao, Guid processoId) => operacao switch
        {
            OperacaoQueCongela.Publicar => servicos.PublicarAsync(
                new PublicarProcessoSeletivoCommand(processoId, "001/2026", null, null, DocumentoEditalId: Guid.CreateVersion7(), Ato: NovoAto())),
            _ => servicos.RetificarAsync(
                new RetificarProcessoSeletivoCommand(processoId, "Correção do prazo", "001/2026-R1", null, null, DocumentoEditalId: Guid.CreateVersion7(), Ato: NovoAto())),
        };
}
