namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ImportacaoDoResultado;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;

/// <summary>O leiaute que cada relatório declarado cita existe no catálogo, com a versão declarada.</summary>
public sealed class ConferenciaDosLeiautesDeclaradosTests
{
    private static readonly LeiauteDoRelatorio[] Catalogo = [LeiauteDeTeste.Criar("CHAMADA_REGULAR"), LeiauteDeTeste.Criar("LISTA_DE_ESPERA")];

    [Fact(DisplayName = "Os relatórios com leiaute do catálogo são aceitos")]
    public void LeiautesDoCatalogo_Aceitos() =>
        ConferenciaDosLeiautesDeclarados.RecusasDosLeiautes(LeiauteDeTeste.Declarar(null, "CHAMADA_REGULAR", "LISTA_DE_ESPERA"), Catalogo).Should().BeEmpty();

    [Fact(DisplayName = "O leiaute que o catálogo não tem é recusado no relatório pelo índice, e a versão também conta")]
    public void LeiauteDesconhecido_RecusaPeloIndice()
    {
        DeclaracaoDaImportacaoDoResultado declaracao = DeclaracaoDaImportacaoDoResultado.Declarar(
            [new("CHAMADA_REGULAR", "v1"), new("NAO_EXISTE", "v1"), new("LISTA_DE_ESPERA", "v9")], null).Value!;

        ConferenciaDosLeiautesDeclarados.RecusasDosLeiautes(declaracao, Catalogo).Select(static recusa => (recusa.Field, recusa.Error.Code)).Should().Equal(
            ("importacao.relatorios[1]", ImportacaoDoResultadoErrorCodes.LeiauteDesconhecido),
            ("importacao.relatorios[2]", ImportacaoDoResultadoErrorCodes.LeiauteDesconhecido));
    }
}
