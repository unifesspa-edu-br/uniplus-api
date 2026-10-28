namespace Unifesspa.UniPlus.Authorization.UnitTests.Decisao;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Decisao;
using Unifesspa.UniPlus.Authorization.Enums;

/// <summary>
/// Amarra o vocabulário dos enums do modelo ao valor canônico que o registro grava.
/// </summary>
public sealed class VocabularioDaDecisaoTests
{
    [Theory(DisplayName = "Todo motivo de negativa tem valor canônico em snake_case")]
    [MemberData(nameof(TodosOsMotivos))]
    public void MotivoNegativa_TemValorCanonicoEmSnakeCase(MotivoNegativa motivo)
    {
        string canonico = ValoresCanonicos.De(motivo);

        canonico.Should().MatchRegex("^[a-z]+(_[a-z]+)*$");
        canonico.Should().NotBe(motivo.ToString(),
            "gravar o identificador C# em vez do valor canônico é o desvio que este teste existe para pegar");
    }

    [Fact(DisplayName = "Os valores canônicos dos motivos são distintos entre si")]
    public void MotivoNegativa_ValoresCanonicosDistintos() =>
        Enum.GetValues<MotivoNegativa>()
            .Select(ValoresCanonicos.De)
            .Should().OnlyHaveUniqueItems();

    [Fact(DisplayName = "Toda fonte de concessão tem valor canônico")]
    public void FonteGrant_TodasTemValorCanonico() =>
        Enum.GetValues<FonteGrant>()
            .Select(ValoresCanonicos.De)
            .Should().OnlyHaveUniqueItems().And.AllSatisfy(
                static valor => valor.Should().MatchRegex("^[a-z]+(_[a-z]+)*$"));

    [Fact(DisplayName = "Toda origem de requisição tem valor canônico")]
    public void OrigemRequisicao_TodasTemValorCanonico() =>
        Enum.GetValues<OrigemRequisicao>()
            .Select(ValoresCanonicos.De)
            .Should().OnlyHaveUniqueItems().And.AllSatisfy(
                static valor => valor.Should().MatchRegex("^[a-z]+(-[a-z]+)*$"));

    [Fact(DisplayName = "A sensibilidade usa o mesmo vocabulário do catálogo de permissões")]
    public void Sensibilidade_UsaVocabularioDoCatalogo() =>
        Enum.GetValues<Sensibilidade>()
            .Select(ValoresCanonicos.De)
            .Should().BeEquivalentTo(["publica", "interna", "pessoal", "sensivel"]);

    [Fact(DisplayName = "Um valor fora do conjunto fechado é recusado, não convertido")]
    public void ValoresCanonicos_ValorForaDoConjunto_Recusa()
    {
        Action acao = () => ValoresCanonicos.De((MotivoNegativa)99);

        acao.Should().Throw<ArgumentException>();
    }

    public static TheoryData<MotivoNegativa> TodosOsMotivos()
    {
        TheoryData<MotivoNegativa> dados = new();
        foreach (MotivoNegativa motivo in Enum.GetValues<MotivoNegativa>())
        {
            dados.Add(motivo);
        }

        return dados;
    }
}
