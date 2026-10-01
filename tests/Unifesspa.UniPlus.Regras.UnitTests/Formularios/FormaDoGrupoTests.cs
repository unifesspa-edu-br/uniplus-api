namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>A forma do grupo repetível, a mesma no processo e no modelo (UNI-REQ-0146).</summary>
public sealed class FormaDoGrupoTests
{
    [Theory]
    [InlineData(0, 1, null)]
    [InlineData(0, FormaDoGrupo.MaximoDeOcorrencias, null)]
    [InlineData(-1, 3, GrupoFormularioErrorCodes.ContagemIncoerente)]
    [InlineData(0, 0, GrupoFormularioErrorCodes.ContagemIncoerente)]
    [InlineData(3, 2, GrupoFormularioErrorCodes.ContagemIncoerente)]
    [InlineData(0, FormaDoGrupo.MaximoDeOcorrencias + 1, GrupoFormularioErrorCodes.ContagemIncoerente)]
    public void Conferir_MinimoEMaximo_CabemEntreZeroEOTeto(int minimo, int maximo, string? esperado)
    {
        List<FieldError> erros = Conferir(minimo: minimo, maximo: maximo);

        erros.Select(static e => e.Error.Code).Should().Equal(esperado is null ? [] : [esperado]);
        erros.Select(static e => e.Field).Should().AllBe(minimo < 0 ? "minimo" : "maximo", "a recusa aponta o campo que está fora");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(FormaDoGrupo.MaximoDeSubitens + 1)]
    public void Conferir_QuantidadeDeCampos_ForaDoLimite_Recusa(int quantidade)
    {
        List<FieldError> erros = Conferir(subitens: [.. Enumerable.Range(0, quantidade).Select(static i => $"CAMPO_{i}")]);

        erros.Should().ContainSingle().Which.Error.Code.Should().Be(GrupoFormularioErrorCodes.SubitensForaDoLimite);
    }

    [Theory]
    [InlineData("PARENTESCO", false)]
    [InlineData("COMPOSICAO", false)]
    [InlineData("PARENTESCO", true)]
    public void Conferir_RegraDoGrupoQueCitaOProprioGrupo_RecusaNaRegraQueCita(string citado, bool naObrigatoriedade)
    {
        List<FieldError> erros = naObrigatoriedade
            ? Conferir(obrigatoriedade: Obrigatoriedade.Quando(Predicado(citado)))
            : Conferir(citados: [citado]);

        FieldError erro = erros.Should().ContainSingle().Subject;
        erro.Error.Code.Should().Be(GrupoFormularioErrorCodes.RegraAutorreferente);
        erro.Field.Should().Be(naObrigatoriedade ? "predicadoObrigatoriedade" : "exibicao");
    }

    [Fact]
    public void Conferir_ViolacoesIndependentes_SaemJuntas()
    {
        List<FieldError> erros = FormaDoGrupo.Conferir(" ", -1, null, 2, 1, [], [], Obrigatoriedade.Sempre);

        erros.Select(static e => e.Error.Code).Should().BeEquivalentTo(
        [
            GrupoFormularioErrorCodes.CodigoInvalido,
            GrupoFormularioErrorCodes.OrdemInvalida,
            GrupoFormularioErrorCodes.RotuloInvalido,
            GrupoFormularioErrorCodes.ContagemIncoerente,
            GrupoFormularioErrorCodes.SubitensForaDoLimite,
        ]);
    }

    private static List<FieldError> Conferir(
        int minimo = 0,
        int maximo = 5,
        IReadOnlyCollection<string>? subitens = null,
        IEnumerable<string>? citados = null,
        Obrigatoriedade? obrigatoriedade = null) =>
        FormaDoGrupo.Conferir(
            "COMPOSICAO", 1, "Composição familiar", minimo, maximo, subitens ?? ["PARENTESCO"], citados ?? [], obrigatoriedade ?? Obrigatoriedade.Sempre);

    private static PredicadoDnf Predicado(string fato) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar(fato, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!;
}
