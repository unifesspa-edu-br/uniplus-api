namespace Unifesspa.UniPlus.Regras.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Derivado categórico por regra (Story #927): a união dos códigos que as regras ativas contribuem,
/// indeterminado enquanto uma dependência não se sabe, e a forma que o cadastro da regra recusa.
/// </summary>
public sealed class MotorDerivacaoCategoricoTests
{
    private static readonly string[] Dominio = ["AMPLA", "RESERVA", "RESERVA_COM_RENDA"];

    private static readonly RegrasDerivacaoFato Regras = RegrasDerivacaoFato.Criar(
        "VAGA",
        [Ancora("AMPLA"), Regra("RESERVA", ("OPTOU", true)), Regra("RESERVA_COM_RENDA", ("OPTOU", true), ("RENDA", true))],
        ["OPTOU", "RENDA"],
        Dominio).Value!;

    private static FatoResolvido Sim => FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(true));

    private static FatoResolvido Nao => FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(false));

    [Fact(DisplayName = "A união traz cada código das regras ativas uma vez, com a âncora sempre")]
    public void Uniao_DasRegrasAtivas() =>
        Derivar(("OPTOU", Sim), ("RENDA", Sim)).Should().Equal("AMPLA", "RESERVA", "RESERVA_COM_RENDA");

    [Fact(DisplayName = "Dependência não aplicável não bloqueia: a cláusula que a cita não contribui")]
    public void NaoAplicavel_NaoContribui() =>
        Derivar(("OPTOU", Sim), ("RENDA", FatoResolvido.NaoAplicavel())).Should().Equal("AMPLA", "RESERVA");

    [Fact(DisplayName = "Dependência indeterminada torna o derivado indeterminado")]
    public void DependenciaIndeterminada_Indeterminado() =>
        MotorDerivacao.Derivar(Regras, Fatos(("OPTOU", Sim), ("RENDA", FatoResolvido.Indeterminado()))).Estado
            .Should().Be(EstadoFato.Indeterminado, "um dependente que não se sabe bloqueia o derivado inteiro");

    [Fact(DisplayName = "Dependência ausente torna o derivado indeterminado")]
    public void DependenciaAusente_Indeterminado() =>
        MotorDerivacao.Derivar(Regras, Fatos(("OPTOU", Nao))).Estado.Should().Be(EstadoFato.Indeterminado);

    [Fact(DisplayName = "Dependente resolvido com valor de tipo incoerente torna o derivado indeterminado")]
    public void TipoIncoerente_Indeterminado() =>
        MotorDerivacao.Derivar(Regras, Fatos(("OPTOU", FatoResolvido.Resolvido(JsonSerializer.SerializeToElement("SIM"))), ("RENDA", Nao))).Estado
            .Should().Be(EstadoFato.Indeterminado, "o valor que o motor não sabe comparar não é regra inativa");

    [Fact(DisplayName = "Código contribuído fora do domínio é recusado no cadastro")]
    public void ContribuiForaDoDominio_Recusado()
    {
        Result<RegrasDerivacaoFato> resultado = RegrasDerivacaoFato.Criar("VAGA", [Ancora("OUTRA")], [], Dominio);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(RegrasDerivacaoFatoErrorCodes.ContribuiForaDoDominio);
    }

    [Theory(DisplayName = "As dependências declaradas são exatamente os fatos citados")]
    [InlineData("OPTOU", "RENDA")]
    [InlineData("OPTOU")]
    public void DependenciasIncoerentes_Recusadas(params string[] declaradas)
    {
        Result<RegrasDerivacaoFato> resultado = RegrasDerivacaoFato.Criar(
            "VAGA", [Regra("RESERVA", ("OPTOU", true)), Regra("RESERVA_COM_RENDA", ("CONCORRER", true))], declaradas, Dominio);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(RegrasDerivacaoFatoErrorCodes.DependenciasIncoerentes);
    }

    [Fact(DisplayName = "Derivação que cita o próprio fato é recusada")]
    public void Autorreferente_Recusada()
    {
        Result<RegrasDerivacaoFato> resultado = RegrasDerivacaoFato.Criar("VAGA", [Regra("AMPLA", ("VAGA", true))], ["VAGA"], Dominio);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(RegrasDerivacaoFatoErrorCodes.DerivacaoAutorreferente);
    }

    private static string[] Derivar(params (string Fato, FatoResolvido Estado)[] fatos)
    {
        ResultadoDerivacao resultado = MotorDerivacao.Derivar(Regras, Fatos(fatos));
        resultado.Estado.Should().Be(EstadoFato.Resolvido);
        return [.. resultado.Valores.Order(StringComparer.Ordinal)];
    }

    private static Dictionary<string, FatoResolvido> Fatos(params (string Fato, FatoResolvido Estado)[] fatos) =>
        fatos.ToDictionary(static f => f.Fato, static f => f.Estado, StringComparer.Ordinal);

    private static RegraDerivacao Ancora(string contribui) =>
        RegraDerivacao.Criar(PredicadoDnf.SemClausula, contribui).Value!;

    private static RegraDerivacao Regra(string contribui, params (string Fato, bool Valor)[] atomos) =>
        RegraDerivacao.Criar(
            PredicadoDnf.CriarDeCondicoesAgrupadas([.. atomos.Select(static a => (0, CondicaoDnf.Criar(
                a.Fato, Operador.Igual, JsonSerializer.SerializeToElement(a.Valor)).Value!))]).Value!,
            contribui).Value!;
}
