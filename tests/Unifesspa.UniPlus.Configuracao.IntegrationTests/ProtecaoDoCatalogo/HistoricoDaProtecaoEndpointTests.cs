namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ProtecaoDoCatalogo;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Application.Permissoes;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// O registro permanente dos atos da proteção de dados do catálogo, por item, pelo host real (ADR-0153): cada ato fica
/// gravado e sobrevive à edição do item, e quem mantém ou consulta o cadastro lê o histórico do item.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class HistoricoDaProtecaoEndpointTests : HistoricoDaProtecaoTestBase
{
    private const string Paulo = "paulo.conceicao";

    public HistoricoDaProtecaoEndpointTests(ConfiguracaoEndpointFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "A reprovação continua registrada depois que o administrador corrige o fato, que volta a pendente")]
    public async Task Reprovacao_ContinuaRegistradaDepoisDaCorrecao()
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await FatoReprovadoECorrigidoAsync(client);

        (await ObterFatoAsync(client, id)).GetProperty("aprovacao").GetProperty("situacao").GetString().Should().Be("PENDENTE");
        HttpResponseMessage historico = await Enviar(client, HttpMethod.Get, $"{Fatos}/{id}/protecao/atos", Isabella);
        historico.StatusCode.Should().Be(HttpStatusCode.OK);
        historico.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.uniplus.protecao-do-catalogo-ato.v1+json");
        List<JsonElement> atos = [.. (await historico.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray()];
        atos.Select(a => (a.GetProperty("ato").GetString(), a.GetProperty("atorNome").GetString()))
            .Should().Equal(("SOLICITACAO", Isabella.Nome), ("REPROVACAO", Betina.Nome));
        (atos[1].GetProperty("justificativa").GetString(), atos[1].GetProperty("finalidadeTratamento").GetString())
            .Should().Be(("A renda bruta é desnecessária para a finalidade.", Finalidade));
        atos.Should().OnlyContain(a => a.GetProperty("instante").GetDateTimeOffset() > DateTimeOffset.UtcNow.AddMinutes(-5));
        atos.SelectMany(a => a.EnumerateObject().Select(p => p.Name)).Should().NotContain(["sub", "atorSub", "emissor", "atorEmissor"]);
    }

    [Fact(DisplayName = "O detalhe do item, lido pelo Representante do controlador, traz os atos registrados mesmo depois da correção")]
    public async Task Reprovacao_DetalheTrazOsAtos()
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await FatoReprovadoECorrigidoAsync(client);

        JsonElement detalhe = await (await Enviar(client, HttpMethod.Get, $"{Base}/fatos/{id}", Betina)).Content.ReadFromJsonAsync<JsonElement>();

        detalhe.GetProperty("atos").EnumerateArray().Select(a => a.GetProperty("ato").GetString()).Should().Equal("SOLICITACAO", "REPROVACAO");
    }

    [Fact(DisplayName = "O administrador vê a solicitação, o ajuste recomendado e o aceite do tipo, em ordem")]
    public async Task Historico_DoTipo_SolicitacaoAjusteEAceite()
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await TipoComAjusteAceitoAsync(client);

        HttpResponseMessage resposta = await Enviar(client, HttpMethod.Get, $"{Tipos}/{id}/protecao/atos", Isabella);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        List<JsonElement> atos = [.. (await resposta.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray()];
        atos.Select(a => (a.GetProperty("ato").GetString(), a.GetProperty("atorNome").GetString()))
            .Should().Equal(("SOLICITACAO", Isabella.Nome), ("AJUSTE_RECOMENDADO", Caroline.Nome), ("ACEITE", Isabella.Nome));
        atos[0].GetProperty("classificacaoProtecao").GetString().Should().Be("INTERNO");
        (atos[1].GetProperty("classificacaoRecomendada").GetString(), atos[1].GetProperty("justificativa").GetString()).Should().Be(("SENSIVEL", "Traz dado de saúde."));
        atos[2].GetProperty("classificacaoProtecao").GetString().Should().Be("SENSIVEL", "o aceite grava a classificação que passou a valer");
        atos[2].GetProperty("classificacaoRecomendada").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact(DisplayName = "Aprovar a proteção, como Representante do controlador, não abre o histórico dos cadastros")]
    public async Task HistoricoDoItem_RepresentanteNaoLe()
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        (Guid tipo, Guid fato) = await TipoEFatoSemAtoAsync(client);

        await ConcessoesDeTeste.ConcederGovernancaAsync(Fixture.Factory.Services, Paulo, TipoDeGovernanca.RepresentanteDoControlador);

        (await Enviar(client, HttpMethod.Get, $"{Tipos}/{tipo}/protecao/atos", PessoaPaulo)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Enviar(client, HttpMethod.Get, $"{Fatos}/{fato}/protecao/atos", PessoaPaulo)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "Quem mantém ou consulta o cadastro lê o histórico do item, e o item que não existe responde 404")]
    public async Task HistoricoDoItem_QuemMantemOuConsultaLe()
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        (Guid tipo, Guid fato) = await TipoEFatoSemAtoAsync(client);
        await ConcessoesDeTeste.ConcederPermissoesAsync(
            Fixture.Factory.Services, Paulo, PermissaoConfiguracao.TiposDocumentoManter.Codigo, PermissaoConfiguracao.FatosCandidatoConsultar.Codigo);

        HttpResponseMessage doTipo = await Enviar(client, HttpMethod.Get, $"{Tipos}/{tipo}/protecao/atos", PessoaPaulo);

        doTipo.StatusCode.Should().Be(HttpStatusCode.OK);
        (await doTipo.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength().Should().Be(0, "o item que nunca foi solicitado não tem ato");
        (await Enviar(client, HttpMethod.Get, $"{Fatos}/{fato}/protecao/atos", PessoaPaulo)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Enviar(client, HttpMethod.Get, $"{Tipos}/{Guid.NewGuid()}/protecao/atos", Isabella)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Enviar(client, HttpMethod.Get, $"{Fatos}/{Guid.NewGuid()}/protecao/atos", Isabella)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static Persona PessoaPaulo => new(Paulo, "Paulo");

    private async Task<(Guid Tipo, Guid Fato)> TipoEFatoSemAtoAsync(HttpClient client)
    {
        await PrepararPersonasAsync();
        await ConcessoesDeTeste.RemoverDaPessoaAsync(Fixture.Factory.Services, Paulo);

        return (await CadastrarTipoAsync(client, $"{Marca()} Laudo médico complementar"), await CadastrarFatoAsync(client, $"{Marca()} Renda familiar bruta"));
    }

    // O fato solicitado por Isabella, reprovado por Betina e corrigido por Isabella depois.
    private async Task<Guid> FatoReprovadoECorrigidoAsync(HttpClient client)
    {
        await PrepararPersonasAsync();
        Guid id = await CadastrarFatoAsync(client, $"{Marca()} Renda familiar bruta");
        (await SolicitarAsync(client, Isabella, "FATO", id)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await DecidirAsync(client, Betina, new("FATO", id, "REPROVAR", "PESSOAL", Justificativa: "A renda bruta é desnecessária para a finalidade.")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        JsonElement fato = await ObterFatoAsync(client, id);
        (await Enviar(client, HttpMethod.Put, $"{Fatos}/{id}", Isabella, new { nome = fato.GetProperty("nome").GetString(), descricao = "Renda per capita da família." }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        return id;
    }

    // O tipo solicitado por Isabella, com ajuste recomendado por Caroline e aceito por Isabella.
    private async Task<Guid> TipoComAjusteAceitoAsync(HttpClient client)
    {
        await PrepararPersonasAsync();
        Guid id = await CadastrarTipoAsync(client, $"{Marca()} Laudo médico complementar");
        (await SolicitarAsync(client, Isabella, "TIPO_DOCUMENTO", id)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await DecidirAsync(client, Caroline, new("TIPO_DOCUMENTO", id, "RECOMENDAR_AJUSTE", "INTERNO", "SENSIVEL", "Traz dado de saúde.")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Enviar(client, HttpMethod.Post, $"{Tipos}/{id}/aceite-da-recomendacao", Isabella)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        return id;
    }
}
