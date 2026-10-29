namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.NotasDoEnem;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.NotasDoEnem;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// A janela de inscrição só é exigida de quem grava: a consulta de histórico de um processo que não recebe
/// inscrição pelo sistema continua respondendo.
/// </summary>
public sealed class ResolvedorDoEscopoDeNotasDoEnemTests
{
    private const string Edicao = "2025";

    private readonly CenarioDoEnemDeTeste _cenario = new(semFormularioDeInscricao: true, Edicao);

    private Task<Result<EscopoDeNotasDoEnem>> Resolver(bool exigeNotaDoEnem) =>
        ResolvedorDoEscopoDeNotasDoEnem.ResolverAsync(
            _cenario.ProcessoId, _cenario.EtapaDoEnem.Id, Edicao, exigeNotaDoEnem,
            _cenario.Certame, _cenario.Processos, _cenario.Codecs, CancellationToken.None);

    [Fact(DisplayName = "a consulta de histórico resolve o escopo de processo sem formulário de inscrição, sem janela")]
    public async Task Consulta_ProcessoSemInscricao_Resolve()
    {
        Result<EscopoDeNotasDoEnem> resultado = await Resolver(exigeNotaDoEnem: false);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Inscricao.Should().BeNull();
    }

    [Fact(DisplayName = "quem grava recebe a recusa de processo sem formulário de inscrição")]
    public async Task Gravacao_ProcessoSemInscricao_Recusa()
    {
        Result<EscopoDeNotasDoEnem> resultado = await Resolver(exigeNotaDoEnem: true);

        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.ProcessoSemInscricao);
    }
}
