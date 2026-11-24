namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class PrazoEValidadeDoDocumentoTests
{
    private static DataDeReferenciaDoDocumento Referencia =>
        DataDeReferenciaDoDocumento.Criar(TipoDaDataDeReferencia.DataDaApresentacao, null, null).Value!;

    [Fact]
    public void Criar_SemPrazoComDetalhes_Recusa() =>
        PrazoDeEmissao.Criar(EscolhaDoPrazoDeEmissao.SemPrazo, 1, null, null, null).Error!.Code
            .Should().Be(PrazoDeEmissaoErrorCodes.DetalhesSemPrazo);

    [Fact]
    public void Criar_ComPrazoIncompleto_Recusa()
    {
        PrazoDeEmissao.Criar(EscolhaDoPrazoDeEmissao.ComPrazo, 0, null, null, null).Error!.Code.Should().Be(PrazoDeEmissaoErrorCodes.ValorInvalido);
        PrazoDeEmissao.Criar(EscolhaDoPrazoDeEmissao.ComPrazo, 1, null, null, null).Error!.Code.Should().Be(PrazoDeEmissaoErrorCodes.UnidadeInvalida);
        PrazoDeEmissao.Criar(EscolhaDoPrazoDeEmissao.ComPrazo, 1, UnidadeDoPrazoDeEmissao.Dias, null, null).Error!.Code.Should().Be(DataDeReferenciaDoDocumentoErrorCodes.Ausente);
        PrazoDeEmissao.Criar(EscolhaDoPrazoDeEmissao.ComPrazo, 1, UnidadeDoPrazoDeEmissao.Dias, Referencia, null).Error!.Code.Should().Be(EfeitoNaAnaliseDoDocumentoErrorCodes.Invalido);
    }

    [Fact]
    public void DataMinimaDeEmissao_MesSemDiaEquivalente_UsaUltimoDia()
    {
        PrazoDeEmissao prazo = PrazoDeEmissao.Criar(EscolhaDoPrazoDeEmissao.ComPrazo, 1, UnidadeDoPrazoDeEmissao.Meses,
            Referencia, EfeitoNaAnaliseDoDocumento.RecusarDocumento).Value!;
        DateOnly referencia = new DateOnly(2024, 3, 31);
        prazo.DataMinimaDeEmissao(referencia).Should().Be(new DateOnly(2024, 2, 29));
        prazo.AtendeAoPrazo(new DateOnly(2024, 2, 29), referencia).Should().BeTrue();
        prazo.AtendeAoPrazo(new DateOnly(2024, 2, 28), referencia).Should().BeFalse();
    }

    [Fact]
    public void Criar_ValidadeSemExigenciaComDetalhes_Recusa() =>
        ValidadeDoDocumento.Criar(EscolhaDaValidade.NaoExigeDocumentoValido, Referencia, null).Error!.Code
            .Should().Be(ValidadeDoDocumentoErrorCodes.DetalhesSemExigencia);

    [Fact]
    public void AtendeAValidade_VencimentoNaReferencia_Aceita()
    {
        ValidadeDoDocumento validade = ValidadeDoDocumento.Criar(EscolhaDaValidade.ExigeDocumentoValido, Referencia,
            EfeitoNaAnaliseDoDocumento.AceitarComAviso).Value!;
        DateOnly data = new DateOnly(2026, 3, 1);
        validade.AtendeAValidade(data, data).Should().BeTrue();
        validade.AtendeAValidade(data.AddDays(-1), data).Should().BeFalse();
    }
    [Theory]
    [InlineData(UnidadeDoPrazoDeEmissao.Dias, 2024, 3, 30)]
    [InlineData(UnidadeDoPrazoDeEmissao.Anos, 2023, 3, 31)]
    public void DataMinimaDeEmissao_UnidadeDeclarada_SubtraiPrazo(UnidadeDoPrazoDeEmissao unidade, int ano, int mes, int dia)
    {
        PrazoDeEmissao prazo = PrazoDeEmissao.Criar(EscolhaDoPrazoDeEmissao.ComPrazo, 1, unidade,
            Referencia, EfeitoNaAnaliseDoDocumento.RecusarDocumento).Value!;
        prazo.DataMinimaDeEmissao(new DateOnly(2024, 3, 31)).Should().Be(new DateOnly(ano, mes, dia));
    }

    [Theory]
    [InlineData(UnidadeDoPrazoDeEmissao.Dias, 36500, "1926-12-19")]
    [InlineData(UnidadeDoPrazoDeEmissao.Meses, 1200, "1926-11-24")]
    [InlineData(UnidadeDoPrazoDeEmissao.Anos, 100, "1926-11-24")]
    public void Criar_PrazoNoLimiteDaUnidade_AceitaECalculaADataMinima(UnidadeDoPrazoDeEmissao unidade, int limite, string esperada)
    {
        PrazoDeEmissao prazo = new PrazoDeEmissaoBuilder().De(limite, unidade).Referenciado(Referencia).Construir();

        prazo.DataMinimaDeEmissao(new DateOnly(2026, 11, 24)).Should().Be(DateOnly.Parse(esperada, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData(UnidadeDoPrazoDeEmissao.Dias, 36501)]
    [InlineData(UnidadeDoPrazoDeEmissao.Meses, 1201)]
    [InlineData(UnidadeDoPrazoDeEmissao.Anos, 101)]
    [InlineData(UnidadeDoPrazoDeEmissao.Anos, int.MaxValue)]
    public void Criar_PrazoAcimaDoLimiteDaUnidade_Recusa(UnidadeDoPrazoDeEmissao unidade, int valor) =>
        PrazoDeEmissao.Criar(EscolhaDoPrazoDeEmissao.ComPrazo, valor, unidade, Referencia, EfeitoNaAnaliseDoDocumento.RecusarDocumento)
            .Error!.Code.Should().Be(PrazoDeEmissaoErrorCodes.ValorInvalido);

    [Fact]
    public void Criar_EscolhasInvalidas_Recusa()
    {
        PrazoDeEmissao.Criar((EscolhaDoPrazoDeEmissao)99, null, null, null, null).Error!.Code
            .Should().Be(PrazoDeEmissaoErrorCodes.EscolhaInvalida);
        ValidadeDoDocumento.Criar((EscolhaDaValidade)99, null, null).Error!.Code
            .Should().Be(ValidadeDoDocumentoErrorCodes.EscolhaInvalida);
    }

    [Fact]
    public void Criar_ValidadeExigidaSemDados_Recusa()
    {
        ValidadeDoDocumento.Criar(EscolhaDaValidade.ExigeDocumentoValido, null, null).Error!.Code
            .Should().Be(DataDeReferenciaDoDocumentoErrorCodes.Ausente);
        ValidadeDoDocumento.Criar(EscolhaDaValidade.ExigeDocumentoValido, Referencia, null).Error!.Code
            .Should().Be(EfeitoNaAnaliseDoDocumentoErrorCodes.Invalido);
    }

    [Fact]
    public void Criar_SemPrazoESemExigencia_Aceita()
    {
        PrazoDeEmissao prazo = PrazoDeEmissao.Criar(EscolhaDoPrazoDeEmissao.SemPrazo, null, null, null, null).Value!;
        ValidadeDoDocumento validade = ValidadeDoDocumento.Criar(EscolhaDaValidade.NaoExigeDocumentoValido, null, null).Value!;
        prazo.DataMinimaDeEmissao(new DateOnly(2026, 1, 1)).Should().BeNull();
        validade.AtendeAValidade(new DateOnly(2020, 1, 1), new DateOnly(2026, 1, 1)).Should().BeTrue();
    }
}
