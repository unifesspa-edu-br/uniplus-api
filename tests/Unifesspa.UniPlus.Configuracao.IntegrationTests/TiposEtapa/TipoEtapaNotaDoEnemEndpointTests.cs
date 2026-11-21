namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposEtapa;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Npgsql;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposEtapa.ApoioDoEndpointDeTiposEtapa;

/// <summary>A proteção do tipo semeado da nota do ENEM: a origem da nota não muda pela API, a pontuação não sai e o tipo não é desativado.</summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class TipoEtapaNotaDoEnemEndpointTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public TipoEtapaNotaDoEnemEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "PUT não muda a nota de origem no ENEM e recusa com 422 tirar a pontuação do tipo NOTA_ENEM")]
    public async Task Atualizar_TipoDoEnem_MantemAOrigemERecusaSemPontuacao()
    {
        await using TipoDoEnemRestauradoNoFim restauracao = await TipoDoEnemRestauradoNoFim.GuardarAsync(_fixture.ConnectionString);
        using HttpClient client = _fixture.Factory.CreateClient();
        (Guid id, string nome, string? descricao) = await TipoDoEnem(client);
        (string, object?)[] corpoDoTipo = [("id", id), ("nome", nome), ("descricao", descricao)];

        HttpResponseMessage semAtributo = await EnviarPutAdmin(client, id, Corpo([.. corpoDoTipo, ("notaDeOrigemNoEnem", false)]));

        semAtributo.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await ConferirOrigemDoEnemMantida(client, id);
        await ConferirRecusaSemPontuacao(await EnviarPutAdmin(client, id, Corpo([.. corpoDoTipo, ("admitePontuacao", false)])));
    }

    private static async Task ConferirOrigemDoEnemMantida(HttpClient client, Guid id)
    {
        using JsonDocument item = await ObterTipo(client, id);
        item.RootElement.GetProperty("avaliacao").GetProperty("notaDeOrigemNoEnem").GetBoolean().Should().BeTrue(
            "o PUT não carrega a origem da nota, e o campo enviado é ignorado");
    }

    private static async Task ConferirRecusaSemPontuacao(HttpResponseMessage semPontuacao)
    {
        semPontuacao.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await semPontuacao.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("code").GetString()
            .Should().Be("uniplus.configuracao.tipo_etapa.nota_de_origem_no_enem_exige_pontuacao");
        problema.RootElement.GetProperty("errors")[0].GetProperty("field").GetString().Should().Be("avaliacao.admitePontuacao");
    }

    [Fact(DisplayName = "DELETE recusa com 422 desativar o tipo NOTA_ENEM")]
    public async Task Desativar_TipoDoEnem_Recusa()
    {
        await using TipoDoEnemRestauradoNoFim restauracao = await TipoDoEnemRestauradoNoFim.GuardarAsync(_fixture.ConnectionString);
        using HttpClient client = _fixture.Factory.CreateClient();
        (Guid id, _, _) = await TipoDoEnem(client);

        HttpResponseMessage desativar = await EnviarDeleteAdmin(client, id);

        desativar.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await desativar.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("code").GetString()
            .Should().Be("uniplus.configuracao.tipo_etapa.nota_de_origem_no_enem_nao_desativa");
        (await client.GetAsync(new Uri($"/api/configuracao/tipos-etapa/{id}", UriKind.Relative)))
            .StatusCode.Should().Be(HttpStatusCode.OK, "o tipo continua ativo");
    }

    [Fact(DisplayName = "A restauração do tipo NOTA_ENEM desfaz o que o teste alterou na linha semeada")]
    public async Task RestauracaoDoTipoDoEnem_DesfazAAlteracao()
    {
        await using (await TipoDoEnemRestauradoNoFim.GuardarAsync(_fixture.ConnectionString))
        {
            await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
            await conexao.OpenAsync();
            await using NpgsqlCommand desativar = new(
                "UPDATE configuracao.tipos_etapa SET ativo = false, nome = 'Alterado' WHERE codigo = 'NOTA_ENEM'", conexao);
            await desativar.ExecuteNonQueryAsync();
        }

        using HttpClient client = _fixture.Factory.CreateDefaultClient();
        (_, string nome, _) = await TipoDoEnem(client);
        nome.Should().NotBe("Alterado", "a linha volta ativa e com o nome semeado");
    }

    /// <summary>O tipo semeado, com os campos que um PUT precisa repetir para não alterá-lo.</summary>
    private static async Task<(Guid Id, string Nome, string? Descricao)> TipoDoEnem(HttpClient client)
    {
        HttpResponseMessage listar = await client.GetAsync(new Uri("/api/configuracao/tipos-etapa", UriKind.Relative));
        using JsonDocument lista = JsonDocument.Parse(await listar.Content.ReadAsStringAsync());
        JsonElement tipo = lista.RootElement.EnumerateArray()
            .Single(item => item.GetProperty("codigo").GetString() == "NOTA_ENEM");
        string? descricao = tipo.TryGetProperty("descricao", out JsonElement valor) ? valor.GetString() : null;
        return (tipo.GetProperty("id").GetGuid(), tipo.GetProperty("nome").GetString()!, descricao);
    }
}
