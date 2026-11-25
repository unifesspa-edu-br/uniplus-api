namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class CategoriaDaRemocaoCodigosTests
{
    [Fact]
    public void Codigo_DeCadaValor_EODeclarado() =>
        Enum.GetValues<CategoriaDaRemocao>().ToDictionary(static valor => valor, static valor => valor.Codigo())
            .Should().Equal(new Dictionary<CategoriaDaRemocao, string>
            {
                [CategoriaDaRemocao.Incidente] = "INCIDENTE",
                [CategoriaDaRemocao.ErroDeComposicao] = "ERRO_DE_COMPOSICAO",
                [CategoriaDaRemocao.MudancaDeFuncao] = "MUDANCA_DE_FUNCAO",
            });
}
