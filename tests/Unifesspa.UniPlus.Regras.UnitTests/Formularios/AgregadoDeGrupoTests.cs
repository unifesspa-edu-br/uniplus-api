namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// O agregado sobre o grupo repetível (UNI-REQ-0146, UNI-REQ-0075): a operação pelo domínio do fato
/// de membro e o resultado pelo estado do grupo e das ocorrências.
/// </summary>
public sealed class AgregadoDeGrupoTests
{
    [Theory]
    [InlineData("BOOLEANO", OperacaoAgregado.Existe)]
    [InlineData("CATEGORICO", OperacaoAgregado.ValoresPresentes)]
    [InlineData("NUMERICO", OperacaoAgregado.Nenhuma)]
    public void OperacaoDoDominio_SaiDoDominioDoFatoDeMembro(string dominio, OperacaoAgregado esperada) =>
        AgregadoDeGrupo.OperacaoDoDominio(dominio).Should().Be(esperada);

    [Theory]
    [InlineData(new[] { false, true }, true)]
    [InlineData(new[] { false, false }, false)]
    public void Calcular_Existe_VerdadeiroQuandoAlgumaOcorrenciaRespondeuVerdadeiro(bool[] respostas, bool esperado)
    {
        AvaliacaoGrupo grupo = Grupo(EstadoFato.Resolvido, [.. respostas.Select(static r => Resolvido(r))]);

        FatoResolvido agregado = AgregadoDeGrupo.Calcular(grupo, "MENOR_SOB_GUARDA", OperacaoAgregado.Existe);

        agregado.Valor!.Value.GetBoolean().Should().Be(esperado);
    }

    [Fact]
    public void Calcular_ValoresPresentes_UneOsValoresDasOcorrencias()
    {
        AvaliacaoGrupo grupo = Grupo(EstadoFato.Resolvido, [Resolvido("RURAL"), Resolvido("URBANA"), Resolvido("RURAL")]);

        FatoResolvido agregado = AgregadoDeGrupo.Calcular(grupo, "MENOR_SOB_GUARDA", OperacaoAgregado.ValoresPresentes);

        agregado.Valor!.Value.EnumerateArray().Select(static v => v.GetString()).Should().Equal("RURAL", "URBANA");
    }

    [Theory]
    [InlineData(EstadoFato.NaoAplicavel)]
    [InlineData(EstadoFato.NaoInformado)]
    public void Calcular_GrupoOcultoOuNaoInformado_ResolveVazio(EstadoFato estado)
    {
        AvaliacaoGrupo grupo = Grupo(estado, [Resolvido(true)]);

        AgregadoDeGrupo.Calcular(grupo, "MENOR_SOB_GUARDA", OperacaoAgregado.Existe).Valor!.Value.GetBoolean().Should().BeFalse();
        AgregadoDeGrupo.Calcular(grupo, "MENOR_SOB_GUARDA", OperacaoAgregado.ValoresPresentes).Valor!.Value.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public void Calcular_GrupoPendente_DeixaOAgregadoPendente()
    {
        AgregadoDeGrupo.Calcular(Grupo(EstadoFato.Indeterminado, []), "MENOR_SOB_GUARDA", OperacaoAgregado.Existe).Estado
            .Should().Be(EstadoFato.Indeterminado);
    }

    private static FatoResolvido Resolvido(object valor) => FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(valor));

    private static AvaliacaoGrupo Grupo(EstadoFato estado, IReadOnlyList<FatoResolvido> fatosDasOcorrencias) => new(
        "COMPOSICAO", "DADOS", Ternario.Verdadeiro, Ternario.Falso, estado, ContagemValida: true,
        [.. fatosDasOcorrencias.Select(static (f, i) => new AvaliacaoOcorrencia(
            $"m{i}", [], new Dictionary<string, FatoResolvido>(StringComparer.Ordinal) { ["MENOR_SOB_GUARDA"] = f }, EstadoFato.Resolvido))]);
}
