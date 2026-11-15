namespace Unifesspa.UniPlus.Selecao.IntegrationTests.NotasDoEnem;

using AwesomeAssertions;

public sealed class NotaEmLogTests
{
    [Theory(DisplayName = "A nota escrita como valor próprio no log é reconhecida")]
    [InlineData("nota=960")]
    [InlineData("960")]
    [InlineData("candidato com nota 960, linha 4")]
    [InlineData("{\"nota\":960}")]
    public void Aparece_NotaComoValorProprio_Reconhece(string log) =>
        NotaEmLog.Aparece(log, "960").Should().BeTrue();

    [Theory(DisplayName = "A sequência da nota dentro de texto aleatório ou de outro número não é nota escrita")]
    [InlineData("boundary=a960b7c1-2d3e-4f50-8a61-0c9d8e7f6a5b")]
    [InlineData("boundary=0b7c1960-2d3e-4f50-8a61-0c9d8e7f6a5b")]
    [InlineData("respondeu em 39.9608 ms")]
    [InlineData("lote 1960")]
    [InlineData("trace-960-x")]
    public void Aparece_SequenciaDentroDeOutroTexto_NaoReconhece(string log) =>
        NotaEmLog.Aparece(log, "960").Should().BeFalse();
}
