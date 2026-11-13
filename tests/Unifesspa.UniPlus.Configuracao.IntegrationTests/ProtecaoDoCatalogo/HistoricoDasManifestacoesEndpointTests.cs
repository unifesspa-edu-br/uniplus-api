namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ProtecaoDoCatalogo;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// O histórico das manifestações técnicas e a justificativa da aprovação, gravados e relidos de um escopo novo pelo host
/// real (ADR-0163): a substituída e a de ciclo encerrado continuam registradas, e o ato guarda a justificativa.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class HistoricoDasManifestacoesEndpointTests : ManifestacaoDaProtecaoTestBase
{
    private const string Justificativa = "A classificação interna basta depois da correção da finalidade.";

    public HistoricoDasManifestacoesEndpointTests(ConfiguracaoEndpointFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "Relidos de um escopo novo, os eventos mostram a encerrada do ciclo anterior, a substituída e a vigente")]
    public async Task Recarga_EventosDosDoisCiclos()
    {
        Guid id = await DoisCiclosAsync();

        IReadOnlyList<ManifestacaoDaProtecaoDoCatalogo> eventos = await RecarregarAsync(
            (db) => new ManifestacaoDaProtecaoRepository(db).ListarDoItemAsync(new ItemDaProtecao(CadastroDoCatalogo.TipoDocumento, id), CancellationToken.None));

        eventos.Select(static e => (e.Ciclo.Numero, e.Parecer, e.Autor.Nome)).Should().Equal(
            (1, ParecerDaManifestacao.Inadequada, Kevin.Nome),
            (2, ParecerDaManifestacao.RecomendaAjuste, Kevin.Nome),
            (2, ParecerDaManifestacao.Adequada, Kevin.Nome));
        (eventos[2].SubstituiId, eventos[1].SubstituiId).Should().Be((eventos[1].Id, (Guid?)null));
        eventos[1].Texto!.Valor.Should().Be("A classificação deve ser sensível.");
    }

    [Fact(DisplayName = "Relido de um escopo novo, o ato da aprovação guarda a justificativa, o ciclo e o indicador")]
    public async Task Recarga_JustificativaDaAprovacao()
    {
        Guid id = await DoisCiclosAsync();

        AtoDaProtecaoDoCatalogo aprovacao = await RecarregarAsync(db => db.AtosDaProtecaoDoCatalogo.AsNoTracking()
            .SingleAsync(a => a.ItemId == id && a.Ato == AtoDaProtecao.Aprovacao));

        (aprovacao.Justificativa, aprovacao.Ciclo.Numero, aprovacao.DianteDeManifestacaoDesfavoravel).Should().Be((Justificativa, 2, false));
    }

    [Fact(DisplayName = "O Encarregado de dados lê o histórico com a situação de cada evento, e a leitura é registrada pela permissão de consultar")]
    public async Task Historico_LidoPelaEncarregada()
    {
        Guid id = await DoisCiclosAsync();
        using HttpClient client = Fixture.Factory.CreateClient();
        int antes = Registro.Registrados.Count;

        List<JsonElement> historico = await HistoricoAsync(client, Lucia, "TIPO_DOCUMENTO", id);

        historico.Select(static m => (m.GetProperty("ciclo").GetInt32(), m.GetProperty("situacao").GetString())).Should().Equal(
            (1, "ENCERRADA"), (2, "SUBSTITUIDA"), (2, "VIGENTE"));
        historico.SelectMany(static m => m.EnumerateObject().Select(p => p.Name)).Should().NotContain(["autorSub", "autorEmissor", "sub", "emissor"]);
        Registro.Registrados.Skip(antes).Should().Contain(r =>
            r.AtorSubject == Lucia.Sub && r.Permissao == "configuracao:protecao-do-catalogo:consultar" && r.Permitido);
    }

    [Fact(DisplayName = "O histórico do item inexistente responde 404, e quem só mantém o cadastro recebe 403")]
    public async Task Historico_InexistenteESemPermissao()
    {
        await PrepararTodosAsync();
        using HttpClient client = Fixture.Factory.CreateClient();

        HttpResponseMessage inexistente = await Enviar(client, HttpMethod.Get, $"{Base}/fatos/{Guid.NewGuid()}/manifestacoes", Betina);
        HttpResponseMessage mantenedora = await Enviar(client, HttpMethod.Get, $"{Base}/fatos/{Guid.NewGuid()}/manifestacoes", Isabella);

        (inexistente.StatusCode, mantenedora.StatusCode).Should().Be((HttpStatusCode.NotFound, HttpStatusCode.Forbidden));
    }

    // Ciclo 1: Kevin julga inadequada e Betina reprova; Isabella corrige e solicita de novo. Ciclo 2: Kevin recomenda
    // ajuste, depois julga adequada, e Betina aprova com justificativa.
    private async Task<Guid> DoisCiclosAsync()
    {
        await PrepararTodosAsync();
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await SolicitadoAsync(client, "TIPO_DOCUMENTO");
        await PassoAsync(ManifestarAsync(client, Kevin, "TIPO_DOCUMENTO", id, "INADEQUADA"));
        await PassoAsync(DecidirComoAsync(client, Betina, "TIPO_DOCUMENTO", id, "REPROVAR", "O laudo não é necessário."));
        await PassoAsync(EditarTipoAsync(client, id));
        (await SolicitarAsync(client, Isabella, "TIPO_DOCUMENTO", id)).StatusCode.Should().Be(HttpStatusCode.OK);
        await PassoAsync(ManifestarAsync(client, Kevin, "TIPO_DOCUMENTO", id, "RECOMENDA_AJUSTE", ciclo: 2));
        await PassoAsync(ManifestarAsync(client, Kevin, "TIPO_DOCUMENTO", id, "ADEQUADA", texto: null, ciclo: 2));
        await PassoAsync(DecidirComoAsync(client, Betina, "TIPO_DOCUMENTO", id, "APROVAR", Justificativa));
        return id;
    }

    private static async Task PassoAsync(Task<HttpResponseMessage> passo)
    {
        using HttpResponseMessage resposta = await passo;
        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent, await resposta.Content.ReadAsStringAsync());
    }

    private static async Task<HttpResponseMessage> EditarTipoAsync(HttpClient client, Guid id)
    {
        JsonElement detalhe = await DetalheAsync(client, "TIPO_DOCUMENTO", id);
        return await Enviar(client, HttpMethod.Put, $"{Tipos}/{id}", Isabella, new
        {
            id,
            codigo = detalhe.GetProperty("codigo").GetString(),
            nome = $"{Marca()} Laudo médico",
            categoria = "SAUDE",
            classificacaoProtecao = "INTERNO",
            finalidadeTratamento = Finalidade,
            hipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL",
        });
    }

    private async Task<T> RecarregarAsync<T>(Func<ConfiguracaoDbContext, Task<T>> leitura)
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();
        return await leitura(escopo.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>());
    }

    private RegistroDeDecisoesDeTeste Registro => Fixture.Factory.Services.GetRequiredService<RegistroDeDecisoesDeTeste>();
}
