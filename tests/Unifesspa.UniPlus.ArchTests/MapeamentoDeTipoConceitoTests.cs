using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

namespace Unifesspa.UniPlus.ArchTests;

/// <summary>Confere os mapeamentos HTTP dos códigos de erro do tipo de conceito da avaliação.</summary>
public sealed class MapeamentoDeTipoConceitoTests
{
    public static TheoryData<string> CodigosDoTipoConceito()
    {
        TheoryData<string> dados = new();
        foreach (FieldInfo codigo in typeof(TipoConceitoErrorCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            dados.Add((string)codigo.GetRawConstantValue()!);
        return dados;
    }

    [Theory(DisplayName = "Todo código de erro do tipo de conceito tem mapeamento com code próprio do cadastro")]
    [MemberData(nameof(CodigosDoTipoConceito))]
    public void CodigoDeErro_TemMapeamentoComCodeProprio(string codigo)
    {
        Dictionary<string, DomainErrorMapping> mapeamentos = MapeamentosDaConfiguracao();

        mapeamentos.Should().ContainKey(codigo);
        mapeamentos[codigo].Code.Should().StartWith("uniplus.configuracao.tipo_conceito.");
    }

    [Theory(DisplayName = "O conflito do código responde 409, o tipo inexistente 404 e a recusa de corpo 422")]
    [InlineData(TipoConceitoErrorCodes.CodigoJaExiste, 409)]
    [InlineData(TipoConceitoErrorCodes.NaoEncontrado, 404)]
    [InlineData(TipoConceitoErrorCodes.NaturezaInvalida, 422)]
    [InlineData(TipoConceitoErrorCodes.EscalaNumericaInvalida, 422)]
    [InlineData(TipoConceitoErrorCodes.ValorCategoricoDuplicado, 422)]
    [InlineData(TipoConceitoErrorCodes.ParametrosIncompativeisComANatureza, 422)]
    public void CodigoDeErro_TemOStatusDaSuaEspecie(string codigo, int statusEsperado) =>
        MapeamentosDaConfiguracao()[codigo].Status.Should().Be(statusEsperado);

    private static Dictionary<string, DomainErrorMapping> MapeamentosDaConfiguracao()
    {
        Type registro = Assembly.Load("Unifesspa.UniPlus.Configuracao.API")
            .GetType("Unifesspa.UniPlus.Configuracao.API.Errors.ConfiguracaoDomainErrorRegistration", throwOnError: true)!;
        IDomainErrorRegistration instancia = (IDomainErrorRegistration)Activator.CreateInstance(registro, nonPublic: true)!;
        return instancia.GetMappings().ToDictionary(m => m.Key, m => m.Value, StringComparer.Ordinal);
    }
}
