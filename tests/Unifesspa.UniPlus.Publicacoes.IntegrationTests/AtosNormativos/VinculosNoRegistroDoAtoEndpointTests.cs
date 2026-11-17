namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.AtosNormativos;

using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static AtoNormativoApoio;
using static AtosDaEntidadeApoio;

/// <summary>
/// As recusas dos vínculos no registro do ato pelo endpoint administrativo (ADR-0105): a entidade repetida, o
/// elemento nulo, o rótulo fora da grafia canônica e o vínculo com o processo seletivo, que só o domínio dono do
/// objeto registra.
/// </summary>
[Collection(PublicacoesEndpointCollection.Name)]
public sealed class VinculosNoRegistroDoAtoEndpointTests
{
    private const string VinculoExigeRegistroPeloDominio = "uniplus.publicacoes.ato_normativo.vinculo_exige_registro_pelo_dominio";

    private readonly PublicacoesEndpointFixture _fixture;
    private readonly AtosDaEntidadeApoio _apoio;

    public VinculosNoRegistroDoAtoEndpointTests(PublicacoesEndpointFixture fixture)
    {
        _fixture = fixture;
        _apoio = new AtosDaEntidadeApoio(_fixture);
    }

    [Fact(DisplayName = "A mesma entidade vinculada duas vezes ao mesmo ato é recusada com 422")]
    public async Task Registrar_VinculoDuplicadoNoPayload_Retorna422()
    {
        string tipo = await _apoio.CriarTipoAsync(unicoPorObjeto: false);
        Guid processo = Guid.CreateVersion7();
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await PostAtoAsync(
            client, PayloadAto(tipo, "13", "2026-03-13", [Vinculo(processo), Vinculo(processo)]));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    /// <remarks>A anotação de nulidade do record não impede um <c>"vinculos": [null]</c> no corpo.</remarks>
    [Fact(DisplayName = "Elemento nulo na lista de vínculos é recusado com 422 — nunca 500")]
    public async Task Registrar_VinculoNulo_Retorna422()
    {
        string tipo = await _apoio.CriarTipoAsync(unicoPorObjeto: false);
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await PostAtoAsync(client, PayloadComVinculoNulo(tipo));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "Rótulo de entidade fora da grafia canônica é recusado com 422")]
    public async Task Registrar_RotuloForaDaGrafia_Retorna422()
    {
        string tipo = await _apoio.CriarTipoAsync(unicoPorObjeto: false);
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await PostAtoAsync(
            client,
            PayloadAto(tipo, "13", "2026-03-13", [Vinculo(Guid.CreateVersion7(), "processo-seletivo")]));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "O endpoint administrativo recusa ato vinculado a processo seletivo, mesmo entre outros vínculos")]
    public async Task Registrar_VinculoProcessoSeletivo_Retorna422NomeadoESemRegistrar()
    {
        string tipo = await _apoio.CriarTipoAsync(unicoPorObjeto: false);
        Guid processo = Guid.CreateVersion7();
        using HttpClient client = _fixture.Factory.CreateClient();
        object[] vinculos = [Vinculo(Guid.CreateVersion7(), ObjetoA), Vinculo(processo, EntidadesDeRegistroPeloDominio.ProcessoSeletivo)];

        HttpResponseMessage resposta = await PostAtoAsync(client, PayloadAto(tipo, "13", "2026-03-13", vinculos));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain(VinculoExigeRegistroPeloDominio);
        (await ListarDaEntidadeAsync(client, EntidadesDeRegistroPeloDominio.ProcessoSeletivo, processo)).Should().BeEmpty();
    }

    [Fact(DisplayName = "A retificação pelo endpoint de ato de processo seletivo é recusada, mesmo sem repetir o vínculo")]
    public async Task Registrar_RetificacaoHerdaProcessoSeletivo_Retorna422()
    {
        string tipo = await _apoio.CriarTipoAsync(unicoPorObjeto: false);
        Guid processo = Guid.CreateVersion7();
        Guid raiz = await _apoio.RegistrarPeloDominioAsync(tipo, "13", processo);
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await PostAtoAsync(
            client, PayloadRetificacao(tipo, "13", "2026-03-14", raiz, "corrige o anexo", []));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain(VinculoExigeRegistroPeloDominio);
        (await ListarIdsDaEntidadeAsync(client, EntidadesDeRegistroPeloDominio.ProcessoSeletivo, processo)).Should().Equal(raiz);
    }

    private static object PayloadComVinculoNulo(string tipoCodigo) =>
        new
        {
            orgao = "CEPS",
            serie = "EDITAL",
            ano = 2026,
            numero = "13",
            tipoCodigo,
            dataDoAto = "2026-03-13",
            documentoHash = HashValido,
            assinante = PersonasDeTeste.NomeKevinCarvalho,
            vinculos = new object?[] { null },
        };
}
