using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Contratos.UnitTests.Hosting;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

namespace Unifesspa.UniPlus.Contratos.UnitTests;

/// <summary>Confere os mapeamentos HTTP dos códigos de erro do tipo de conceito da avaliação.</summary>
public sealed class MapeamentoDeTipoConceitoTests
{
    [Theory(DisplayName = "O conflito do código responde 409, o tipo inexistente 404 e a recusa de corpo 422")]
    [InlineData(TipoConceitoErrorCodes.CodigoJaExiste, 409)]
    [InlineData(TipoConceitoErrorCodes.NaoEncontrado, 404)]
    [InlineData(TipoConceitoErrorCodes.NaturezaInvalida, 422)]
    [InlineData(TipoConceitoErrorCodes.EscalaNumericaInvalida, 422)]
    [InlineData(TipoConceitoErrorCodes.ValorCategoricoDuplicado, 422)]
    [InlineData(TipoConceitoErrorCodes.ParametrosIncompativeisComANatureza, 422)]
    public void CodigoDeErro_TemOStatusDaSuaEspecie(string codigo, int statusEsperado) =>
        MapeamentosDaConfiguracao()[codigo].Status.Should().Be(statusEsperado);

    private static Dictionary<string, DomainErrorMapping> MapeamentosDaConfiguracao() =>
        HostsDoProdutoIniciados.Obter(HostsDoProdutoIniciados.UniPlus).ProvedorDeServicos
            .GetServices<IDomainErrorRegistration>().SelectMany(registro => registro.GetMappings())
            .GroupBy(mapeamento => mapeamento.Key, StringComparer.Ordinal)
            .ToDictionary(grupo => grupo.Key, grupo => grupo.Last().Value, StringComparer.Ordinal);
}
