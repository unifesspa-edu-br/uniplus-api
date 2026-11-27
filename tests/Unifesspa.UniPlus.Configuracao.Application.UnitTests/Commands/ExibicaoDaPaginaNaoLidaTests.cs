namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands.RecusasParaTeste;

/// <summary>
/// A exibição enviada conta para a regra de que só a seção a tem, mesmo malformada: a página que a leitura recusa por
/// causa dela recusa também a exibição fora de seção.
/// </summary>
public sealed class ExibicaoDaPaginaNaoLidaTests
{
    private static readonly IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>> CondicaoMalformada = [[]];

    [Fact(DisplayName = "O bloco de sistema com a exibição malformada recusa a condição e a exibição fora de seção")]
    public void Ler_BlocoDeSistemaComExibicaoMalformada_RecusaAsDuas()
    {
        PaginaDoFormularioInput pagina = EntradaDaPaginaDoFormularioDeTeste.BlocoDeSistema("REVISAO", 1, "REVISAO_E_ACEITE").ComTitulo("Revisão")
            .ExibidaQuando(CondicaoMalformada).Construir();

        DaLeitura(new ConteudoDoModeloInput(null, [pagina], null, null, null)).Should().Equal(
            ("paginas[0].exibicao[0]", "ClausulaDnf.ClausulaVazia"),
            ("paginas[0].exibicao", EstruturaFormularioErrorCodes.ExibicaoForaDeSecao));
    }

    [Fact(DisplayName = "A seção com a exibição malformada recusa só a condição")]
    public void Ler_SecaoComExibicaoMalformada_RecusaSoACondicao()
    {
        PaginaDoFormularioInput pagina = EntradaDaPaginaDoFormularioDeTeste.Secao("DADOS").ComTitulo("Dados").ExibidaQuando(CondicaoMalformada).Construir();

        DaLeitura(new ConteudoDoModeloInput(null, [pagina], null, null, null)).Should().Equal(("paginas[0].exibicao[0]", "ClausulaDnf.ClausulaVazia"));
    }

    [Fact(DisplayName = "O bloco de sistema sem exibição e com bloco inválido não recusa exibição fora de seção")]
    public void Ler_BlocoInvalidoSemExibicao_NaoRecusaExibicaoForaDeSecao()
    {
        PaginaDoFormularioInput pagina = EntradaDaPaginaDoFormularioDeTeste.BlocoDeSistema("REVISAO", 1, "BLOCO_INEXISTENTE").ComTitulo("Revisão").Construir();

        DaLeitura(new ConteudoDoModeloInput(null, [pagina], null, null, null)).Select(static r => r.Campo).Should().Equal("paginas[0].bloco");
    }
}
