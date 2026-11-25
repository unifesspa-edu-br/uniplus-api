namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class TipoDeGovernancaCodigosTests
{
    [Fact]
    public void Codigo_DeCadaValor_EODeclarado() =>
        Enum.GetValues<TipoDeGovernanca>().ToDictionary(static valor => valor, static valor => valor.Codigo())
            .Should().Equal(new Dictionary<TipoDeGovernanca, string>
            {
                [TipoDeGovernanca.GestorDeAcessos] = "GESTOR_DE_ACESSOS",
                [TipoDeGovernanca.RepresentanteDoControlador] = "REPRESENTANTE_DO_CONTROLADOR",
                [TipoDeGovernanca.GestorDeProtecaoDeDados] = "GESTOR_DE_PROTECAO_DE_DADOS",
            });
}
