namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Os pares valor, código e rótulo dos enums do fato do candidato, escritos aqui de propósito: alterar um código é alterar
/// o contrato publicado e o dado gravado, e este teste é o que obriga a revisão.
/// </summary>
public sealed class CodigosDeContratoDoFatoTests
{
    private static readonly (EscopoFato Valor, string Codigo, string Rotulo)[] Escopos =
    [
        (EscopoFato.Candidato, "CANDIDATO", "Candidato"),
        (EscopoFato.MembroGrupo, "MEMBRO_GRUPO", "Membro do grupo"),
    ];

    private static readonly (FormatoTexto Valor, string Codigo, string Rotulo)[] Formatos =
    [
        (FormatoTexto.Livre, "LIVRE", "Texto livre"),
        (FormatoTexto.Cpf, "CPF", "CPF"),
        (FormatoTexto.Email, "EMAIL", "E-mail"),
        (FormatoTexto.Telefone, "TELEFONE", "Telefone"),
        (FormatoTexto.Cep, "CEP", "CEP"),
        (FormatoTexto.NomePessoa, "NOME_PESSOA", "Nome de pessoa"),
    ];

    private static readonly (ComponenteDeExibicao Valor, string Codigo, string Rotulo)[] Componentes =
    [
        (ComponenteDeExibicao.Moeda, "MOEDA", "Valor em reais"),
        (ComponenteDeExibicao.Situacao, "SITUACAO", "Situação"),
    ];

    [Fact(DisplayName = "Cada escopo do fato tem o código e o rótulo que o contrato publica")]
    public void EscopoFato_CodigoERotuloDeCadaValor() => ConferirPares(Escopos);

    [Fact(DisplayName = "Cada formato de texto tem o código e o rótulo que o contrato publica")]
    public void FormatoTexto_CodigoERotuloDeCadaValor() => ConferirPares(Formatos);

    [Fact(DisplayName = "Cada componente de exibição tem o código e o rótulo que o contrato publica")]
    public void ComponenteDeExibicao_CodigoERotuloDeCadaValor() => ConferirPares(Componentes);

    [Theory(DisplayName = "O código fora do contrato não é reconhecido: sem ignorar maiúsculas nem espaços")]
    [InlineData("candidato")]
    [InlineData(" CANDIDATO")]
    [InlineData("TODOS")]
    public void EscopoFato_CodigoForaDoContrato_NaoEhReconhecido(string codigo) =>
        CatalogoDe<EscopoFato>.TentarObter(codigo, out _).Should().BeFalse();

    [Theory(DisplayName = "O código de formato fora do contrato não é reconhecido")]
    [InlineData("livre")]
    [InlineData("NENHUM")]
    public void FormatoTexto_CodigoForaDoContrato_NaoEhReconhecido(string codigo) =>
        CatalogoDe<FormatoTexto>.TentarObter(codigo, out _).Should().BeFalse();

    [Theory(DisplayName = "O código de componente fora do contrato não é reconhecido")]
    [InlineData("moeda")]
    [InlineData("NENHUM")]
    public void ComponenteDeExibicao_CodigoForaDoContrato_NaoEhReconhecido(string codigo) =>
        CatalogoDe<ComponenteDeExibicao>.TentarObter(codigo, out _).Should().BeFalse();

    private static void ConferirPares<TEnum>((TEnum Valor, string Codigo, string Rotulo)[] esperados)
        where TEnum : struct, Enum
    {
        CatalogoDe<TEnum>.Valores().Should().Equal(esperados.Select(static item => item.Valor));
        foreach ((TEnum valor, string codigo, string rotulo) in esperados)
        {
            valor.Codigo().Should().Be(codigo);
            valor.Rotulo().Should().Be(rotulo);
            CatalogoDe<TEnum>.TentarObter(codigo, out TEnum obtido).Should().BeTrue();
            obtido.Should().Be(valor);
        }
    }
}
