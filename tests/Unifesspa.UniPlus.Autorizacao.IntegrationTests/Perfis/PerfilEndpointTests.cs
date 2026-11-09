namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;

/// <summary>Cadastro de perfis de acesso pelo endpoint real, contra Postgres efêmero: criação, leitura, listagem, desativação e perfis de governança. As personas são as do portal: Diogo Souza opera o cadastro.</summary>
[Collection(AutorizacaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class PerfilEndpointTests : PerfilEndpointTestBase
{
    public PerfilEndpointTests(AutorizacaoEndpointFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "Criar perfil devolve 201 com a URL de envio e o perfil nasce sem composição em vigor")]
    public async Task Criar_Valido_NasceComVersaoPendente()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();

        using HttpResponseMessage criada = await EnviarAsync(
            diogo, HttpMethod.Post, Rota, Corpo(Nome(), [ConsultarCatalogo, ManterMotivos]));
        criada.StatusCode.Should().Be(HttpStatusCode.Created, await criada.Content.ReadAsStringAsync());
        using JsonDocument envio = JsonDocument.Parse(await criada.Content.ReadAsStringAsync());
        envio.RootElement.GetProperty("situacao").GetString().Should().Be("aguardando_envio");
        envio.RootElement.GetProperty("tipoDeMidia").GetString().Should().Be("application/pdf");
        envio.RootElement.GetProperty("urlDeEnvio").GetString().Should().StartWith("http");

        using JsonDocument perfil = await LerAsync(diogo, envio.RootElement.GetProperty("perfilId").GetGuid());
        perfil.RootElement.GetProperty("ativo").GetBoolean().Should().BeTrue();
        perfil.RootElement.GetProperty("governanca").ValueKind.Should().Be(JsonValueKind.Null);
        perfil.RootElement.GetProperty("composicaoEmVigor").ValueKind.Should().Be(JsonValueKind.Null);
        perfil.RootElement.GetProperty("sensibilidade").GetString().Should().Be("interno");
        JsonElement pendente = perfil.RootElement.GetProperty("versaoPendente");
        pendente.GetProperty("numero").GetInt32().Should().Be(1);
        pendente.GetProperty("situacaoDoDocumento").GetString().Should().Be("aguardando_envio");
        pendente.GetProperty("permissoes").EnumerateArray()
            .Select(p => p.GetProperty("codigo").GetString())
            .Should().Equal(ConsultarCatalogo, ManterMotivos);
    }

    [Fact(DisplayName = "Repetir a criação com a mesma Idempotency-Key reproduz a resposta e não cria outro perfil")]
    public async Task Criar_MesmaChave_ReproduzARespostaSemDuplicar()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        string chave = Guid.NewGuid().ToString();
        object corpo = Corpo(Nome(), [ConsultarCatalogo]);

        using HttpResponseMessage primeira = await EnviarAsync(diogo, HttpMethod.Post, Rota, corpo, chave);
        using HttpResponseMessage repeticao = await EnviarAsync(diogo, HttpMethod.Post, Rota, corpo, chave);

        primeira.StatusCode.Should().Be(HttpStatusCode.Created);
        repeticao.StatusCode.Should().Be(HttpStatusCode.Created);
        repeticao.Headers.Contains("Idempotency-Replayed").Should().BeTrue();
        (await repeticao.Content.ReadAsStringAsync()).Should().Be(await primeira.Content.ReadAsStringAsync());
    }

    [Fact(DisplayName = "Código fora do catálogo devolve 422 com o código de erro e lista o que não existe")]
    public async Task Criar_PermissaoInexistente_Devolve422()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();

        using HttpResponseMessage resposta = await EnviarAsync(
            diogo, HttpMethod.Post, Rota, Corpo(Nome(), [ConsultarCatalogo, "modulo:que:nao-existe"]));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        string corpo = await resposta.Content.ReadAsStringAsync();
        corpo.Should().Contain("uniplus.autorizacao.perfil.permissao_inexistente").And.Contain("modulo:que:nao-existe");
    }

    [Fact(DisplayName = "Perfil comum com permissão reservada à governança devolve 422")]
    public async Task Criar_PermissaoReservada_Devolve422()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();

        using HttpResponseMessage resposta = await EnviarAsync(
            diogo, HttpMethod.Post, Rota, Corpo(Nome(), [PermissaoManter]));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.perfil.permissao_reservada");
    }

    [Fact(DisplayName = "Protocolo ausente devolve 422 com o código do protocolo")]
    public async Task Criar_SemProtocolo_Devolve422()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();

        using HttpResponseMessage resposta = await EnviarAsync(
            diogo, HttpMethod.Post, Rota, new { nome = Nome(), descricao = Descricao, permissoes = new[] { ConsultarCatalogo }, protocolo = " ", tamanhoDoDocumento = 1024 });

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.perfil.protocolo_invalido");
    }

    [Fact(DisplayName = "Nome repetido, mesmo com outra caixa, devolve 409")]
    public async Task Criar_NomeRepetidoComOutraCaixa_Devolve409()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        string nome = Nome();

        using HttpResponseMessage primeira = await EnviarAsync(diogo, HttpMethod.Post, Rota, Corpo(nome, [ConsultarCatalogo]));
        primeira.StatusCode.Should().Be(HttpStatusCode.Created);

        using HttpResponseMessage segunda = await EnviarAsync(
            diogo, HttpMethod.Post, Rota, Corpo(nome.ToUpperInvariant(), [ConsultarCatalogo]));

        segunda.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await segunda.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.perfil.nome_duplicado");
    }

    [Fact(DisplayName = "Perfil que mistura permissão de unidade e institucional devolve 422")]
    public async Task Criar_AlcanceMisto_Devolve422()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();

        using HttpResponseMessage resposta = await EnviarAsync(
            diogo, HttpMethod.Post, Rota, Corpo(Nome(), [OperarInscricoes, ManterMotivos]));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.perfil.alcance_misto");
    }

    [Fact(DisplayName = "Não há rota para editar o perfil: a mudança é uma versão nova")]
    public async Task Atualizar_NaoHaRota()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        Guid id = await SemearPerfilVigenteAsync(ConsultarCatalogo);

        using HttpResponseMessage resposta = await EnviarAsync(diogo, HttpMethod.Put, $"{Rota}/{id}", Corpo(Nome(), [ConsultarCatalogo]));

        resposta.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Fact(DisplayName = "Perfil de governança recusa versão e desativação com 422")]
    public async Task Governanca_RecusaVersaoEDesativacao()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        Guid id = Guid.Parse(GestorDeAcessosId);

        using HttpResponseMessage versao = await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/versoes", Corpo(Nome(), [ConsultarCatalogo]));
        using HttpResponseMessage desativacao = await EnviarAsync(diogo, HttpMethod.Delete, $"{Rota}/{id}/ativacao");

        versao.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        desativacao.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await desativacao.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.perfil.governanca_imutavel");
        using JsonDocument perfil = await LerAsync(diogo, id);
        perfil.RootElement.GetProperty("governanca").GetString().Should().Be("gestor-de-acessos");
        perfil.RootElement.GetProperty("composicaoEmVigor").GetProperty("versaoId").ValueKind.Should().Be(JsonValueKind.Null);
        perfil.RootElement.GetProperty("composicaoEmVigor").GetProperty("permissoes").GetArrayLength().Should().BePositive();
    }

    [Fact(DisplayName = "Desativar e reativar alternam o estado do perfil")]
    public async Task DesativarEReativar_AlternaOEstado()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        Guid id = await CriarAsync(diogo);

        using HttpResponseMessage desativada = await EnviarAsync(diogo, HttpMethod.Delete, $"{Rota}/{id}/ativacao");
        desativada.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await LerAsync(diogo, id)).RootElement.GetProperty("ativo").GetBoolean().Should().BeFalse();

        using HttpResponseMessage reativada = await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/ativacao");
        reativada.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await LerAsync(diogo, id)).RootElement.GetProperty("ativo").GetBoolean().Should().BeTrue();
    }

    [Fact(DisplayName = "A listagem resume o perfil com a presença de versão vigente e pendente")]
    public async Task Listar_ResumeAsVersoes()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        Guid comVigente = await SemearPerfilVigenteAsync(ConsultarCatalogo);
        Guid semVigente = await CriarAsync(diogo);

        JsonElement[] itens = await ListarTudoAsync(diogo);

        JsonElement vigente = itens.Single(i => i.GetProperty("id").GetGuid() == comVigente);
        vigente.GetProperty("temVersaoVigente").GetBoolean().Should().BeTrue();
        vigente.GetProperty("temVersaoPendente").GetBoolean().Should().BeFalse();
        JsonElement pendente = itens.Single(i => i.GetProperty("id").GetGuid() == semVigente);
        pendente.GetProperty("temVersaoVigente").GetBoolean().Should().BeFalse();
        pendente.GetProperty("temVersaoPendente").GetBoolean().Should().BeTrue();
    }

    [Fact(DisplayName = "Perfil inexistente devolve 404 com o código de erro")]
    public async Task Obter_Inexistente_Devolve404()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();

        using HttpResponseMessage resposta = await diogo.GetAsync(new Uri($"{Rota}/{Guid.NewGuid()}", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.perfil.nao_encontrado");
    }
}
