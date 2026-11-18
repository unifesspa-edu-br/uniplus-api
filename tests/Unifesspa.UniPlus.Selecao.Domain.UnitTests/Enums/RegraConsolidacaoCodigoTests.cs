namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

public sealed class RegraConsolidacaoCodigoTests
{
    [Fact(DisplayName = "O rol contém exatamente as constantes públicas de código, sem esquecimento nem repetição")]
    public void Rol_ContemExatamenteAsConstantesPublicas()
    {
        string[] constantes =
        [
            .. typeof(RegraConsolidacaoCodigo)
                .GetFields()
                .Where(campo => campo.IsLiteral && campo.FieldType == typeof(string))
                .Select(campo => (string)campo.GetRawConstantValue()!),
        ];

        RegraConsolidacaoCodigo.Rol.Should().BeEquivalentTo(constantes);
        RegraConsolidacaoCodigo.Rol.Should().OnlyHaveUniqueItems();
    }

    [Fact(DisplayName = "As regras de conceito categórico entram no rol com o código canônico")]
    public void Rol_IncluiAsRegrasDeConceitoCategorico()
    {
        RegraConsolidacaoCodigo.Rol.Should().Contain(
        [
            "CONSOLIDACAO-CONCEITO-UNANIMIDADE",
            "CONSOLIDACAO-CONCEITO-MAIORIA-SIMPLES",
            "CONSOLIDACAO-CONCEITO-MAIORIA-ABSOLUTA",
        ]);
    }
}
