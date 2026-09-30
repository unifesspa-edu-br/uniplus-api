namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Formularios;

public sealed class DependenciasDoFormularioTests
{
    private static DependenciasDoFormulario Dependencias(IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes) =>
        DependenciasDoFormulario.Criar(
            new Dictionary<string, int>(StringComparer.Ordinal) { ["COR_RACA"] = 0, ["BAIXA_RENDA"] = 1, ["CONCORRER_PPI"] = 2 },
            new HashSet<string>(["EGRESSO_ESCOLA_PUBLICA"], StringComparer.Ordinal),
            derivacoes);

    [Theory(DisplayName = "Regra cita campo anterior, fato conhecido antes do formulário ou derivado de fatos anteriores")]
    [InlineData("COR_RACA", 1, null)]
    [InlineData("BAIXA_RENDA", 1, RecusaDeCitacao.FatoPosterior)]
    [InlineData("EGRESSO_ESCOLA_PUBLICA", 0, null)]
    [InlineData("MODALIDADE", 2, null)]
    [InlineData("MODALIDADE", 1, RecusaDeCitacao.FatoPosterior)]
    [InlineData("FAIXA_ETARIA", 2, RecusaDeCitacao.FatoNaoConhecido)]
    public void Conferir(string citado, int posicaoDoCitante, RecusaDeCitacao? recusa)
    {
        DependenciasDoFormulario dependencias = Dependencias(new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)
        {
            ["MODALIDADE"] = ["EGRESSO_ESCOLA_PUBLICA", "COR_RACA", "BAIXA_RENDA"],
        });

        dependencias.Conferir(citado, posicaoDoCitante).Should().Be(recusa);
    }

    [Fact(DisplayName = "Derivado que depende de fato desconhecido ou de si mesmo não é conhecido")]
    public void Conferir_DerivadoSemDependenciasConhecidas_NaoConhecido()
    {
        DependenciasDoFormulario dependencias = Dependencias(new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)
        {
            ["A"] = ["COR_RACA", "B"],
            ["B"] = ["A"],
            ["C"] = ["COR_RACA", "FATO_DE_OUTRO_FORMULARIO"],
        });

        dependencias.Conferir("A", DependenciasDoFormulario.PosicaoDosTermos).Should().Be(RecusaDeCitacao.FatoNaoConhecido);
        dependencias.Conferir("C", DependenciasDoFormulario.PosicaoDosTermos).Should().Be(RecusaDeCitacao.FatoNaoConhecido);
    }

    [Fact(DisplayName = "Termo cita campo de qualquer ordem, inclusive a maior possível")]
    public void Conferir_TermoCitaCampoDeOrdemMaxima() =>
        DependenciasDoFormulario.Criar(
                new Dictionary<string, int>(StringComparer.Ordinal) { ["ULTIMO"] = int.MaxValue },
                new HashSet<string>(StringComparer.Ordinal),
                new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal))
            .Conferir("ULTIMO", DependenciasDoFormulario.PosicaoDosTermos).Should().BeNull();
}
