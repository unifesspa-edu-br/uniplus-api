namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.FatosCandidato;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// Contrato HTTP da manutenção do catálogo de fatos pela permissão de manter
/// (UNI-REQ-0143, ADR-0136): cadastro de fato declarado, edição de nome e descrição e desativação.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class FatoCandidatoAdminEndpointTests(ConfiguracaoEndpointFixture fixture) : FatoCandidatoAdminEndpointTestBase(fixture)
{
    [Fact(DisplayName = "Cadastro, edição, desativação e reativação do fato declarado; a lista filtra por ativo")]
    public async Task CicloDoFatoDeclarado()
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        string codigo = CodigoUnico();

        HttpResponseMessage criar = await EnviarAsync(client, HttpMethod.Post, Base, FatoBooleano(codigo));
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        using (JsonDocument fato = await ObterAsync(client, id))
        {
            fato.RootElement.GetProperty("origem").GetString().Should().Be("DECLARADO");
            fato.RootElement.GetProperty("binding").GetString().Should().Be($"CAMPO_FORMULARIO:{codigo}");
            fato.RootElement.GetProperty("semeadoPeloSistema").GetBoolean().Should().BeFalse();
        }

        (await EnviarAsync(client, HttpMethod.Put, $"{Base}/{id}", new { nome = "Nome editado", descricao = "Descrição editada.", componenteExibicao = "MOEDA" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await EnviarAsync(client, HttpMethod.Delete, $"{Base}/{id}", corpo: null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage inativos = await EnviarAsync(client, HttpMethod.Get, $"{Base}?ativo=false&origem=DECLARADO", corpo: null);
        inativos.StatusCode.Should().Be(HttpStatusCode.OK);
        using (JsonDocument lista = JsonDocument.Parse(await inativos.Content.ReadAsStringAsync()))
        {
            lista.RootElement.EnumerateArray().Should().Contain(item => item.GetProperty("id").GetGuid() == id
                && item.GetProperty("nome").GetString() == "Nome editado");
            lista.RootElement.EnumerateArray().Should().OnlyContain(item => !item.GetProperty("ativo").GetBoolean()
                && item.GetProperty("origem").GetString() == "DECLARADO");
            lista.RootElement.EnumerateArray().Should().NotContain(item => item.GetProperty("codigo").GetString() == "COR_RACA");
        }

        (await EnviarAsync(client, HttpMethod.Post, $"{Base}/{id}/ativacao", corpo: null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact(DisplayName = "Cadastro sem descrição é recusado com o código da descrição obrigatória")]
    public async Task Criar_SemDescricao_Retorna422()
    {
        using HttpClient client = Fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Post, Base, new
        {
            codigo = CodigoUnico(),
            nome = "Vínculo com outra instituição pública",
            dominio = "BOOLEANO",
            cardinalidade = "ESCALAR",
            pontoResolucao = "HABILITACAO",
            escopo = "CANDIDATO",
            classificacaoProtecao = "PESSOAL",
            finalidadeTratamento = "Verificação dos requisitos de matrícula do processo seletivo.",
            hipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL",
        });

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("errors").EnumerateArray().Should().ContainSingle()
            .Which.GetProperty("code").GetString().Should().Be("uniplus.configuracao.fato_candidato.descricao_obrigatoria");
    }

    [Fact(DisplayName = "Componente fora do vocabulário é recusado com o código do componente de exibição")]
    public async Task Atualizar_ComponenteForaDoVocabulario_Retorna422()
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await (await EnviarAsync(client, HttpMethod.Post, Base, FatoBooleano(CodigoUnico()))).Content.ReadFromJsonAsync<Guid>();

        HttpResponseMessage resposta = await EnviarAsync(
            client, HttpMethod.Put, $"{Base}/{id}", new { nome = "Nome editado", descricao = "Descrição editada.", componenteExibicao = "REAIS" });

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("errors").EnumerateArray().Should().ContainSingle()
            .Which.GetProperty("code").GetString().Should().Be("uniplus.configuracao.fato_candidato.componente_exibicao_invalido");
    }

    [Fact(DisplayName = "A semente declara a renda per capita como moeda, e o fato de sistema aceita editar a apresentação")]
    public async Task FatoDeSistema_ApresentacaoEditavel()
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = Guid.Parse("fa700000-0000-7000-8000-000000000005");

        string descricaoDaSemente;
        string situacaoAntes;
        using (JsonDocument antes = await ObterAsync(client, id))
        {
            antes.RootElement.GetProperty("componenteExibicao").GetString().Should().Be("MOEDA");
            descricaoDaSemente = antes.RootElement.GetProperty("descricao").GetString()!;
            descricaoDaSemente.Should().NotBeNullOrWhiteSpace();
            situacaoAntes = antes.RootElement.GetProperty("aprovacao").GetProperty("situacao").GetString()!;
        }

        (await EnviarAsync(client, HttpMethod.Put, $"{Base}/{id}", new { nome = "Renda familiar per capita", descricao = "Descrição editada.", componenteExibicao = "SITUACAO" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        try
        {
            using JsonDocument depois = await ObterAsync(client, id);
            depois.RootElement.GetProperty("componenteExibicao").GetString().Should().Be("SITUACAO");
            depois.RootElement.GetProperty("descricao").GetString().Should().Be("Descrição editada.");
            depois.RootElement.GetProperty("aprovacao").GetProperty("situacao").GetString().Should().Be(situacaoAntes);
        }
        finally
        {
            (await EnviarAsync(client, HttpMethod.Put, $"{Base}/{id}", new { nome = "Renda familiar per capita", descricao = descricaoDaSemente, componenteExibicao = "MOEDA" }))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
    }

    [Fact(DisplayName = "Origem desconhecida no filtro da lista devolve página vazia, não erro")]
    public async Task Listar_OrigemDesconhecida_PaginaVazia()
    {
        using HttpClient client = Fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Get, $"{Base}?origem=declarado", corpo: null);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument lista = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        lista.RootElement.GetArrayLength().Should().Be(0);
    }

    [Fact(DisplayName = "Código já usado, inclusive por fato de sistema, é recusado com conflito")]
    public async Task Criar_CodigoExistente_Retorna409()
    {
        using HttpClient client = Fixture.Factory.CreateClient();

        (await EnviarAsync(client, HttpMethod.Post, Base, FatoBooleano("COR_RACA")))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact(DisplayName = "Fato de sistema não é desativado pela manutenção")]
    public async Task Desativar_FatoDeSistema_Retorna422()
    {
        using HttpClient client = Fixture.Factory.CreateClient();

        (await EnviarAsync(client, HttpMethod.Delete, $"{Base}/{IdDeCorRaca}", corpo: null))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
