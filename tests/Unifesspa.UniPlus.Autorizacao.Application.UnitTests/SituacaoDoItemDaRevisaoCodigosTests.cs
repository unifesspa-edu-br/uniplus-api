namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class SituacaoDoItemDaRevisaoCodigosTests
{
    [Fact]
    public void Codigo_DeCadaValor_EODeclarado() =>
        Enum.GetValues<SituacaoDoItemDaRevisao>().ToDictionary(static valor => valor, static valor => valor.Codigo())
            .Should().Equal(Esperados);

    private static readonly Dictionary<SituacaoDoItemDaRevisao, string> Esperados = new()
    {
        [SituacaoDoItemDaRevisao.Pendente] = "PENDENTE",
        [SituacaoDoItemDaRevisao.Confirmada] = "CONFIRMADA",
        [SituacaoDoItemDaRevisao.Revogada] = "REVOGADA",
        [SituacaoDoItemDaRevisao.ConcessaoEncerrada] = "CONCESSAO_ENCERRADA",
    };
}
