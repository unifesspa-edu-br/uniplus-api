namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CodigoBaseLegalBonusRegionalTests
{
    [Theory(DisplayName = "O código nasce do tipo e da identificação, sem acento nem pontuação, e sem repetir o tipo")]
    [InlineData(TipoInstrumentoNormativo.Portaria, "Portaria Unifesspa nº 2514/2023", "PORTARIA_UNIFESSPA_NO_2514_2023")]
    [InlineData(TipoInstrumentoNormativo.Lei, "Lei Estadual 123", "LEI_ESTADUAL_123")]
    [InlineData(TipoInstrumentoNormativo.Lei, "Estadual 123/2020", "LEI_ESTADUAL_123_2020")]
    [InlineData(TipoInstrumentoNormativo.Resolucao, "Resolução CONSEPE 45", "RESOLUCAO_CONSEPE_45")]
    [InlineData(TipoInstrumentoNormativo.InstrucaoNormativa, "Instrução Normativa 5/2022", "INSTRUCAO_NORMATIVA_5_2022")]
    [InlineData(TipoInstrumentoNormativo.Edital, "  Edital -- 12 / 2021  ", "EDITAL_12_2021")]
    public void Gerar_FormaOCodigo(TipoInstrumentoNormativo tipo, string identificacao, string esperado)
    {
        Result<CodigoBaseLegalBonusRegional> resultado = CodigoBaseLegalBonusRegional.Gerar(tipo, identificacao);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Valor.Should().Be(esperado);
    }

    [Fact(DisplayName = "Identificações que só diferem na pontuação geram o mesmo código")]
    public void Gerar_PontuacaoNaoDistingue()
    {
        string a = CodigoBaseLegalBonusRegional.Gerar(TipoInstrumentoNormativo.Portaria, "Portaria A-B").Value!.Valor;
        string b = CodigoBaseLegalBonusRegional.Gerar(TipoInstrumentoNormativo.Portaria, "portaria A B").Value!.Valor;

        a.Should().Be(b);
    }

    [Fact(DisplayName = "O mesmo texto em tipos diferentes gera códigos diferentes")]
    public void Gerar_TipoDistingue()
    {
        string lei = CodigoBaseLegalBonusRegional.Gerar(TipoInstrumentoNormativo.Lei, "12/2020").Value!.Valor;
        string decreto = CodigoBaseLegalBonusRegional.Gerar(TipoInstrumentoNormativo.Decreto, "12/2020").Value!.Valor;

        lei.Should().NotBe(decreto);
    }

    [Fact(DisplayName = "Código com exatamente 100 caracteres é aceito")]
    public void Gerar_NoLimite_Aceita()
    {
        string identificacao = new('A', 100 - "LEI_".Length);

        Result<CodigoBaseLegalBonusRegional> resultado = CodigoBaseLegalBonusRegional.Gerar(TipoInstrumentoNormativo.Lei, identificacao);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Valor.Should().HaveLength(100);
    }

    [Fact(DisplayName = "Código acima de 100 caracteres, contado com o prefixo do tipo, é recusado citando o limite")]
    public void Gerar_AcimaDoLimite_Falha()
    {
        string identificacao = new('A', 100 - "LEI_".Length + 1);

        Result<CodigoBaseLegalBonusRegional> resultado = CodigoBaseLegalBonusRegional.Gerar(TipoInstrumentoNormativo.Lei, identificacao);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(BaseLegalBonusRegionalErrorCodes.CodigoTamanho);
        resultado.Error.Message.Should().Contain("100");
    }

    [Theory(DisplayName = "Identificação sem nenhuma letra ou dígito não forma código")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("§§ -- //")]
    public void Gerar_SemLetraNemDigito_Falha(string identificacao)
    {
        Result<CodigoBaseLegalBonusRegional> resultado = CodigoBaseLegalBonusRegional.Gerar(TipoInstrumentoNormativo.Lei, identificacao);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(BaseLegalBonusRegionalErrorCodes.CodigoFormatoInvalido);
    }

    [Theory(DisplayName = "Referência por código no formato fechado é aceita, com Trim")]
    [InlineData("PORTARIA_UNIFESSPA_NO_2514_2023")]
    [InlineData("LEI_1")]
    public void Criar_FormatoValido_Aceita(string valor)
    {
        Result<CodigoBaseLegalBonusRegional> resultado = CodigoBaseLegalBonusRegional.Criar($"  {valor} ");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Valor.Should().Be(valor);
    }

    [Theory(DisplayName = "Referência ausente ou em branco retorna CodigoObrigatorio")]
    [InlineData(null)]
    [InlineData("  ")]
    public void Criar_EmBranco_Falha(string? valor)
    {
        Result<CodigoBaseLegalBonusRegional> resultado = CodigoBaseLegalBonusRegional.Criar(valor);

        resultado.Error!.Code.Should().Be(BaseLegalBonusRegionalErrorCodes.CodigoObrigatorio);
    }

    [Theory(DisplayName = "Referência fora do formato fechado retorna CodigoFormatoInvalido")]
    [InlineData("portaria_1")]
    [InlineData("1_PORTARIA")]
    [InlineData("PORTARIA 1")]
    [InlineData("A")]
    public void Criar_ForaDoFormato_Falha(string valor)
    {
        Result<CodigoBaseLegalBonusRegional> resultado = CodigoBaseLegalBonusRegional.Criar(valor);

        resultado.Error!.Code.Should().Be(BaseLegalBonusRegionalErrorCodes.CodigoFormatoInvalido);
    }
}
