namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;

using AwesomeAssertions;

using Xunit;

/// <summary>
/// <b>Parâmetro de consulta que a vitrine não entende é recusado, nunca ignorado.</b>
/// </summary>
/// <remarks>
/// <para>
/// O desfecho que se quer evitar não é o erro, é o <b>silêncio</b>: uma resposta 200 com a coleção
/// inteira para quem pediu um recorte. O cliente não tem como perceber — recebe uma lista
/// plausível, e conclui que aquele é o conjunto filtrado.
/// </para>
/// <para>
/// A rota é anônima, então quem a consome pode ser um integrador qualquer, sem acesso ao log do
/// servidor para desconfiar. A recusa é a única forma de ele saber.
/// </para>
/// </remarks>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class RecusaDeParametroDaVitrineTests
{
    private readonly CascadingFixture _fixture;

    public RecusaDeParametroDaVitrineTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory(DisplayName = "Situação fora do vocabulário é recusada, e não devolve a vitrine inteira")]
    [InlineData("9")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("naoExiste")]
    public async Task Situacao_ForaDoVocabulario_ERecusada(string valor)
    {
        // Quem recusa é o binding do parâmetro, não código nosso — verificado removendo qualquer
        // guarda própria e observando 400 nos quatro valores. O teste existe para TRAVAR isso: é
        // comportamento de framework do qual a rota depende, e uma mudança nele devolveria a
        // vitrine inteira a quem pediu um recorte, sem nada denunciar. O literal numérico entra na
        // amostra de propósito, porque é o caso em que se poderia supor que o enum aceita o valor.
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await client.GetAsync(
            new Uri($"/api/selecao/certames?situacao={valor}", UriKind.Relative), CancellationToken.None);

        resposta.StatusCode.Should().BeOneOf(
            [HttpStatusCode.UnprocessableEntity, HttpStatusCode.BadRequest],
            "recortar por situação inexistente não pode devolver 200 com a coleção inteira");
    }

    [Theory(DisplayName = "Situação do vocabulário é aceita")]
    [InlineData("emBreve")]
    [InlineData("inscricoesAbertas")]
    [InlineData("ultimosDias")]
    [InlineData("encerradas")]
    public async Task Situacao_DoVocabulario_EAceita(string valor)
    {
        // O outro lado da recusa: uma guarda severa demais tornaria o filtro inútil, e o teste
        // acima passaria com a rota inteiramente quebrada.
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await client.GetAsync(
            new Uri($"/api/selecao/certames?situacao={valor}", UriKind.Relative), CancellationToken.None);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "Sem o parâmetro, a vitrine inteira é servida")]
    public async Task Listar_QuandoOmiteASituacao_DeveServirAVitrineInteira()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await client.GetAsync(
            new Uri("/api/selecao/certames", UriKind.Relative), CancellationToken.None);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, "ausência de filtro é ausência do parâmetro");
    }

    [Fact(DisplayName = "Campo de ordenação fora do catálogo é recusado nomeando os aceitos")]
    public async Task Sort_ForaDoCatalogo_ERecusado()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await client.GetAsync(
            new Uri("/api/selecao/certames?sort=hashConfiguracao", UriKind.Relative), CancellationToken.None);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        string corpo = await resposta.Content.ReadAsStringAsync(CancellationToken.None);
        corpo.Should().Contain("inscricoesAte", "a recusa precisa dizer o que vale, não só que errou");
    }
}
