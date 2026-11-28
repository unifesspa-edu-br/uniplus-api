namespace Unifesspa.UniPlus.Kernel.UnitTests.Domain.Cidades;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A regra de cidade devolve as causas sem campo; os nomes que o pedido dá aos três dados dizem qual deles cada causa
/// descreve (ADR-0023).
/// </summary>
public sealed class CamposDaCidadeTests
{
    private static readonly CamposDaCidade Campos = new("localidadeCodigoIbge", "localidadeNome", "localidadeUf");

    [Fact(DisplayName = "Cada causa da regra de cidade aponta o campo do pedido que ela descreve, na ordem da regra")]
    public void Apontar_TresCausas_CadaUmaNoSeuCampo()
    {
        Result regra = ReferenciaCidadeGeo.Validar("12", "  ", string.Empty);

        IReadOnlyList<FieldError> apontadas = Campos.Apontar(regra.Errors);

        apontadas.Select(static causa => (causa.Field, causa.Error.Code)).Should().Equal(
            ("localidadeNome", CidadeReferenciaErrorCodes.NomeObrigatorio),
            ("localidadeUf", CidadeReferenciaErrorCodes.UfObrigatoria),
            ("localidadeCodigoIbge", CidadeReferenciaErrorCodes.CodigoIbgeFormatoInvalido));
    }

    [Theory(DisplayName = "Nome com caractere inválido ou longo demais aponta o nome, e UF incoerente aponta a UF")]
    [InlineData(CidadeReferenciaErrorCodes.NomeCaractereInvalido, "localidadeNome")]
    [InlineData(CidadeReferenciaErrorCodes.NomeTamanho, "localidadeNome")]
    [InlineData(CidadeReferenciaErrorCodes.UfIncoerente, "localidadeUf")]
    [InlineData(CidadeReferenciaErrorCodes.CodigoIbgeObrigatorio, "localidadeCodigoIbge")]
    public void Apontar_CodigoDaCausa_EscolheOCampo(string codigo, string campoEsperado)
    {
        FieldError causa = new(null, new DomainError(codigo, "Mensagem."));

        Campos.Apontar([causa]).Should().ContainSingle().Which.Field.Should().Be(campoEsperado);
    }
}
