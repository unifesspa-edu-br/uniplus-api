namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>Quando o grupo repetível aparece e quando é obrigatório (UNI-REQ-0146).</summary>
public sealed class ExibicaoEObrigatoriedadeTests
{
    [Fact(DisplayName = "A obrigatoriedade é obrigatória para o grupo existir")]
    public void Construtor_ObrigatoriedadeNula_Recusa() =>
        FluentActions.Invoking(static () => new ExibicaoEObrigatoriedade(null, null!)).Should().Throw<ArgumentNullException>();

    [Fact(DisplayName = "Os fatos citados pela exibição e pela obrigatoriedade saem uma vez só")]
    public void FatosCitados_ExibicaoEObrigatoriedadeCitandoOMesmoFato_SemRepeticao()
    {
        ExibicaoEObrigatoriedade regras = new(Predicado("COR_RACA"), Obrigatoriedade.Quando(Predicado("COR_RACA", "RENDA")));

        regras.FatosCitados.Should().BeEquivalentTo(["COR_RACA", "RENDA"]);
    }

    [Fact(DisplayName = "Sem exibição nem predicado de obrigatoriedade, o grupo não cita fato nem tem condição")]
    public void FatosCitados_SempreEmNunca_Vazio()
    {
        ExibicaoEObrigatoriedade regras = new(null, Obrigatoriedade.Nunca);

        regras.FatosCitados.Should().BeEmpty();
        regras.Condicoes.Should().BeEmpty();
    }

    [Fact(DisplayName = "As condições são as da exibição seguidas das do predicado de obrigatoriedade")]
    public void Condicoes_ExibicaoEObrigatoriedade_NaOrdemDasRegras()
    {
        ExibicaoEObrigatoriedade regras = new(Predicado("COR_RACA"), Obrigatoriedade.Quando(Predicado("RENDA")));

        regras.Condicoes.Select(static c => c.Fato).Should().Equal("COR_RACA", "RENDA");
    }

    private static PredicadoDnf Predicado(params string[] fatos) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas(
            [.. fatos.Select(static fato => (0, CondicaoDnf.Criar(fato, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!))]).Value!;
}
