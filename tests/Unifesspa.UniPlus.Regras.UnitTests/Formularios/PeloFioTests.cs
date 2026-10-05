namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// O formulário portável é completo: cada cenário dos testes de avaliação roda de novo com a definição
/// levada ao portável, serializada como no fio, lida de volta e reconstruída — e a avaliação tem de ser
/// a mesma da definição original (ADR-0139). Uma regra nova que o avaliador use e o portável não
/// carregue faz algum desses cenários divergir.
/// </summary>
/// <summary>
/// A base dos testes que avaliam formulário: o ponto único por onde passa a avaliação, que as subclasses
/// pelo fio sobrescrevem para rodar cada cenário de novo com a definição reconstruída do portável.
/// </summary>
public abstract class TestesDeAvaliacao
{
    protected virtual AvaliacaoFormulario AvaliarDefinicao(DefinicaoFormulario definicao, EntradaAvaliacaoFormulario entrada) =>
        AvaliadorFormulario.Avaliar(definicao, entrada);
}

internal static class PeloFio
{
    public static AvaliacaoFormulario Avaliar(DefinicaoFormulario definicao, EntradaAvaliacaoFormulario entrada)
    {
        string json = JsonSerializer.Serialize(FormularioPortavel.De(definicao), JsonSerializerOptions.Web);
        FormularioPortavel lido = JsonSerializer.Deserialize<FormularioPortavel>(json, JsonSerializerOptions.Web)!;

        Result<AvaliacaoFormulario> peloFio = lido.Avaliar(entrada);
        peloFio.IsSuccess.Should().BeTrue(peloFio.Error?.Message);

        AvaliacaoFormulario original = AvaliadorFormulario.Avaliar(definicao, entrada);
        JsonSerializer.Serialize(peloFio.Value, JsonSerializerOptions.Web)
            .Should().Be(JsonSerializer.Serialize(original, JsonSerializerOptions.Web), "o portável não pode perder regra que o avaliador usa");

        return peloFio.Value!;
    }
}

public sealed class AvaliadorFormularioPeloFioTests : AvaliadorFormularioTests
{
    protected override AvaliacaoFormulario AvaliarDefinicao(DefinicaoFormulario definicao, EntradaAvaliacaoFormulario entrada) =>
        PeloFio.Avaliar(definicao, entrada);
}

public sealed class AgregadoNaAvaliacaoPeloFioTests : AgregadoNaAvaliacaoTests
{
    protected override AvaliacaoFormulario AvaliarDefinicao(DefinicaoFormulario definicao, EntradaAvaliacaoFormulario entrada) =>
        PeloFio.Avaliar(definicao, entrada);
}

public sealed class ImpedimentoPeloFioTests : ImpedimentoTests
{
    protected override AvaliacaoFormulario AvaliarDefinicao(DefinicaoFormulario definicao, EntradaAvaliacaoFormulario entrada) =>
        PeloFio.Avaliar(definicao, entrada);
}

public sealed class ConjuntoBasicoDaInscricaoPeloFioTests : ConjuntoBasicoDaInscricaoTests
{
    protected override AvaliacaoFormulario AvaliarDefinicao(DefinicaoFormulario definicao, EntradaAvaliacaoFormulario entrada) =>
        PeloFio.Avaliar(definicao, entrada);
}
