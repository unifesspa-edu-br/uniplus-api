namespace Unifesspa.UniPlus.Authorization.UnitTests.Decisao;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Decisao;
using Unifesspa.UniPlus.Authorization.Enums;

/// <summary>
/// Amarra o que as permissões declaram ao que o ponto de decisão sabe verificar,
/// e o vocabulário dos enums ao valor canônico que o registro grava.
/// </summary>
/// <remarks>
/// Uma permissão que exija um campo de contexto desconhecido do backend viraria
/// negativa inexplicada; uma que declare verificação sem implementação estouraria
/// no meio de uma decisão. Nenhuma das duas falhas aparece ao escrever a
/// permissão — só em requisição —, e é isso que estes testes antecipam.
/// </remarks>
public sealed class VocabularioDaDecisaoTests
{
    [Fact(DisplayName = "Toda permissão exige apenas campos de contexto que a decisão sabe verificar")]
    public void Permissoes_ExigemContextoConhecido()
    {
        IEnumerable<string> exigidos = UniPlusPermissions.Todas
            .SelectMany(static permissao => permissao.EscopoContextoObrigatorio)
            .Distinct(StringComparer.Ordinal);

        exigidos.Should().BeSubsetOf(
            CamposDeContexto.NomesConhecidos,
            "um campo exigido e desconhecido do backend faz a permissão ser negada sem explicação");
    }

    [Fact(DisplayName = "Toda verificação declarada por uma permissão tem implementação de IPermissaoCheck")]
    public void Permissoes_DeclaramVerificacaoImplementada()
    {
        IEnumerable<string> declaradas = UniPlusPermissions.Todas
            .SelectMany(static permissao => permissao.VerificacoesDeContexto)
            .Distinct(StringComparer.Ordinal);

        declaradas.Should().BeSubsetOf(
            VerificacoesImplementadas(),
            "declarar verificação sem implementação só falharia em requisição, no meio de uma decisão");
    }

    [Fact(DisplayName = "A verificação canônica de concessão não é declarada por permissão alguma")]
    public void VerificacaoDeConcessao_NaoEDeclarada() =>
        UniPlusPermissions.Todas
            .SelectMany(static permissao => permissao.VerificacoesDeContexto)
            .Should().NotContain(
                GrantEfetivoAplicavelCheck.NomeCanonico,
                "a seleção de concessão é implícita em toda decisão — declará-la a executaria duas vezes");

    [Fact(DisplayName = "Todo código de permissão segue o formato modulo:recurso:acao em minúsculas")]
    public void Permissoes_SeguemOFormatoCanonico() =>
        UniPlusPermissions.Todas.Select(static permissao => permissao.Permissao)
            .Should().OnlyHaveUniqueItems()
            .And.AllSatisfy(static codigo => codigo.Should().MatchRegex(
                "^[a-z][a-z0-9-]*:[a-z][a-z0-9-]*:[a-z][a-z0-9-]*$"));

    private static IEnumerable<string> VerificacoesImplementadas() =>
        typeof(GrantEfetivoAplicavelCheck).Assembly
            .GetTypes()
            .Where(tipo => tipo is { IsAbstract: false, IsInterface: false }
                && typeof(IPermissaoCheck).IsAssignableFrom(tipo))
            .Select(tipo => (IPermissaoCheck)Activator.CreateInstance(tipo)!)
            .Select(check => check.Nome);

    [Fact(DisplayName = "Toda constante de permissão tem o requisito correspondente em Todas")]
    public void Permissoes_ConstanteECorrespondenteEstaoAlinhadas()
    {
        // Todas é derivada das propriedades da classe, então uma permissão nova
        // entra sozinha nas conferências. O que a derivação não pega é a
        // constante declarada sem o requisito ao lado — e é essa metade que
        // ficaria fora de tudo, sem nada acusar.
        IEnumerable<string> constantes = typeof(UniPlusPermissions)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(static campo => campo is { IsLiteral: true, IsInitOnly: false }
                && campo.FieldType == typeof(string))
            .Select(static campo => (string)campo.GetRawConstantValue()!);

        UniPlusPermissions.Todas.Select(static permissao => permissao.Permissao)
            .Should().BeEquivalentTo(constantes);
    }

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

    [Fact(DisplayName = "A sensibilidade usa o mesmo vocabulário do catálogo declarativo")]
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
