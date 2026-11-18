namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ConceitoDaAvaliacao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;

/// <summary>
/// A cópia por valor do conceito com que a etapa é avaliada (ADR-0061, ADR-0169): a natureza decide os
/// parâmetros, as guardas repetem as do catálogo e as recusas se acumulam (ADR-0125).
/// </summary>
public sealed class ConceitoDaEtapaTests
{
    private static ValorCategoricoInformado[] AptoEInapto() =>
        [new("APTO", "Apto", true), new("INAPTO", "Inapto", false)];

    private static Result<ConceitoDaEtapa> Numerico(EscalaInformada? escala, IReadOnlyList<ValorCategoricoInformado?>? valores = null) =>
        ConceitoDaEtapa.Criar("NOTA", "Nota", NaturezaConceitoCodigo.Numerico, escala, valores);

    private static Result<ConceitoDaEtapa> Categorico(
        IReadOnlyList<ValorCategoricoInformado?>? valores, EscalaInformada? escala = null) =>
        ConceitoDaEtapa.Criar("APTO_INAPTO", "Apto ou inapto", NaturezaConceitoCodigo.Categorico, escala, valores);

    private static (string? Campo, string Codigo)[] Pares(Result<ConceitoDaEtapa> resultado) =>
        [.. resultado.Errors.Select(static erro => (erro.Field, erro.Error.Code))];

    [Fact(DisplayName = "O conceito numérico copia o código, o nome e a escala, e não tem valores")]
    public void Criar_Numerico_CopiaAEscala()
    {
        ConceitoDaEtapa conceito = Numerico(new(0m, 10m, 2)).Value!;

        conceito.Codigo.Should().Be("NOTA");
        conceito.Nome.Should().Be("Nota");
        conceito.Natureza.Should().Be(NaturezaConceito.Numerico);
        conceito.Escala.Should().Match<EscalaNumerica>(escala => escala.Minima == 0m && escala.Maxima == 10m && escala.CasasDecimais == 2);
        conceito.Valores.Should().BeEmpty();
    }

    [Fact(DisplayName = "O conceito categórico copia os valores na ordem do cadastro e não tem escala")]
    public void Criar_Categorico_CopiaOsValoresNaOrdem()
    {
        ConceitoDaEtapa conceito = Categorico(AptoEInapto()).Value!;

        conceito.Natureza.Should().Be(NaturezaConceito.Categorico);
        conceito.Escala.Should().BeNull();
        conceito.Valores.Select(static valor => (valor.Codigo, valor.Rotulo, valor.ContaComoAprovacao))
            .Should().Equal(("APTO", "Apto", true), ("INAPTO", "Inapto", false));
    }

    [Fact(DisplayName = "O conceito numérico sem escala é recusado como incoerente com a natureza")]
    public void Criar_NumericoSemEscala_Recusa()
    {
        Pares(Numerico(escala: null)).Should().Equal(("escala", ConceitoDaEtapaErrorCodes.NaturezaIncoerente));
    }

    [Fact(DisplayName = "O conceito numérico com valores categóricos é recusado como incoerente com a natureza")]
    public void Criar_NumericoComValores_Recusa()
    {
        Pares(Numerico(new(0m, 10m, 2), AptoEInapto())).Should().Equal(("valores", ConceitoDaEtapaErrorCodes.NaturezaIncoerente));
    }

    [Fact(DisplayName = "O conceito categórico com escala é recusado como incoerente com a natureza")]
    public void Criar_CategoricoComEscala_Recusa()
    {
        Pares(Categorico(AptoEInapto(), new EscalaInformada(0m, 10m, 2))).Should().Equal(("escala", ConceitoDaEtapaErrorCodes.NaturezaIncoerente));
    }

    [Theory(DisplayName = "O conceito categórico com menos de dois valores é recusado")]
    [InlineData(0)]
    [InlineData(1)]
    public void Criar_CategoricoComMenosDeDoisValores_Recusa(int quantidade)
    {
        Result<ConceitoDaEtapa> resultado = Categorico([.. AptoEInapto().Take(quantidade)]);

        Pares(resultado).Should().Equal(("valores", ConceitoDaEtapaErrorCodes.ValoresInsuficientes));
    }

    [Fact(DisplayName = "O conceito categórico sem lista de valores é recusado como de valores insuficientes")]
    public void Criar_CategoricoSemValores_Recusa()
    {
        Pares(Categorico(valores: null)).Should().Equal(("valores", ConceitoDaEtapaErrorCodes.ValoresInsuficientes));
    }

    [Fact(DisplayName = "O código de valor repetido é recusado na posição do repetido")]
    public void Criar_CodigoDeValorRepetido_Recusa()
    {
        Result<ConceitoDaEtapa> resultado = Categorico([new("APTO", "Apto", true), new("APTO", "Outro", false)]);

        Pares(resultado).Should().Equal(("valores[1].codigo", ConceitoDaEtapaErrorCodes.ValorRepetido));
    }

    [Fact(DisplayName = "O rótulo de valor repetido é recusado na posição do repetido")]
    public void Criar_RotuloDeValorRepetido_Recusa()
    {
        Result<ConceitoDaEtapa> resultado = Categorico([new("APTO", "Apto", true), new("CONFORME", "Apto", false)]);

        Pares(resultado).Should().Equal(("valores[1].rotulo", ConceitoDaEtapaErrorCodes.ValorRotuloRepetido));
    }

    [Fact(DisplayName = "O código repetido é recusado ainda que o rótulo do repetido seja inválido")]
    public void Criar_CodigoRepetidoComRotuloInvalido_AcumulaAsDuasRecusas()
    {
        Result<ConceitoDaEtapa> resultado = Categorico([new("APTO", "Apto", true), new("APTO", null, false)]);

        Pares(resultado).Should().Equal(
            ("valores[1].rotulo", ConceitoDaEtapaErrorCodes.ValorRotuloInvalido),
            ("valores[1].codigo", ConceitoDaEtapaErrorCodes.ValorRepetido));
    }

    [Fact(DisplayName = "O rótulo repetido é recusado ainda que o código do repetido seja inválido")]
    public void Criar_RotuloRepetidoComCodigoInvalido_AcumulaAsDuasRecusas()
    {
        Result<ConceitoDaEtapa> resultado = Categorico([new("APTO", "Apto", true), new(null, "Apto", false)]);

        Pares(resultado).Should().Equal(
            ("valores[1].codigo", ConceitoDaEtapaErrorCodes.ValorCodigoInvalido),
            ("valores[1].rotulo", ConceitoDaEtapaErrorCodes.ValorRotuloRepetido));
    }

    [Theory(DisplayName = "A escala com a mínima igual ou acima da máxima é recusada")]
    [InlineData(10, 10)]
    [InlineData(11, 10)]
    public void Criar_EscalaSemFaixa_Recusa(int minima, int maxima)
    {
        Pares(Numerico(new(minima, maxima, 2))).Should().Equal(("escala", ConceitoDaEtapaErrorCodes.EscalaInvalida));
    }

    [Theory(DisplayName = "As casas decimais de 0 a 4 são aceitas")]
    [InlineData(0)]
    [InlineData(4)]
    public void Criar_CasasNoIntervalo_Aceita(int casas)
    {
        Numerico(new(0m, 10m, casas)).IsSuccess.Should().BeTrue();
    }

    [Theory(DisplayName = "As casas decimais abaixo de 0 ou acima de 4 são recusadas")]
    [InlineData(-1)]
    [InlineData(5)]
    public void Criar_CasasForaDoIntervalo_Recusa(int casas)
    {
        Pares(Numerico(new(0m, 10m, casas))).Should().Equal(("escala.casasDecimais", ConceitoDaEtapaErrorCodes.CasasDecimaisInvalidas));
    }

    [Theory(DisplayName = "A natureza ausente ou fora dos tokens é recusada, nunca tratada como ausente")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("numérico")]
    [InlineData("NUMERICO")]
    public void Criar_NaturezaDesconhecida_Recusa(string? natureza)
    {
        Result<ConceitoDaEtapa> resultado = ConceitoDaEtapa.Criar("NOTA", "Nota", natureza, new EscalaInformada(0m, 10m, 2), null);

        Pares(resultado).Should().Equal(("natureza", ConceitoDaEtapaErrorCodes.NaturezaInvalida));
    }

    [Theory(DisplayName = "O código e o nome ausentes, em branco ou com o caractere nulo são recusados")]
    [InlineData(null)]
    [InlineData("  ")]
    [InlineData("A\0B")]
    public void Criar_CodigoENomeInvalidos_Recusa(string? texto)
    {
        Result<ConceitoDaEtapa> resultado = ConceitoDaEtapa.Criar(texto, texto, NaturezaConceitoCodigo.Numerico, new EscalaInformada(0m, 10m, 2), null);

        Pares(resultado).Should().Equal(
            ("codigo", ConceitoDaEtapaErrorCodes.CodigoInvalido), ("nome", ConceitoDaEtapaErrorCodes.NomeInvalido));
    }

    [Fact(DisplayName = "O código acima de 64 caracteres é recusado sem repetir o texto na mensagem")]
    public void Criar_CodigoLongo_RecusaSemRepetirOTexto()
    {
        string longo = new('X', 65);

        Result<ConceitoDaEtapa> resultado = ConceitoDaEtapa.Criar(longo, "Nota", NaturezaConceitoCodigo.Numerico, new EscalaInformada(0m, 10m, 2), null);

        resultado.Error!.Code.Should().Be(ConceitoDaEtapaErrorCodes.CodigoInvalido);
        resultado.Error.Message.Should().NotContain(longo);
    }

    [Fact(DisplayName = "O código e o rótulo de um valor ausentes ou longos demais são recusados na posição do valor")]
    public void Criar_ValorInvalido_RecusaNaPosicao()
    {
        Result<ConceitoDaEtapa> resultado = Categorico([new("APTO", "Apto", true), new(null, new string('R', 101), false)]);

        Pares(resultado).Should().Equal(
            ("valores[1].codigo", ConceitoDaEtapaErrorCodes.ValorCodigoInvalido),
            ("valores[1].rotulo", ConceitoDaEtapaErrorCodes.ValorRotuloInvalido));
    }

    [Fact(DisplayName = "O elemento nulo da lista de valores é recusado como valor sem código nem rótulo")]
    public void Criar_ValorNulo_Recusa()
    {
        Pares(Categorico([new("APTO", "Apto", true), null])).Should().Equal(
            ("valores[1].codigo", ConceitoDaEtapaErrorCodes.ValorCodigoInvalido),
            ("valores[1].rotulo", ConceitoDaEtapaErrorCodes.ValorRotuloInvalido));
    }

    [Fact(DisplayName = "Os textos copiados ficam aparados e em NFC")]
    public void Criar_TextoDecomposto_FicaEmNfc()
    {
        const string NomeDecomposto = " Avaliação ";
        const string RotuloDecomposto = "Não apto";

        ConceitoDaEtapa conceito = ConceitoDaEtapa.Criar(
            "AVALIACAO", NomeDecomposto, NaturezaConceitoCodigo.Categorico, null, [new("APTO", "Apto", true), new("NAO_APTO", RotuloDecomposto, false)]).Value!;

        conceito.Nome.Should().Be("Avaliação");
        conceito.Valores[1].Rotulo.Should().Be("Não apto");
    }

    [Fact(DisplayName = "As recusas de código, nome, natureza e parâmetros se acumulam no mesmo resultado")]
    public void Criar_VariasRecusas_Acumula()
    {
        Result<ConceitoDaEtapa> resultado = ConceitoDaEtapa.Criar(" ", null, NaturezaConceitoCodigo.Numerico, new EscalaInformada(5m, 5m, 9), AptoEInapto());

        Pares(resultado).Should().Equal(
            ("codigo", ConceitoDaEtapaErrorCodes.CodigoInvalido),
            ("nome", ConceitoDaEtapaErrorCodes.NomeInvalido),
            ("valores", ConceitoDaEtapaErrorCodes.NaturezaIncoerente),
            ("escala", ConceitoDaEtapaErrorCodes.EscalaInvalida),
            ("escala.casasDecimais", ConceitoDaEtapaErrorCodes.CasasDecimaisInvalidas));
    }

    [Fact(DisplayName = "Mudar a lista de origem depois da cópia não muda o conceito, e a cópia não aceita alteração")]
    public void Criar_ListaDeOrigemMutada_NaoMudaACopia()
    {
        List<ValorCategoricoInformado?> origem = [.. AptoEInapto()];
        ConceitoDaEtapa conceito = Categorico(origem).Value!;

        origem.RemoveAt(0);
        Action alterarACopia = () => ((IList<ValorCategorico>)conceito.Valores).RemoveAt(0);

        conceito.Valores.Should().HaveCount(2);
        alterarACopia.Should().Throw<NotSupportedException>();
    }

    [Fact(DisplayName = "Dois conceitos com os mesmos dados e valores na mesma ordem são iguais")]
    public void Igualdade_MesmosDados_PorValor()
    {
        ConceitoDaEtapa um = Categorico(AptoEInapto()).Value!;
        ConceitoDaEtapa outro = Categorico(AptoEInapto()).Value!;

        outro.Should().Be(um);
        outro.GetHashCode().Should().Be(um.GetHashCode());
    }

    [Fact(DisplayName = "A ordem dos valores diferencia os conceitos")]
    public void Igualdade_OrdemDiferente_Diferencia()
    {
        ConceitoDaEtapa um = Categorico(AptoEInapto()).Value!;
        ConceitoDaEtapa invertido = Categorico([.. AptoEInapto().Reverse()]).Value!;

        invertido.Should().NotBe(um);
    }

    [Fact(DisplayName = "Dois conceitos numéricos de escalas diferentes são diferentes")]
    public void Igualdade_EscalaDiferente_Diferencia()
    {
        Numerico(new(0m, 10m, 2)).Value!.Should().NotBe(Numerico(new(0m, 100m, 2)).Value!);
    }

    [Fact(DisplayName = "O valor de um código existente é devolvido e o inexistente não")]
    public void ValorDeCodigo_ExistenteEInexistente()
    {
        ConceitoDaEtapa conceito = Categorico(AptoEInapto()).Value!;

        conceito.ValorDeCodigo("INAPTO")!.Rotulo.Should().Be("Inapto");
        conceito.ValorDeCodigo("inapto").Should().BeNull();
        Numerico(new(0m, 10m, 2)).Value!.ValorDeCodigo("APTO").Should().BeNull();
    }

    [Fact(DisplayName = "Só o valor marcado conta como aprovação, e o valor que o conceito não tem não conta")]
    public void ContaComoAprovacao_SoOValorMarcado()
    {
        ConceitoDaEtapa conceito = Categorico(AptoEInapto()).Value!;

        conceito.ContaComoAprovacao("APTO").Should().BeTrue();
        conceito.ContaComoAprovacao("INAPTO").Should().BeFalse();
        conceito.ContaComoAprovacao("DESCONHECIDO").Should().BeFalse();
    }
}
