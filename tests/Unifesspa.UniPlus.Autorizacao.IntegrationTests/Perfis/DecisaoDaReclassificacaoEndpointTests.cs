namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Perfis;

using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Reclassificacoes;
using Unifesspa.UniPlus.Autorizacao.Application.Monitoramento;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

using static Unifesspa.UniPlus.Autorizacao.IntegrationTests.Perfis.CenarioDaReclassificacao;

/// <summary>A fila, o detalhe e a decisão da reclassificação pelo Representante do controlador.</summary>
[Collection(AutorizacaoEndpointCollection.Name)]
public sealed class DecisaoDaReclassificacaoEndpointTests : PerfilEndpointTestBase, IAsyncLifetime
{
    private readonly CenarioDaReclassificacao _cenario;

    public DecisaoDaReclassificacaoEndpointTests(AutorizacaoEndpointFixture fixture)
        : base(fixture)
    {
        _cenario = new CenarioDaReclassificacao(Fixture);
    }

    public Task InitializeAsync() => _cenario.ConciliarAsync();

    public Task DisposeAsync() => _cenario.ConciliarAsync();

    [Fact(DisplayName = "O Representante do controlador libera a subida de classe, a permissão volta à pessoa e a concessão liberada sai da reclassificação")]
    public async Task Liberacao_DevolveAPermissaoERecusaARepeticao()
    {
        (Guid perfil, string pessoa, Guid reclassificacao) = await ReclassificacaoComRepresentanteAsync();
        await LerODocumentoDaVersaoAsync(perfil, Diogo);
        using HttpClient representante = Cliente(Diogo);

        using HttpResponseMessage primeira = await EnviarAsync(
            representante, HttpMethod.Post, $"/api/autorizacao/reclassificacoes/{reclassificacao}/decisoes", new { etapa = "protecao_de_dados", resultado = "liberada", todas = true });
        using HttpResponseMessage segunda = await LiberarAConcessaoAsync(representante, reclassificacao, await _cenario.ConcessaoDoPerfilAsync(perfil));

        primeira.StatusCode.Should().Be(HttpStatusCode.OK, await primeira.Content.ReadAsStringAsync());
        (await _cenario.PermissoesDaPessoaAsync(pessoa)).Should().Contain(PermissaoReclassificada);
        segunda.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await segunda.Content.ReadAsStringAsync()).Should().Contain("concessao_fora_da_reclassificacao");
    }

    [Fact(DisplayName = "A fila da proteção de dados lista a reclassificação, o detalhe traz a concessão suspensa e quem não tem a permissão da etapa recebe 403")]
    public async Task Fila_EDetalhe_ListamAReclassificacao()
    {
        (_, _, Guid reclassificacao) = await ReclassificacaoComRepresentanteAsync();
        using HttpClient representante = Cliente(Diogo);
        using HttpClient semPermissao = Cliente($"pessoa.{Guid.NewGuid():N}");

        using HttpResponseMessage fila = await representante.GetAsync(new Uri("/api/autorizacao/reclassificacoes?etapa=protecao_de_dados", UriKind.Relative));
        using HttpResponseMessage detalhe = await representante.GetAsync(new Uri($"/api/autorizacao/reclassificacoes/{reclassificacao}", UriKind.Relative));
        using HttpResponseMessage negada = await semPermissao.GetAsync(new Uri("/api/autorizacao/reclassificacoes?etapa=protecao_de_dados", UriKind.Relative));

        fila.StatusCode.Should().Be(HttpStatusCode.OK);
        (await fila.Content.ReadAsStringAsync()).Should().Contain(reclassificacao.ToString());
        detalhe.StatusCode.Should().Be(HttpStatusCode.OK, await detalhe.Content.ReadAsStringAsync());
        (await detalhe.Content.ReadAsStringAsync()).Should().Contain("protecao_de_dados");
        negada.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "A fila com etapa inválida é recusada, e o detalhe da reclassificação inexistente responde 404")]
    public async Task Fila_EtapaInvalidaEDetalheInexistente()
    {
        await ReclassificacaoComRepresentanteAsync();
        using HttpClient representante = Cliente(Diogo);

        using HttpResponseMessage invalida = await representante.GetAsync(new Uri("/api/autorizacao/reclassificacoes?etapa=outra", UriKind.Relative));
        using HttpResponseMessage inexistente = await representante.GetAsync(new Uri($"/api/autorizacao/reclassificacoes/{Guid.NewGuid()}", UriKind.Relative));

        invalida.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        inexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "A decisão com resultado, etapa ou alvo inválidos é recusada antes de qualquer gravação")]
    public async Task Decisao_ComPedidoInvalido_ERecusada()
    {
        (_, _, Guid reclassificacao) = await ReclassificacaoComRepresentanteAsync();
        using HttpClient representante = Cliente(Diogo);
        string rota = $"/api/autorizacao/reclassificacoes/{reclassificacao}/decisoes";

        using HttpResponseMessage etapa = await EnviarAsync(representante, HttpMethod.Post, rota, new { etapa = "x", resultado = "liberada", todas = true });
        using HttpResponseMessage resultado = await EnviarAsync(representante, HttpMethod.Post, rota, new { etapa = "protecao_de_dados", resultado = "x", todas = true });
        using HttpResponseMessage alvo = await EnviarAsync(representante, HttpMethod.Post, rota, new { etapa = "protecao_de_dados", resultado = "liberada", todas = false });
        using HttpResponseMessage mantidaSemMotivo = await EnviarAsync(representante, HttpMethod.Post, rota, new { etapa = "protecao_de_dados", resultado = "mantida", todas = true });
        using HttpResponseMessage inexistente = await EnviarAsync(
            representante, HttpMethod.Post, $"/api/autorizacao/reclassificacoes/{Guid.NewGuid()}/decisoes", new { etapa = "protecao_de_dados", resultado = "liberada", todas = true });

        new[] { etapa, resultado, alvo, mantidaSemMotivo }.Select(static r => r.StatusCode).Should().OnlyContain(static c => c == HttpStatusCode.UnprocessableEntity);
        inexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // A reclassificação do perfil concedido, depois da conciliação, com Diogo designado Representante do controlador.
    private async Task<(Guid Perfil, string Pessoa, Guid Reclassificacao)> ReclassificacaoComRepresentanteAsync()
    {
        (Guid perfil, string pessoa) = await _cenario.PerfilConcedidoComCatalogoAnteriorAsync(ClassificacaoDeProtecao.Publico, deUnidade: false);
        await _cenario.ConciliarAsync();
        Guid reclassificacao = await _cenario.ReclassificacaoDoPerfilAsync(perfil);
        await ConcessoesDeTeste.ConcederGovernancaAsync(Fixture.Factory.Services, Diogo, TipoDeGovernanca.RepresentanteDoControlador);

        return (perfil, pessoa, reclassificacao);
    }

    private Task LerODocumentoDaVersaoAsync(Guid perfil, string leitor) =>
        ExecutarAsync(
            """
            INSERT INTO autorizacao.acessos_ao_documento_da_versao (id, documento_id, leitor_emissor, leitor_conta_id, instante)
            SELECT {0}, d.id, {1}, {2}, now() FROM autorizacao.documentos_da_versao d WHERE d.perfil_id = {3}
            """,
            Guid.CreateVersion7(), TestAuthHandler.DefaultIssuer, leitor, perfil);

    private static Task<HttpResponseMessage> LiberarAConcessaoAsync(HttpClient representante, Guid reclassificacao, Guid concessao) =>
        EnviarAsync(
            representante, HttpMethod.Post, $"/api/autorizacao/reclassificacoes/{reclassificacao}/decisoes",
            new { etapa = "protecao_de_dados", resultado = "liberada", concessoes = new[] { concessao }, todas = false });
}
