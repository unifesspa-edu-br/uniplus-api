namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ProtecaoDoCatalogo;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

/// <summary>
/// A decisão diante de manifestação técnica desfavorável vigente pelo host real (UNI-REQ-0374): aprovar ou recomendar
/// ajuste exige a justificativa, registrada no ato; o administrador aceita a recomendação sem nova justificativa.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class JustificativaDianteDaManifestacaoEndpointTests : ManifestacaoDaProtecaoTestBase
{
    private const string Justificativa = "A classificação declarada basta: o documento só comprova a matrícula.";

    public JustificativaDianteDaManifestacaoEndpointTests(ConfiguracaoEndpointFixture fixture)
        : base(fixture)
    {
    }

    [Theory(DisplayName = "Aprovar diante de manifestação desfavorável exige a justificativa, que o ato registra com o indicador")]
    [InlineData("TIPO_DOCUMENTO")]
    [InlineData("FATO")]
    public async Task Aprovar_DianteDeDesfavoravel(string cadastro)
    {
        await PrepararTodosAsync();
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await SolicitadoAsync(client, cadastro);
        (await ManifestarAsync(client, Kevin, cadastro, id, "INADEQUADA")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage semJustificativa = await DecidirComoAsync(client, Betina, cadastro, id, "APROVAR");
        HttpResponseMessage comJustificativa = await DecidirComoAsync(client, Betina, cadastro, id, "APROVAR", Justificativa);

        (await CodigoAsync(semJustificativa)).Should().Be("uniplus.configuracao.protecao_do_catalogo.justificativa_exigida_pela_manifestacao");
        comJustificativa.StatusCode.Should().Be(HttpStatusCode.NoContent);
        JsonElement ato = await UltimoAtoAsync(client, cadastro, id);
        (ato.GetProperty("ato").GetString(), ato.GetProperty("justificativa").GetString(), ato.GetProperty("dianteDeManifestacaoDesfavoravel").GetBoolean())
            .Should().Be(("APROVACAO", Justificativa, true));
    }

    [Theory(DisplayName = "O ajuste diante de manifestação desfavorável exige a justificativa, e o administrador aceita sem nova justificativa")]
    [InlineData("TIPO_DOCUMENTO")]
    [InlineData("FATO")]
    public async Task Ajuste_DianteDeDesfavoravel_EAceite(string cadastro)
    {
        await PrepararTodosAsync();
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await SolicitadoAsync(client, cadastro);
        (await ManifestarAsync(client, Kevin, cadastro, id, "RECOMENDA_AJUSTE")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage semJustificativa = await DecidirComoAsync(client, Betina, cadastro, id, "RECOMENDAR_AJUSTE", recomendada: "SENSIVEL");
        HttpResponseMessage comJustificativa = await DecidirComoAsync(client, Betina, cadastro, id, "RECOMENDAR_AJUSTE", Justificativa, "SENSIVEL");

        (await CodigoAsync(semJustificativa)).Should().Be("uniplus.configuracao.protecao_do_catalogo.justificativa_exigida_pela_manifestacao");
        comJustificativa.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AceitarAsync(client, cadastro, id)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        JsonElement aprovacao = (await DetalheAsync(client, cadastro, id)).GetProperty("aprovacao");
        (aprovacao.GetProperty("situacao").GetString(), aprovacao.GetProperty("ciclo").GetInt32()).Should().Be(("APROVADA", 1));
    }

    [Fact(DisplayName = "Sem manifestação, a aprovação aceita a justificativa opcional e o ato a guarda")]
    public async Task Aprovar_SemManifestacao_JustificativaOpcional()
    {
        await PrepararTodosAsync();
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid semJustificativa = await SolicitadoAsync(client, "TIPO_DOCUMENTO");
        Guid comJustificativa = await SolicitadoAsync(client, "TIPO_DOCUMENTO");

        (await DecidirComoAsync(client, Betina, "TIPO_DOCUMENTO", semJustificativa, "APROVAR")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await DecidirComoAsync(client, Betina, "TIPO_DOCUMENTO", comJustificativa, "APROVAR", Justificativa)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        JsonElement ato = await UltimoAtoAsync(client, "TIPO_DOCUMENTO", comJustificativa);
        (ato.GetProperty("justificativa").GetString(), ato.GetProperty("dianteDeManifestacaoDesfavoravel").GetBoolean()).Should().Be((Justificativa, false));
    }
}
