namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.PublicacaoDeTeste;

/// <summary>O processo que não existe é recusado, sem citar o identificador, pelos handlers que congelam, antes de qualquer leitura.</summary>
public sealed class RecusaDeProcessoInexistenteNosHandlersTests
{
    [Theory(DisplayName = "O processo inexistente é recusado sem citar o identificador, antes de ler documento, ato ou catálogo")]
    [InlineData(OperacaoQueCongela.Publicar)]
    [InlineData(OperacaoQueCongela.Retificar)]
    public async Task ProcessoInexistente_RecusaSemCitarOIdentificador(OperacaoQueCongela operacao)
    {
        Guid processoId = Guid.CreateVersion7();
        ServicosDoCongelamentoDeTeste servicos = new();

        (Result resposta, IEnumerable<object> eventos) = await ExecutarAsync(servicos, operacao, processoId);

        resposta.Errors.Should().ContainSingle().Which.Field.Should().BeNull();
        resposta.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
        resposta.Error.Message.Should().Be("Processo seletivo não encontrado.");
        resposta.Error.Message.Should().NotContain(processoId.ToString());
        eventos.Should().BeEmpty();
        await servicos.Documentos.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
        await servicos.NadaFoiGravadoAsync();
    }

    [Fact(DisplayName = "A recusa do processo inexistente é igual à do acesso negado, para não revelar que o processo existe")]
    public async Task ProcessoInexistente_RecusaIgualAoAcessoNegado()
    {
        ServicosDoCongelamentoDeTeste servicos = new();
        IVerificadorDeAcesso verificador = Substitute.For<IVerificadorDeAcesso>();
        verificador.ExigirAsync(default!, default, default).ReturnsForAnyArgs(ResultadoDoAcesso.Negado);

        (Result inexistente, _) = await ExecutarAsync(servicos, OperacaoQueCongela.Publicar, Guid.CreateVersion7());
        Result negado = await AcessoAoProcesso.ExigirConfigurarNaUnidadeAtualAsync(
            verificador, Guid.CreateVersion7(), Guid.CreateVersion7(), CancellationToken.None);

        inexistente.Error.Should().BeEquivalentTo(negado.Error);
        inexistente.Error.Should().BeEquivalentTo(AcessoAoProcesso.NaoEncontrado());
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
