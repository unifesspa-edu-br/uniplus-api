namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using System.Text;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;

public sealed class TipoConceitoTests
{
    private const string Nulo = "valor\0invalido";

    [Fact(DisplayName = "Cria tipo numérico ativo, com a escala e sem valores categóricos")]
    public void Criar_Numerico_GuardaAEscala()
    {
        Result<TipoConceito> resultado = TipoConceito.Criar("NOTA_ENTREVISTA", DefinicoesDeTipoConceito.Numerica());

        TipoConceito tipo = resultado.Value!;
        resultado.IsSuccess.Should().BeTrue();
        (tipo.Ativo, tipo.Natureza, tipo.Codigo).Should().Be((true, FormaDoConceito.Numerico, "NOTA_ENTREVISTA"));
        tipo.EscalaMinima.Should().Be(0m);
        tipo.EscalaMaxima.Should().Be(10m);
        tipo.CasasDecimais.Should().Be(2);
        tipo.Valores.Should().BeEmpty();
    }

    [Fact(DisplayName = "Cria tipo categórico com os valores na ordem informada e sem escala")]
    public void Criar_Categorico_GuardaOsValoresEmOrdem()
    {
        TipoConceito tipo = TipoConceito.Criar("PARECER_ANALISE", DefinicoesDeTipoConceito.Categorica()).Value!;

        tipo.Natureza.Should().Be(FormaDoConceito.PorOpcoes);
        tipo.Valores.Select(v => (v.Posicao, v.Codigo, v.Rotulo, v.ContaComoAprovacao))
            .Should().Equal((1, "DEFERIDO", "Deferido", true), (2, "INDEFERIDO", "Indeferido", false));
        tipo.EscalaMinima.Should().BeNull();
        tipo.EscalaMaxima.Should().BeNull();
        tipo.CasasDecimais.Should().BeNull();
    }

    [Fact(DisplayName = "Normaliza nome e descrição para NFC e remove os espaços das pontas")]
    public void Criar_ComFormaDecomposta_NormalizaParaNfc()
    {
        const string composto = "Avaliação";
        string decomposto = composto.Normalize(NormalizationForm.FormD);
        decomposto.Should().NotBe(composto, "pré-condição: as duas formas têm bytes diferentes");
        DefinicaoInformada definicao = DefinicoesDeTipoConceito.Numerica() with { Nome = $" {decomposto} ", Descricao = $" {decomposto} " };

        TipoConceito tipo = TipoConceito.Criar("AVALIACAO", definicao).Value!;

        (tipo.Codigo, tipo.Nome, tipo.Descricao).Should().Be(("AVALIACAO", composto, composto));
    }

    [Fact(DisplayName = "Descrição em branco vira ausência de descrição")]
    public void Criar_DescricaoEmBranco_ViraNula()
    {
        TipoConceito tipo = TipoConceito.Criar("TIPO", DefinicoesDeTipoConceito.Numerica() with { Descricao = "   " }).Value!;

        tipo.Descricao.Should().BeNull();
    }

    [Theory(DisplayName = "Recusa código ausente")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_CodigoAusente_Recusa(string? codigo) =>
        TipoConceito.Criar(codigo, DefinicoesDeTipoConceito.Numerica())
            .DeveRecusarComo(TipoConceitoErrorCodes.CodigoObrigatorio, "codigo");

    [Fact(DisplayName = "Aceita código de 80 caracteres e recusa o de 81")]
    public void Criar_CodigoNoLimiteDeTamanho()
    {
        TipoConceito.Criar(new string('A', 80), DefinicoesDeTipoConceito.Numerica()).IsSuccess.Should().BeTrue();

        TipoConceito.Criar(new string('A', 81), DefinicoesDeTipoConceito.Numerica())
            .DeveRecusarComo(TipoConceitoErrorCodes.CodigoFormatoInvalido, "codigo");
    }

    [Fact(DisplayName = "Recusa código com o caractere nulo")]
    public void Criar_CodigoCaractereInvalido_Recusa() =>
        TipoConceito.Criar(Nulo, DefinicoesDeTipoConceito.Numerica())
            .DeveRecusarComo(TipoConceitoErrorCodes.CodigoFormatoInvalido, "codigo");

    [Theory(DisplayName = "Recusa nome ausente")]
    [InlineData(null)]
    [InlineData("  ")]
    public void Criar_NomeAusente_Recusa(string? nome) =>
        TipoConceito.Criar("TIPO", DefinicoesDeTipoConceito.Numerica() with { Nome = nome })
            .DeveRecusarComo(TipoConceitoErrorCodes.NomeObrigatorio, "nome");

    [Fact(DisplayName = "Aceita nome de 200 caracteres e recusa o de 201")]
    public void Criar_NomeNoLimiteDeTamanho()
    {
        TipoConceito.Criar("TIPO", DefinicoesDeTipoConceito.Numerica() with { Nome = new string('N', 200) }).IsSuccess.Should().BeTrue();

        TipoConceito.Criar("TIPO", DefinicoesDeTipoConceito.Numerica() with { Nome = new string('N', 201) })
            .DeveRecusarComo(TipoConceitoErrorCodes.NomeTamanho, "nome");
    }

    [Fact(DisplayName = "Recusa nome com o caractere nulo")]
    public void Criar_NomeCaractereInvalido_Recusa() =>
        TipoConceito.Criar("TIPO", DefinicoesDeTipoConceito.Numerica() with { Nome = Nulo })
            .DeveRecusarComo(TipoConceitoErrorCodes.NomeCaractereInvalido, "nome");

    [Fact(DisplayName = "Aceita descrição de 1000 caracteres e recusa a de 1001")]
    public void Criar_DescricaoNoLimiteDeTamanho()
    {
        TipoConceito.Criar("TIPO", DefinicoesDeTipoConceito.Numerica() with { Descricao = new string('D', 1000) }).IsSuccess.Should().BeTrue();

        TipoConceito.Criar("TIPO", DefinicoesDeTipoConceito.Numerica() with { Descricao = new string('D', 1001) })
            .DeveRecusarComo(TipoConceitoErrorCodes.DescricaoTamanho, "descricao");
    }

    [Fact(DisplayName = "Recusa descrição com o caractere nulo")]
    public void Criar_DescricaoCaractereInvalido_Recusa() =>
        TipoConceito.Criar("TIPO", DefinicoesDeTipoConceito.Numerica() with { Descricao = Nulo })
            .DeveRecusarComo(TipoConceitoErrorCodes.DescricaoCaractereInvalido, "descricao");

    [Theory(DisplayName = "Recusa código e nome com quebra de linha, tabulação ou formatação, que a linha única não aceita")]
    [InlineData("TIPO\tA")]
    [InlineData("TIPO\nA")]
    [InlineData("TIPO\u202EA")]
    public void Criar_CodigoENomeDeUmaLinha_RecusamInvisiveis(string texto)
    {
        TipoConceito.Criar(texto, DefinicoesDeTipoConceito.Numerica())
            .DeveRecusarComo(TipoConceitoErrorCodes.CodigoFormatoInvalido, "codigo");
        TipoConceito.Criar("TIPO", DefinicoesDeTipoConceito.Numerica() with { Nome = texto })
            .DeveRecusarComo(TipoConceitoErrorCodes.NomeCaractereInvalido, "nome");
    }

    [Fact(DisplayName = "Aceita descrição de várias linhas e com tabulação, e a grava em NFC")]
    public void Criar_DescricaoDeVariasLinhas_Aceita()
    {
        Result<TipoConceito> resultado = TipoConceito.Criar("TIPO", DefinicoesDeTipoConceito.Numerica() with { Descricao = "linha 1\r\nlinha\t2 e\u0301" });

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Descricao.Should().Be("linha 1\r\nlinha\t2 é");
    }

    [Theory(DisplayName = "Recusa descrição com controle que não seja quebra de linha ou tabulação, ou com formatação")]
    [InlineData("texto\u0007")]
    [InlineData("texto\u202Einvertido")]
    public void Criar_DescricaoComControleOuFormatacao_Recusa(string descricao) =>
        TipoConceito.Criar("TIPO", DefinicoesDeTipoConceito.Numerica() with { Descricao = descricao })
            .DeveRecusarComo(TipoConceitoErrorCodes.DescricaoCaractereInvalido, "descricao");

    [Theory(DisplayName = "Recusa natureza ausente ou que não seja o código exato de uma natureza")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("numerico")]
    [InlineData("Numerico")]
    [InlineData(" NUMERICO")]
    [InlineData("ORDINAL")]
    public void Criar_NaturezaInvalida_Recusa(string? natureza) =>
        TipoConceito.Criar("TIPO", DefinicoesDeTipoConceito.Numerica() with { Natureza = natureza })
            .DeveRecusarComo(TipoConceitoErrorCodes.NaturezaInvalida, "natureza");

    [Fact(DisplayName = "Natureza inválida não vira também erro de parâmetros incompatíveis")]
    public void Criar_NaturezaInvalida_NaoAcusaIncompatibilidade()
    {
        Result<TipoConceito> resultado = TipoConceito.Criar("TIPO", DefinicoesDeTipoConceito.Numerica() with { Natureza = "x" });

        resultado.Errors.Select(e => e.Error.Code).Should().Equal(TipoConceitoErrorCodes.NaturezaInvalida);
    }

    [Fact(DisplayName = "A recusa da natureza lista os códigos aceitos sem repetir o valor recebido")]
    public void Criar_NaturezaInvalida_ListaOsCodigosSemRepetirOValor()
    {
        Result<TipoConceito> resultado = TipoConceito.Criar("TIPO", DefinicoesDeTipoConceito.Numerica() with { Natureza = "ordinal" });

        string mensagem = resultado.Errors.Single().Error.Message;
        mensagem.Should().Contain("NUMERICO").And.Contain("POR_OPCOES").And.NotContain("ordinal");
    }

    [Fact(DisplayName = "Natureza inválida acumula com as demais recusas do pedido")]
    public void Criar_NaturezaInvalidaENomeAusente_Acumula()
    {
        Result<TipoConceito> resultado = TipoConceito.Criar("TIPO", DefinicoesDeTipoConceito.Numerica() with { Nome = "", Natureza = null });

        resultado.Errors.Select(e => e.Field).Should().Equal("nome", "natureza");
    }

    [Fact(DisplayName = "Acumula as violações de código, nome e escala no mesmo lote")]
    public void Criar_VariasViolacoes_Acumula()
    {
        DefinicaoInformada definicao = DefinicoesDeTipoConceito.Numerica() with { Nome = "", Numerico = new EscalaInformada(5m, 5m, 9) };

        Result<TipoConceito> resultado = TipoConceito.Criar("", definicao);

        resultado.Errors.Select(e => e.Field).Should().Equal("codigo", "nome", "numerico", "numerico.casasDecimais");
    }
}
