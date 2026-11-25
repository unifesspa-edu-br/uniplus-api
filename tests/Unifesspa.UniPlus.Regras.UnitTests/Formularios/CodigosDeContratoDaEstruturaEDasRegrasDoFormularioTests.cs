namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Os pares valor, código e rótulo dos enums da estrutura e das regras do formulário, escritos aqui de propósito: alterar
/// um código é alterar o contrato publicado e o dado gravado. O código fora do contrato, em minúscula ou com espaço, não é
/// reconhecido.
/// </summary>
public sealed class CodigosDeContratoDaEstruturaEDasRegrasDoFormularioTests
{
    [Theory(DisplayName = "Cada tipo de página tem o código e o rótulo do contrato")]
    [InlineData(TipoPaginaFormulario.Secao, "SECAO", "Seção")]
    [InlineData(TipoPaginaFormulario.Bloco, "BLOCO", "Bloco do sistema")]
    public void TipoPaginaFormulario_ParDeCadaValor(TipoPaginaFormulario valor, string codigo, string rotulo) =>
        ConferirPar(valor, codigo, rotulo);

    [Theory(DisplayName = "Cada bloco do sistema tem o código e o rótulo do contrato")]
    [InlineData(BlocoSistema.ComprovacaoDocumental, "COMPROVACAO_DOCUMENTAL", "Comprovação documental")]
    [InlineData(BlocoSistema.ModalidadesCalculadas, "MODALIDADES_CALCULADAS", "Modalidades calculadas")]
    [InlineData(BlocoSistema.RevisaoEAceite, "REVISAO_E_ACEITE", "Revisão e aceite")]
    public void BlocoSistema_ParDeCadaValor(BlocoSistema valor, string codigo, string rotulo) =>
        ConferirPar(valor, codigo, rotulo);

    [Theory(DisplayName = "Cada forma de obrigatoriedade tem o código e o rótulo do contrato")]
    [InlineData(TipoObrigatoriedade.Sempre, "SEMPRE", "Sempre obrigatório")]
    [InlineData(TipoObrigatoriedade.Nunca, "NUNCA", "Nunca obrigatório")]
    [InlineData(TipoObrigatoriedade.Quando, "QUANDO", "Obrigatório quando a condição é verdadeira")]
    public void TipoObrigatoriedade_ParDeCadaValor(TipoObrigatoriedade valor, string codigo, string rotulo) =>
        ConferirPar(valor, codigo, rotulo);

    [Theory(DisplayName = "Cada tipo de restrição de valor tem o código e o rótulo do contrato")]
    [InlineData(TipoRestricaoValor.FaixaNumerica, "FAIXA_NUMERICA", "Faixa numérica")]
    [InlineData(TipoRestricaoValor.TamanhoTexto, "TAMANHO_TEXTO", "Tamanho do texto")]
    [InlineData(TipoRestricaoValor.OpcoesPermitidas, "OPCOES_PERMITIDAS", "Opções permitidas")]
    [InlineData(TipoRestricaoValor.OpcoesDasRespostas, "OPCOES_DAS_RESPOSTAS", "Opções formadas pelas respostas")]
    [InlineData(TipoRestricaoValor.MunicipiosDaUf, "MUNICIPIOS_DA_UF", "Municípios do estado")]
    [InlineData(TipoRestricaoValor.FormatoTexto, "FORMATO_TEXTO", "Formato do texto")]
    [InlineData(TipoRestricaoValor.FaixaDeData, "FAIXA_DE_DATA", "Faixa de data")]
    public void TipoRestricaoValor_ParDeCadaValor(TipoRestricaoValor valor, string codigo, string rotulo) =>
        ConferirPar(valor, codigo, rotulo);

    [Theory(DisplayName = "Cada operação do agregado tem o código e o rótulo do contrato")]
    [InlineData(OperacaoAgregado.Existe, "EXISTE", "Existe alguma ocorrência")]
    [InlineData(OperacaoAgregado.ValoresPresentes, "VALORES_PRESENTES", "Valores presentes nas ocorrências")]
    public void OperacaoAgregado_ParDeCadaValor(OperacaoAgregado valor, string codigo, string rotulo) =>
        ConferirPar(valor, codigo, rotulo);

    [Fact(DisplayName = "Os valores de cada enum são exatamente os do contrato, sem membro de ausência")]
    public void CadaEnum_TemSoOsMembrosDoContrato()
    {
        CatalogoDe<TipoPaginaFormulario>.Valores().Should().Equal(TipoPaginaFormulario.Secao, TipoPaginaFormulario.Bloco);
        CatalogoDe<BlocoSistema>.Valores().Should().Equal(
            BlocoSistema.ComprovacaoDocumental, BlocoSistema.ModalidadesCalculadas, BlocoSistema.RevisaoEAceite);
        CatalogoDe<TipoObrigatoriedade>.Valores().Should().Equal(
            TipoObrigatoriedade.Sempre, TipoObrigatoriedade.Nunca, TipoObrigatoriedade.Quando);
        CatalogoDe<TipoRestricaoValor>.Valores().Should().HaveCount(7);
        CatalogoDe<OperacaoAgregado>.Valores().Should().Equal(OperacaoAgregado.Existe, OperacaoAgregado.ValoresPresentes);
    }

    [Theory(DisplayName = "Código fora do contrato, em minúscula, com espaço, numérico ou ausente, não é reconhecido")]
    [InlineData("secao")]
    [InlineData("Secao")]
    [InlineData(" SECAO")]
    [InlineData("SECAO ")]
    [InlineData("1")]
    [InlineData("")]
    [InlineData(null)]
    public void CodigoForaDoContrato_NaoEReconhecidoEmNenhumEnum(string? codigo)
    {
        CatalogoDe<TipoPaginaFormulario>.TentarObter(codigo, out _).Should().BeFalse();
        CatalogoDe<BlocoSistema>.TentarObter(codigo, out _).Should().BeFalse();
        CatalogoDe<TipoObrigatoriedade>.TentarObter(codigo, out _).Should().BeFalse();
        CatalogoDe<TipoRestricaoValor>.TentarObter(codigo, out _).Should().BeFalse();
        CatalogoDe<OperacaoAgregado>.TentarObter(codigo, out _).Should().BeFalse();
    }

    [Theory(DisplayName = "O código em minúscula de cada enum não é reconhecido")]
    [InlineData("sempre")]
    [InlineData("existe")]
    [InlineData("faixa_numerica")]
    [InlineData("bloco")]
    [InlineData("revisao_e_aceite")]
    public void CodigoEmMinuscula_NaoEReconhecido(string codigo)
    {
        CatalogoDe<TipoPaginaFormulario>.TentarObter(codigo, out _).Should().BeFalse();
        CatalogoDe<BlocoSistema>.TentarObter(codigo, out _).Should().BeFalse();
        CatalogoDe<TipoObrigatoriedade>.TentarObter(codigo, out _).Should().BeFalse();
        CatalogoDe<TipoRestricaoValor>.TentarObter(codigo, out _).Should().BeFalse();
        CatalogoDe<OperacaoAgregado>.TentarObter(codigo, out _).Should().BeFalse();
    }

    private static void ConferirPar<TEnum>(TEnum valor, string codigo, string rotulo)
        where TEnum : struct, Enum
    {
        valor.Codigo().Should().Be(codigo);
        valor.Rotulo().Should().Be(rotulo);
        CatalogoDe<TEnum>.TentarObter(codigo, out TEnum lido).Should().BeTrue();
        lido.Should().Be(valor);
    }
}
