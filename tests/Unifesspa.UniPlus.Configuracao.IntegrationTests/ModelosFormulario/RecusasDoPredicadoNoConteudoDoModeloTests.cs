namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

public sealed class RecusasDoPredicadoNoConteudoDoModeloTests
{
    [Fact(DisplayName = "As recusas do predicado informam os códigos na ordem sem expor o conteúdo")]
    public void Desserializar_DuasCondicoesInvalidas_InformaOsCodigosNaOrdem()
    {
        const string documento = """{"titulo":"Modelo","paginas":[{"codigo":"DADOS","ordem":0,"tipo":"SECAO","titulo":"Dados","exibicao":[[{"codigoDoDado":"DADO_A","operador":"INEXISTENTE","valor":"valor-gravado-ficticio-7f3a"},{"codigoDoDado":"DADO_B","operador":"IGUAL","valor":[]}]]}],"itens":[],"termos":[],"pressupostos":[],"grupos":[]}""";
        Action leitura = () => ConteudoDoModeloJson.Desserializar(documento);

        string mensagem = leitura.Should().Throw<InvalidOperationException>().Which.Message;
        mensagem.Should().Be("Dado inválido no banco ao reidratar ConteudoDoModelo: CondicaoDnf.OperadorInvalido, CondicaoDnf.FormaIncoerenteComOperador. Verifique se houve alteração manual da coluna fora do fluxo da aplicação.");
        mensagem.Should().NotContain("valor-gravado-ficticio-7f3a").And.NotContain("operadores");
    }
}
