namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;

public sealed class CaraterDaEtapaTests
{
    private static (string? Campo, string Codigo)[] Pares(IEnumerable<FieldError> erros) =>
        [.. erros.Select(static e => (e.Field, e.Error.Code))];

    [Theory(DisplayName = "O caráter pontua quando é classificatório ou ambos")]
    [InlineData(CaraterEtapa.Classificatoria, true)]
    [InlineData(CaraterEtapa.Ambas, true)]
    [InlineData(CaraterEtapa.Eliminatoria, false)]
    [InlineData(CaraterEtapa.Nenhum, false)]
    public void Pontua_ConformeOCarater(CaraterEtapa carater, bool pontua)
    {
        CaraterDaEtapa.Pontua(carater).Should().Be(pontua);
    }

    [Theory(DisplayName = "O caráter elimina quando é eliminatório ou ambos")]
    [InlineData(CaraterEtapa.Eliminatoria, true)]
    [InlineData(CaraterEtapa.Ambas, true)]
    [InlineData(CaraterEtapa.Classificatoria, false)]
    [InlineData(CaraterEtapa.Nenhum, false)]
    public void Elimina_ConformeOCarater(CaraterEtapa carater, bool elimina)
    {
        CaraterDaEtapa.Elimina(carater).Should().Be(elimina);
    }

    [Theory(DisplayName = "O caráter ausente ou fora do enum é recusado")]
    [InlineData(CaraterEtapa.Nenhum)]
    [InlineData((CaraterEtapa)4)]
    public void ValidarDeclarado_CaraterIndefinido_Recusa(CaraterEtapa carater)
    {
        List<FieldError> erros = CaraterDaEtapa.ValidarDeclarado(carater, peso: null, notaMinima: null);

        Pares(erros).Should().Equal(("carater", "EtapaProcesso.CaraterObrigatorio"));
    }

    [Theory(DisplayName = "O peso zero ou negativo é recusado em qualquer caráter")]
    [InlineData(0)]
    [InlineData(-1)]
    public void ValidarDeclarado_PesoNaoPositivo_Recusa(int peso)
    {
        List<FieldError> erros = CaraterDaEtapa.ValidarDeclarado(CaraterEtapa.Eliminatoria, peso, notaMinima: null);

        Pares(erros).Should().Equal(("peso", "EtapaProcesso.PesoInvalido"));
    }

    [Fact(DisplayName = "O peso positivo é recusado só na etapa apenas eliminatória")]
    public void ValidarDeclarado_PesoNaEtapaApenasEliminatoria_Recusa()
    {
        List<FieldError> erros = CaraterDaEtapa.ValidarDeclarado(CaraterEtapa.Eliminatoria, peso: 2m, notaMinima: null);

        Pares(erros).Should().Equal(("peso", "EtapaProcesso.PesoSemCaraterQuePontua"));
    }

    [Theory(DisplayName = "O peso positivo é aceito na etapa que pontua")]
    [InlineData(CaraterEtapa.Classificatoria)]
    [InlineData(CaraterEtapa.Ambas)]
    public void ValidarDeclarado_PesoNaEtapaQuePontua_NaoRecusa(CaraterEtapa carater)
    {
        CaraterDaEtapa.ValidarDeclarado(carater, peso: 2m, notaMinima: null).Should().BeEmpty();
    }

    [Fact(DisplayName = "A nota mínima negativa é recusada")]
    public void ValidarDeclarado_NotaMinimaNegativa_Recusa()
    {
        List<FieldError> erros = CaraterDaEtapa.ValidarDeclarado(CaraterEtapa.Ambas, peso: null, notaMinima: -0.5m);

        Pares(erros).Should().Equal(("notaMinima", "EtapaProcesso.NotaMinimaInvalida"));
    }

    [Fact(DisplayName = "A nota mínima é recusada na etapa apenas classificatória")]
    public void ValidarDeclarado_NotaMinimaNaEtapaApenasClassificatoria_Recusa()
    {
        List<FieldError> erros = CaraterDaEtapa.ValidarDeclarado(CaraterEtapa.Classificatoria, peso: null, notaMinima: 5m);

        Pares(erros).Should().Equal(("notaMinima", "EtapaProcesso.NotaMinimaSemCaraterQueElimina"));
    }

    [Theory(DisplayName = "A nota mínima é aceita na etapa que elimina")]
    [InlineData(CaraterEtapa.Eliminatoria)]
    [InlineData(CaraterEtapa.Ambas)]
    public void ValidarDeclarado_NotaMinimaNaEtapaQueElimina_NaoRecusa(CaraterEtapa carater)
    {
        CaraterDaEtapa.ValidarDeclarado(carater, peso: null, notaMinima: 5m).Should().BeEmpty();
    }

    [Fact(DisplayName = "As recusas do caráter declarado acumulam: caráter, peso e nota mínima, nesta ordem")]
    public void ValidarDeclarado_TodasAsRecusas_AcumulamNaOrdem()
    {
        List<FieldError> erros = CaraterDaEtapa.ValidarDeclarado(CaraterEtapa.Nenhum, peso: 0m, notaMinima: -1m);

        Pares(erros).Should().Equal(
            ("carater", "EtapaProcesso.CaraterObrigatorio"),
            ("peso", "EtapaProcesso.PesoInvalido"),
            ("notaMinima", "EtapaProcesso.NotaMinimaInvalida"));
    }

    [Theory(DisplayName = "O caráter declarado é conferido contra o que o tipo de etapa admite")]
    [InlineData(CaraterEtapa.Classificatoria, false, true, 1)]
    [InlineData(CaraterEtapa.Eliminatoria, true, false, 1)]
    [InlineData(CaraterEtapa.Ambas, false, false, 2)]
    [InlineData(CaraterEtapa.Ambas, true, true, 0)]
    [InlineData(CaraterEtapa.Eliminatoria, false, true, 0)]
    public void ValidarAdmitido_ConfereContraOQueOTipoAdmite(
        CaraterEtapa carater, bool admitePontuacao, bool admiteEliminacao, int violacoesEsperadas)
    {
        List<FieldError> erros = CaraterDaEtapa.ValidarAdmitido(
            carater, admitePontuacao, admiteEliminacao, "Análise Documental");

        erros.Should().HaveCount(violacoesEsperadas);
        erros.Should().AllSatisfy(erro =>
        {
            erro.Field.Should().Be("carater");
            erro.Error.Code.Should().Be(EtapaProcesso.CaraterNaoAdmitidoPeloTipo);
        });
    }

    [Fact(DisplayName = "A recusa de caráter nomeia o tipo de etapa que não o admite")]
    public void ValidarAdmitido_Recusa_NomeiaOTipo()
    {
        List<FieldError> erros = CaraterDaEtapa.ValidarAdmitido(
            CaraterEtapa.Classificatoria, admitePontuacao: false, admiteEliminacao: true, "Análise Documental");

        erros.Should().ContainSingle()
            .Which.Error.Message.Should().Contain("Análise Documental");
    }
}
