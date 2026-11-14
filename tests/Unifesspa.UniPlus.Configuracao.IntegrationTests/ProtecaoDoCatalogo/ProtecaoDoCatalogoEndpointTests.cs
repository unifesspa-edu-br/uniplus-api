namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ProtecaoDoCatalogo;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Application.Permissoes;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
/// <summary>
/// O circuito de aprovação da proteção de dados do catálogo pelo host real (ADR-0153): o item nasce pendente, o
/// administrador envia a solicitação, o Representante do controlador recomenda o ajuste com a classificação ou reprova,
/// e o administrador aceita.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class ProtecaoDoCatalogoEndpointTests : ProtecaoDoCatalogoTestBase
{
    public ProtecaoDoCatalogoEndpointTests(ConfiguracaoEndpointFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "O tipo cadastrado fica pendente, e a solicitação o leva à aprovação")]
    public async Task Solicitacao_LevaOTipoAAprovacao()
    {
        await TornarCarolineRepresentanteAsync();
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await CadastrarTipoAsync(client, "INTERNO");
        (await SituacaoAsync(client, id)).Should().Be("PENDENTE");

        HttpResponseMessage solicitada = await SolicitarAsync(client, "TIPO_DOCUMENTO", id);

        solicitada.StatusCode.Should().Be(HttpStatusCode.OK);
        (await solicitada.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("tiposDeDocumento").GetInt32().Should().Be(1);
        (await SituacaoAsync(client, id)).Should().Be("SOLICITADA");
    }

    [Fact(DisplayName = "O ajuste recomenda a classificação, e o aceite aprova o tipo como sensível e o tira da fila")]
    public async Task AjusteEAceite_AprovamOTipoComoSensivel()
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await TipoSolicitadoAsync(client);

        (await DecidirAsync(client, "TIPO_DOCUMENTO", id, "RECOMENDAR_AJUSTE", "SENSIVEL")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        JsonElement ajustado = (await ObterDoCatalogoAsync(client, id)).GetProperty("aprovacao");
        (ajustado.GetProperty("situacao").GetString(), ajustado.GetProperty("classificacaoRecomendada").GetString()).Should().Be(("AJUSTE_RECOMENDADO", "SENSIVEL"));
        (await EnviarAsync(client, HttpMethod.Post, $"{Tipos}/{id}/aceite-da-recomendacao")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        JsonElement aprovado = await (await client.GetAsync(new Uri($"/api/configuracao/tipos-documento/{id}", UriKind.Relative))).Content.ReadFromJsonAsync<JsonElement>();
        (aprovado.GetProperty("classificacaoProtecao").GetString(), aprovado.GetProperty("protecaoAprovada").GetBoolean()).Should().Be(("SENSIVEL", true));
        (await ListarAsync(client)).Should().NotContain(item => item.GetProperty("id").GetGuid() == id, "o item aprovado sai da lista");
    }

    [Fact(DisplayName = "Token que não identifica o solicitante responde 401 na fila do catálogo, e não 403")]
    public async Task Listar_TokenSemJti_Responde401()
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.SemJtiHeader, "1");

        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Get, Base);

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact(DisplayName = "A reprovação exige a justificativa, e a longa demais é recusada")]
    public async Task Reprovacao_ExigeJustificativa()
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await TipoSolicitadoAsync(client);

        HttpResponseMessage semMotivo = await DecidirAsync(client, "TIPO_DOCUMENTO", id, "REPROVAR");
        HttpResponseMessage longa = await DecidirAsync(client, "TIPO_DOCUMENTO", id, "REPROVAR", justificativa: new string('a', 2001));

        semMotivo.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await semMotivo.Content.ReadAsStringAsync()).Should().Contain("justificativa_obrigatoria");
        longa.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await longa.Content.ReadAsStringAsync()).Should().Contain("justificativa_muito_longa");
    }

    [Fact(DisplayName = "A reprovação aparece na fila e no detalhe, e o aceite do item reprovado é recusado")]
    public async Task Reprovacao_NaFilaENoDetalhe_SemAceite()
    {
        const string Justificativa = "O laudo não é necessário para a finalidade.";
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await TipoSolicitadoAsync(client);

        (await DecidirAsync(client, "TIPO_DOCUMENTO", id, "REPROVAR", justificativa: Justificativa)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        JsonElement reprovado = (await ObterDoCatalogoAsync(client, id)).GetProperty("aprovacao");
        (reprovado.GetProperty("situacao").GetString(), reprovado.GetProperty("justificativa").GetString()).Should().Be(("REPROVADA", Justificativa));
        JsonElement detalhe = await (await EnviarAsync(client, HttpMethod.Get, $"{Base}/tipos-documento/{id}")).Content.ReadFromJsonAsync<JsonElement>();
        detalhe.GetProperty("aprovacao").GetProperty("justificativa").GetString().Should().Be(Justificativa);
        HttpResponseMessage aceite = await EnviarAsync(client, HttpMethod.Post, $"{Tipos}/{id}/aceite-da-recomendacao");
        aceite.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await aceite.Content.ReadAsStringAsync()).Should().Contain("aceite_de_item_reprovado");
    }

    [Fact(DisplayName = "A solicitação envia só os itens escolhidos; o outro pendente continua pendente")]
    public async Task Solicitar_EnviaSoOsEscolhidos()
    {
        await TornarCarolineRepresentanteAsync();
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid escolhido = await CadastrarTipoAsync(client, "INTERNO");
        Guid outro = await CadastrarTipoAsync(client, "INTERNO");

        HttpResponseMessage resposta = await SolicitarAsync(client, ("TIPO_DOCUMENTO", escolhido), ("TIPO_DOCUMENTO", escolhido));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        (corpo.GetProperty("tiposDeDocumento").GetInt32(), corpo.GetProperty("fatos").GetInt32()).Should().Be((1, 0));
        (await SituacaoAsync(client, escolhido)).Should().Be("SOLICITADA");
        (await SituacaoAsync(client, outro)).Should().Be("PENDENTE");
    }

    [Fact(DisplayName = "A lista vazia e a ausente são recusadas com o código da solicitação sem item")]
    public async Task Solicitar_ListaVaziaOuAusente_Recusa()
    {
        using HttpClient client = Fixture.Factory.CreateClient();

        HttpResponseMessage vazia = await SolicitarAsync(client);
        HttpResponseMessage ausente = await EnviarAsync(client, HttpMethod.Post, $"{Base}/solicitacoes", new { });

        vazia.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await vazia.Content.ReadAsStringAsync()).Should().Contain("solicitacao_sem_item");
        ausente.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ausente.Content.ReadAsStringAsync()).Should().Contain("solicitacao_sem_item");
    }

    [Fact(DisplayName = "O item já solicitado, o inexistente e o nulo são recusados no campo do item")]
    public async Task Solicitar_ItemNaoPendente_Recusa()
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await TipoSolicitadoAsync(client);

        HttpResponseMessage jaSolicitado = await SolicitarAsync(client, "TIPO_DOCUMENTO", id);
        HttpResponseMessage inexistente = await SolicitarAsync(client, "FATO", Guid.NewGuid());
        HttpResponseMessage nulo = await EnviarAsync(client, HttpMethod.Post, $"{Base}/solicitacoes", new { itens = new object?[] { null } });

        new[] { jaSolicitado, inexistente, nulo }.Should().OnlyContain(static r => r.StatusCode == HttpStatusCode.UnprocessableEntity);
        (await jaSolicitado.Content.ReadAsStringAsync()).Should().Contain("item_nao_pendente").And.Contain("itens[0].id");
        (await inexistente.Content.ReadAsStringAsync()).Should().Contain("item_nao_pendente");
        (await nulo.Content.ReadAsStringAsync()).Should().Contain("itens[0]");
    }

    [Fact(DisplayName = "Item de cadastro que quem envia não mantém responde 403 e o item continua pendente")]
    public async Task Solicitar_ItemDeCadastroSemPermissao_Retorna403()
    {
        await TornarCarolineRepresentanteAsync();
        using HttpClient client = Fixture.Factory.CreateClient();
        JsonElement quilombola = await ObterFatoAsync(client, IdDeQuilombola);
        (await EnviarAsync(client, HttpMethod.Put, $"{Fatos}/{IdDeQuilombola}/definicao", DefinicaoDe(quilombola, hipoteseLegal: "EXERCICIO_REGULAR_DE_DIREITOS")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        await TornarPauloMantenedorAsync(PermissaoConfiguracao.TiposDocumentoManter.Codigo);

        HttpResponseMessage resposta = await EnviarComoPauloAsync(
            client, HttpMethod.Post, $"{Base}/solicitacoes", new { itens = new[] { new { cadastro = "FATO", id = IdDeQuilombola } } });

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ObterFatoAsync(client, IdDeQuilombola)).GetProperty("aprovacao").GetProperty("situacao").GetString().Should().Be("PENDENTE");
        (await EnviarAsync(client, HttpMethod.Put, $"{Fatos}/{IdDeQuilombola}/definicao", DefinicaoDe(quilombola))).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact(DisplayName = "Quem mantém os tipos e os fatos, mas não é Representante do controlador, recebe 403 ao decidir e o item continua solicitado")]
    public async Task Decidir_SemPermissaoDeAprovar_Retorna403()
    {
        await TornarPauloMantenedorAsync(PermissaoConfiguracao.TiposDocumentoManter.Codigo, PermissaoConfiguracao.FatosCandidatoManter.Codigo);
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await TipoSolicitadoAsync(client);

        HttpResponseMessage resposta = await EnviarComoPauloAsync(
            client, HttpMethod.Post, Base, CorpoDaDecisao(await ObterDoCatalogoAsync(client, id), "TIPO_DOCUMENTO", "APROVAR"));

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SituacaoAsync(client, id)).Should().Be("SOLICITADA");
    }

    [Fact(DisplayName = "A decisão sobre a proteção que o item deixou de ter responde 409 e não aprova a vigente")]
    public async Task Decidir_ProtecaoConferidaDiverge_Retorna409()
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await TipoSolicitadoAsync(client);

        HttpResponseMessage resposta = await DecidirAsync(client, "TIPO_DOCUMENTO", id, "APROVAR", protecaoDesatualizada: true);

        resposta.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("protecao_mudou").And.NotContain("conflito_de_concorrencia");
        (await SituacaoAsync(client, id)).Should().Be("SOLICITADA");
    }

    [Fact(DisplayName = "A decisão sobre o item que não está numa solicitação é recusada")]
    public async Task Decidir_ItemPendente_Recusa()
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await CadastrarTipoAsync(client, "PESSOAL");

        HttpResponseMessage resposta = await DecidirAsync(client, "TIPO_DOCUMENTO", id, "APROVAR");

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("item_fora_de_solicitacao");
    }

    [Fact(DisplayName = "A decisão sobre o tipo que uma edição concorrente alterou responde 409 e não aprova por cima dela")]
    public async Task Decidir_TipoAlteradoConcorrentemente_Retorna409()
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await TipoSolicitadoAsync(client);

        HttpResponseMessage resposta = await DecidirDuranteEdicaoConcorrenteAsync(client, id);

        resposta.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("conflito_de_concorrencia");
        (await SituacaoAsync(client, id)).Should().Be("SOLICITADA");
    }

    // A edição concorrente segura a linha do tipo sem commitar; a decisão lê o xmin antigo e bloqueia ao gravar, e o
    // commit torna o xmin que ela leu obsoleto.
    private async Task<HttpResponseMessage> DecidirDuranteEdicaoConcorrenteAsync(HttpClient client, Guid id)
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext concorrente = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        await using IDbContextTransaction transacao = await concorrente.Database.BeginTransactionAsync();
        await concorrente.Database.ExecuteSqlInterpolatedAsync($"UPDATE configuracao.tipo_documento SET updated_at = now() WHERE id = {id}");
        int pid = await ConcorrenciaTestHelpers.GetConnectionPidAsync(concorrente);

        Task<HttpResponseMessage> decisao = DecidirAsync(client, "TIPO_DOCUMENTO", id, "APROVAR");
        await ConcorrenciaTestHelpers.WaitForBlockedBackendAsync(Fixture.Factory, decisao, [pid]);
        await transacao.CommitAsync();
        return await decisao;
    }

    private async Task<Guid> TipoSolicitadoAsync(HttpClient client)
    {
        await TornarCarolineRepresentanteAsync();
        Guid id = await CadastrarTipoAsync(client, "INTERNO");
        (await SolicitarAsync(client, "TIPO_DOCUMENTO", id)).StatusCode.Should().Be(HttpStatusCode.OK);
        return id;
    }
}
