namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

public sealed class RecusasDoPredicadoNoConteudoDoModeloTests
{
    [Fact]
    public void Desserializar_DuasCondicoesInvalidas_CitaSomenteAPrimeiraCausa()
    {
        const string documento = """{"titulo":"Modelo","paginas":[{"codigo":"DADOS","ordem":0,"tipo":"SECAO","titulo":"Dados","exibicao":[[{"codigoDoDado":"DADO_A","operador":"ALGUM_DE","valor":true},{"codigoDoDado":"DADO_B","operador":"IGUAL","valor":[]}]]}],"itens":[],"termos":[],"pressupostos":[],"grupos":[]}""";
        Action leitura = () => ConteudoDoModeloJson.Desserializar(documento);

        leitura.Should().Throw<InvalidOperationException>().WithMessage(
            "O conteúdo gravado do modelo de formulário não remonta: Os operadores ALGUM_DE e NENHUM_DE exigem um array JSON, sem itens de texto em branco (array vazio é aceito).");
    }
}
