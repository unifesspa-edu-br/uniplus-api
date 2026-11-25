namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class ResultadoDaTentativaDeInstalacaoCodigosTests
{
    [Fact]
    public void Codigo_DeCadaValor_EODeclarado() =>
        Enum.GetValues<ResultadoDaTentativaDeInstalacao>().ToDictionary(static valor => valor, static valor => valor.Codigo())
            .Should().Equal(new Dictionary<ResultadoDaTentativaDeInstalacao, string>
            {
                [ResultadoDaTentativaDeInstalacao.Aceita] = "ACEITA",
                [ResultadoDaTentativaDeInstalacao.CodigoErrado] = "CODIGO_ERRADO",
                [ResultadoDaTentativaDeInstalacao.Expirada] = "EXPIRADA",
                [ResultadoDaTentativaDeInstalacao.SemPendencia] = "SEM_PENDENCIA",
                [ResultadoDaTentativaDeInstalacao.JaHaviaGestor] = "JA_HAVIA_GESTOR",
                [ResultadoDaTentativaDeInstalacao.CodigoReenviado] = "CODIGO_REENVIADO",
            });
}
