namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Errors;

using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands.CenarioDoHandlerDoModelo;
using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands.RecusasParaTeste;

/// <summary>
/// O item que a leitura recusa passa pela forma do item e pelas regras do campo que não dependem do que foi recusado,
/// na posição que veio, e as recusas saem juntas.
/// </summary>
public sealed class FormaDoItemNaoLidoComRegrasTests
{
    private static readonly string AjudaLonga = new('a', 1001);

    [Fact(DisplayName = "A obrigatoriedade inválida e a ajuda acima de 1000 caracteres saem juntas")]
    public void Ler_ObrigatoriedadeInvalidaEAjudaLonga_RecusaAsDuas()
    {
        FatoColetadoInput item = Montar("CERTIFICADO", 0).ComObrigatoriedade("INEXISTENTE").ComAjuda(AjudaLonga).Construir();

        DaLeitura(Conteudo(item)).Should().Equal(
            ("itens[0].regras.obrigatoriedade", ModeloFormularioErrorCodes.ObrigatoriedadeInvalida),
            ("itens[0].apresentacao.ajuda", ItemFormularioErrorCodes.AjudaTamanho));
    }

    [Fact(DisplayName = "O campo de município com restrição malformada não recebe a recusa de município sem UF")]
    public void Ler_MunicipioComRestricaoMalformada_NaoRecusaMunicipioSemUf()
    {
        FatoColetadoInput item = Montar("CERTIFICADO", 0, "MUNICIPIO").ComRestricoes(new RestricaoValorInput("DESCONHECIDA")).Construir();

        DaLeitura(Conteudo(item)).Should().Equal(("itens[0].regras.restricoes[0]", RestricaoValorErrorCodes.TipoDesconhecido));
    }

    [Fact(DisplayName = "O campo de município sem restrição e com obrigatoriedade inválida recusa o município sem UF")]
    public void Ler_MunicipioSemRestricaoEObrigatoriedadeInvalida_RecusaMunicipioSemUf()
    {
        FatoColetadoInput item = Montar("CERTIFICADO", 0, "MUNICIPIO").ComObrigatoriedade("INEXISTENTE").Construir();

        DaLeitura(Conteudo(item)).Select(static r => r.Codigo).Should().Equal(
            ModeloFormularioErrorCodes.ObrigatoriedadeInvalida, ItemFormularioErrorCodes.MunicipioSemUf);
    }

    [Fact(DisplayName = "A exibição que cita o próprio fato e a obrigatoriedade inválida saem juntas")]
    public void Ler_ExibicaoQueCitaOProprioFatoEObrigatoriedadeInvalida_RecusaAsDuas()
    {
        FatoColetadoInput item = Montar("CERTIFICADO", 0).ExibidoQuando(Quando("CERTIFICADO", true)).ComObrigatoriedade("INEXISTENTE").Construir();

        DaLeitura(Conteudo(item)).Should().Equal(
            ("itens[0].regras.obrigatoriedade", ModeloFormularioErrorCodes.ObrigatoriedadeInvalida),
            ("itens[0].regras.exibicao", ItemFormularioErrorCodes.RegraAutorreferente));
    }

    [Fact(DisplayName = "A faixa numérica em campo de texto e a obrigatoriedade inválida saem juntas")]
    public void Ler_FaixaNumericaEmCampoDeTextoEObrigatoriedadeInvalida_RecusaARestricaoIncoerente()
    {
        FatoColetadoInput item = Montar("CERTIFICADO", 0, "TEXTO").ComObrigatoriedade("INEXISTENTE")
            .ComRestricoes(new RestricaoValorInput("FAIXA_NUMERICA", 1, 5)).Construir();

        DaLeitura(Conteudo(item)).Select(static r => r.Codigo).Should().Contain(ItemFormularioErrorCodes.RestricaoIncoerente);
    }

    [Fact(DisplayName = "O tipo de campo inválido não confere a restrição pelo tipo")]
    public void Ler_TipoInvalidoComFaixaNumerica_NaoRecusaARestricaoIncoerente()
    {
        FatoColetadoInput item = Montar("CERTIFICADO", 0, "texto").ComRestricoes(new RestricaoValorInput("FAIXA_NUMERICA", 1, 5)).Construir();

        DaLeitura(Conteudo(item)).Select(static r => r.Campo).Should().Equal("itens[0].apresentacao.tipoDeCampo");
    }

    [Fact(DisplayName = "A recusa do décimo primeiro item aponta a posição dez do pedido")]
    public void Ler_DecimoPrimeiroItemNaoLidoComAjudaLonga_RecusaNaPosicaoDez()
    {
        FatoColetadoInput[] itens = [.. Enumerable.Range(0, 10).Select(static ordem => Item("CERTIFICADO", ordem)),
            Montar("CERTIFICADO", 10).ComObrigatoriedade("INEXISTENTE").ComAjuda(AjudaLonga).Construir()];

        DaLeitura(Conteudo(itens)).Select(static r => r.Campo).Should().Equal("itens[10].regras.obrigatoriedade", "itens[10].apresentacao.ajuda");
    }
}
