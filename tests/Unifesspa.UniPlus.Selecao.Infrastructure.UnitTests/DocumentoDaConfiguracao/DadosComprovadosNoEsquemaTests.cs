namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;

public sealed class DadosComprovadosNoEsquemaTests
{
    private const string PonteiroDoCampo = "/documentosExigidos/exigencias/0/dadosComprovados";

    private static readonly LeituraDoDocumentoDaConfiguracao Leitura = new(new EsquemaDoDocumentoDaConfiguracao());

    [Fact(DisplayName = "Os dados comprovados que o autor declara na exigência ficam no documento lido")]
    public void Ler_DadosComprovadosDeclarados_SaoConservados()
    {
        Result<DocumentoLido> resultado = Ler("[\"DADO_UM\", \"DADO_DOIS\"]");

        resultado.IsSuccess.Should().BeTrue();
        JsonArray dados = resultado.Value!.Documento["documentosExigidos"]!["exigencias"]![0]!["dadosComprovados"]!.AsArray();
        dados.Select(static dado => dado!.GetValue<string>()).Should().Equal("DADO_UM", "DADO_DOIS");
    }

    [Fact(DisplayName = "O código repetido na lista é recusado pela forma, no campo, como valor inválido")]
    public void Ler_CodigoRepetido_RecusaNoCampo()
    {
        Result<DocumentoLido> resultado = Ler("[\"DADO_UM\", \"DADO_UM\"]");

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(static erro =>
            erro.Field == PonteiroDoCampo && erro.Error.Code == DocumentoDaConfiguracaoErrorCodes.ValorInvalido);
    }

    [Fact(DisplayName = "O código acima do limite do domínio é recusado pela forma, no item, sem repetir o texto")]
    public void Ler_CodigoAcimaDoLimite_RecusaNoItem()
    {
        string codigo = new('A', FatoDoCatalogo.CodigoMaxLength + 1);

        Result<DocumentoLido> resultado = Ler($"[\"{codigo}\"]");

        resultado.IsFailure.Should().BeTrue();
        FieldError erro = resultado.Errors.Should().ContainSingle().Subject;
        erro.Field.Should().Be($"{PonteiroDoCampo}/0");
        erro.Error.Code.Should().Be(DocumentoDaConfiguracaoErrorCodes.ValorInvalido);
        erro.Error.Message.Should().NotContain(codigo);
    }

    [Fact(DisplayName = "O código com exatamente o limite do domínio é aceito")]
    public void Ler_CodigoNoLimite_EAceito() =>
        Ler($"[\"{new string('A', FatoDoCatalogo.CodigoMaxLength)}\"]").IsSuccess.Should().BeTrue();

    private static Result<DocumentoLido> Ler(string dadosComprovados) => Leitura.Ler($$"""
        { "formato": "uniplus.selecao.configuracao", "versaoFormato": 1,
          "documentosExigidos": { "exigencias": [ { "chave": "declaracao", "obrigatorio": true, "abrePrazoParaReenvio": false, "dadosComprovados": {{dadosComprovados}} } ] } }
        """);
}
