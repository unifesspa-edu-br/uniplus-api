namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// Cadastro de perfis de acesso pelo endpoint real, contra Postgres efêmero. Cobre o que só a
/// borda responde: a permissão exigida em toda operação, o contrato de erro e a proteção dos
/// perfis de governança. As personas são as do portal: Diogo Souza opera o cadastro; Isabella
/// autenticada sem a concessão é recusada.
/// </summary>
[Collection(AutorizacaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class PerfilEndpointTests
{
    private const string Rota = "/api/autorizacao/perfis";
    private const string ConsultarCatalogo = "autorizacao:permissoes:consultar-catalogo";
    private const string ManterMotivos = "selecao:motivos-decisao-isencao:manter";
    private const string OperarInscricoes = "teste:inscricoes:operar";
    private const string AnalisarAtendimento = "teste:atendimento:analisar";
    private const string GestorDeAcessosId = "019a0000-0000-7000-8000-000000000001";

    private const string PermissaoManter = "autorizacao:perfis:manter";
    private const string Diogo = "diogo.souza";

    private readonly AutorizacaoEndpointFixture _fixture;

    public PerfilEndpointTests(AutorizacaoEndpointFixture fixture) => _fixture = fixture;

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

    [Fact(DisplayName = "Perfil em vigor aceita uma versão pendente e mantém a composição em vigor até a verificação")]
    public async Task CriarVersao_PerfilVigente_MantemAVigente()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        Guid id = await SemearPerfilVigenteAsync(ConsultarCatalogo);

        using HttpResponseMessage resposta = await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/versoes", Corpo(Nome(), [ConsultarCatalogo, ManterMotivos]));

        resposta.StatusCode.Should().Be(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync());
        using JsonDocument perfil = await LerAsync(diogo, id);
        perfil.RootElement.GetProperty("composicaoEmVigor").GetProperty("numero").GetInt32().Should().Be(1);
        perfil.RootElement.GetProperty("composicaoEmVigor").GetProperty("permissoes").GetArrayLength().Should().Be(1);
        perfil.RootElement.GetProperty("versaoPendente").GetProperty("numero").GetInt32().Should().Be(2);
    }

    [Fact(DisplayName = "Com versão pendente, outra versão é recusada com 422 até a pendente ser cancelada")]
    public async Task CriarVersao_ComPendente_Devolve422()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        Guid id = await SemearPerfilVigenteAsync(ConsultarCatalogo);
        using HttpResponseMessage primeira = await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/versoes", Corpo(Nome(), [ConsultarCatalogo]));
        Guid versaoId = LerEnvio(await primeira.Content.ReadAsStringAsync()).VersaoId;

        using HttpResponseMessage segunda = await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/versoes", Corpo(Nome(), [ConsultarCatalogo]));
        using HttpResponseMessage cancelada = await EnviarAsync(diogo, HttpMethod.Delete, $"{Rota}/{id}/versoes/{versaoId}");
        using HttpResponseMessage terceira = await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/versoes", Corpo(Nome(), [ConsultarCatalogo]));

        segunda.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await segunda.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.perfil.versao_pendente_existente");
        cancelada.StatusCode.Should().Be(HttpStatusCode.NoContent);
        terceira.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact(DisplayName = "Cancelar versão que não está pendente ou que não existe devolve 422 e 404")]
    public async Task CancelarVersao_NaoPendenteOuInexistente_Recusa()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        Guid id = await SemearPerfilVigenteAsync(ConsultarCatalogo);
        Guid vigente = await VersaoVigenteAsync(id);

        using HttpResponseMessage naoPendente = await EnviarAsync(diogo, HttpMethod.Delete, $"{Rota}/{id}/versoes/{vigente}");
        using HttpResponseMessage inexistente = await EnviarAsync(diogo, HttpMethod.Delete, $"{Rota}/{id}/versoes/{Guid.NewGuid()}");

        naoPendente.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await naoPendente.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.perfil.versao_nao_pendente");
        inexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await inexistente.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.perfil.versao_nao_encontrada");
    }

    [Fact(DisplayName = "Perfil sem concessões aceita versão que amplia a sensibilidade e muda o alcance")]
    public async Task CriarVersao_SemConcessoes_AceitaAmpliarEMudarOAlcance()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        Guid id = await SemearPerfilVigenteAsync(OperarInscricoes);

        using HttpResponseMessage ampliada = await EnviarAsync(
            diogo, HttpMethod.Post, $"{Rota}/{id}/versoes", Corpo(Nome(), [OperarInscricoes, AnalisarAtendimento]));
        Guid pendente = LerEnvio(await ampliada.Content.ReadAsStringAsync()).VersaoId;
        await EnviarAsync(diogo, HttpMethod.Delete, $"{Rota}/{id}/versoes/{pendente}");
        using HttpResponseMessage outroAlcance = await EnviarAsync(
            diogo, HttpMethod.Post, $"{Rota}/{id}/versoes", Corpo(Nome(), [ManterMotivos]));

        ampliada.StatusCode.Should().Be(HttpStatusCode.Created, await ampliada.Content.ReadAsStringAsync());
        outroAlcance.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "Perfil com concessão recusa versão que acrescenta permissão sensível")]
    public async Task CriarVersao_ComConcessao_AmpliarASensibilidade_Recusa()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        Guid id = await SemearPerfilVigenteAsync(OperarInscricoes);
        await SemearConcessaoAsync(id, EscopoDaConcessao.DaUnidade(Guid.NewGuid()));

        using HttpResponseMessage resposta = await EnviarAsync(
            diogo, HttpMethod.Post, $"{Rota}/{id}/versoes", Corpo(Nome(), [OperarInscricoes, AnalisarAtendimento]));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.perfil.ampliacao_exige_protecao_de_dados");
        (await LerAsync(diogo, id)).RootElement.GetProperty("versaoPendente").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact(DisplayName = "Perfil já sensível, com concessão, recusa acrescentar outra permissão sensível")]
    public async Task CriarVersao_ComConcessao_OutraSensivelNoPerfilJaSensivel_Recusa()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        Guid id = await SemearPerfilVigenteAsync(AnalisarAtendimento);
        await SemearConcessaoAsync(id, EscopoDaConcessao.DaUnidade(Guid.NewGuid()));

        using HttpResponseMessage resposta = await EnviarAsync(
            diogo, HttpMethod.Post, $"{Rota}/{id}/versoes", Corpo(Nome(), [AnalisarAtendimento, "teste:laudos:consultar"]));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.perfil.ampliacao_exige_protecao_de_dados");
    }

    [Fact(DisplayName = "Perfil com concessão recusa versão que muda de alcance")]
    public async Task CriarVersao_ComConcessao_MudarOAlcance_Recusa()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        Guid id = await SemearPerfilVigenteAsync(OperarInscricoes);
        await SemearConcessaoAsync(id, EscopoDaConcessao.DaUnidade(Guid.NewGuid()));

        using HttpResponseMessage resposta = await EnviarAsync(
            diogo, HttpMethod.Post, $"{Rota}/{id}/versoes", Corpo(Nome(), [ManterMotivos]));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.perfil.alcance_alterado");
    }

    [Fact(DisplayName = "Perfil com concessão aceita versão que renomeia e mantém a sensibilidade")]
    public async Task CriarVersao_ComConcessao_SemAmpliar_Aceita()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        Guid id = await SemearPerfilVigenteAsync(OperarInscricoes);
        await SemearConcessaoAsync(id, EscopoDaConcessao.DaUnidade(Guid.NewGuid()));

        using HttpResponseMessage resposta = await EnviarAsync(
            diogo, HttpMethod.Post, $"{Rota}/{id}/versoes", Corpo(Nome(), [OperarInscricoes]));

        resposta.StatusCode.Should().Be(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync());
    }

    [Fact(DisplayName = "Perfil desativado recusa versão nova, e a desativação preserva a pendente")]
    public async Task CriarVersao_PerfilInativo_Devolve422()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        Guid id = await SemearPerfilVigenteAsync(ConsultarCatalogo);
        await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/versoes", Corpo(Nome(), [ConsultarCatalogo]));
        await EnviarAsync(diogo, HttpMethod.Delete, $"{Rota}/{id}/ativacao");

        using HttpResponseMessage resposta = await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/versoes", Corpo(Nome(), [ConsultarCatalogo]));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await LerAsync(diogo, id)).RootElement.GetProperty("versaoPendente").ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact(DisplayName = "A URL de envio se reemite para o mesmo documento e a confirmação o põe em verificação")]
    public async Task Documento_ReemiteEConfirma()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        using HttpResponseMessage criada = await EnviarAsync(diogo, HttpMethod.Post, Rota, Corpo(Nome(), [ConsultarCatalogo]));
        Envio envio = LerEnvio(await criada.Content.ReadAsStringAsync());
        string rota = $"{Rota}/{envio.PerfilId}/versoes/{envio.VersaoId}/documento";

        using HttpResponseMessage reemitida = await EnviarAsync(diogo, HttpMethod.Post, $"{rota}/envio");
        using HttpResponseMessage confirmada = await EnviarAsync(diogo, HttpMethod.Post, $"{rota}/confirmacao", new { etag = "\"etag-de-teste\"" });
        using HttpResponseMessage reemitidaDepois = await EnviarAsync(diogo, HttpMethod.Post, $"{rota}/envio");

        reemitida.StatusCode.Should().Be(HttpStatusCode.Created);
        LerEnvio(await reemitida.Content.ReadAsStringAsync()).DocumentoId.Should().Be(envio.DocumentoId);
        confirmada.StatusCode.Should().Be(HttpStatusCode.OK, await confirmada.Content.ReadAsStringAsync());
        (await confirmada.Content.ReadAsStringAsync()).Should().Contain("em_verificacao");
        reemitidaDepois.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await reemitidaDepois.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.perfil.documento_nao_aguarda_envio");
    }

    [Fact(DisplayName = "Confirmar sem ETag devolve 422 e o documento segue aguardando o envio")]
    public async Task Documento_ConfirmarSemEtag_Devolve422()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        using HttpResponseMessage criada = await EnviarAsync(diogo, HttpMethod.Post, Rota, Corpo(Nome(), [ConsultarCatalogo]));
        Envio envio = LerEnvio(await criada.Content.ReadAsStringAsync());

        using HttpResponseMessage resposta = await EnviarAsync(
            diogo, HttpMethod.Post, $"{Rota}/{envio.PerfilId}/versoes/{envio.VersaoId}/documento/confirmacao", new { etag = " " });

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.perfil.documento_invalido");
    }

    [Fact(DisplayName = "Outro documento sem recusa anterior e leitura de documento não verificado devolvem 422 sem URL")]
    public async Task Documento_NovoSemRecusaELeituraSemVerificacao_Recusam()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        using HttpResponseMessage criada = await EnviarAsync(diogo, HttpMethod.Post, Rota, Corpo(Nome(), [ConsultarCatalogo]));
        Envio envio = LerEnvio(await criada.Content.ReadAsStringAsync());
        string rota = $"{Rota}/{envio.PerfilId}/versoes/{envio.VersaoId}/documento";

        using HttpResponseMessage novo = await EnviarAsync(diogo, HttpMethod.Post, rota, new { tamanhoDoDocumento = 2048 });
        using HttpResponseMessage leitura = await diogo.GetAsync(new Uri(rota, UriKind.Relative));

        novo.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await novo.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.perfil.documento_nao_recusado");
        leitura.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await leitura.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.perfil.documento_nao_verificado");
    }

    [Fact(DisplayName = "Ler o documento verificado devolve a URL sem cache e grava o acesso na trilha")]
    public async Task Documento_LerVerificado_GravaATrilha()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        Guid id = await SemearPerfilVigenteAsync(ConsultarCatalogo);
        Guid versao = await VersaoVigenteAsync(id);

        using HttpResponseMessage resposta = await diogo.GetAsync(new Uri($"{Rota}/{id}/versoes/{versao}/documento", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        resposta.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await ContarLeiturasAsync(id)).Should().Be(1);
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

    [Theory(DisplayName = "Sem a permissão de manter perfis, toda operação é 403, inclusive a leitura")]
    [InlineData("GET", "")]
    [InlineData("POST", "")]
    [InlineData("DELETE", "/019a0000-0000-7000-8000-000000000001/ativacao")]
    [InlineData("POST", "/019a0000-0000-7000-8000-000000000001/versoes")]
    [InlineData("DELETE", "/019a0000-0000-7000-8000-000000000001/versoes/019a0000-0000-7000-8000-0000000000aa")]
    [InlineData("GET", "/019a0000-0000-7000-8000-000000000001/versoes/019a0000-0000-7000-8000-0000000000aa/documento")]
    public async Task SemPermissao_Devolve403(string metodo, string sufixo)
    {
        using HttpClient isabella = await ClienteComPermissoesAsync("leandro.viana", ConsultarCatalogo);

        using HttpResponseMessage resposta = await EnviarAsync(
            isabella, new HttpMethod(metodo), Rota + sufixo, metodo == "POST" ? Corpo(Nome(), [ConsultarCatalogo]) : null);

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "Sem autenticação a leitura é 401, não 403")]
    public async Task SemAutenticacao_Devolve401()
    {
        using HttpClient anonimo = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await anonimo.GetAsync(new Uri(Rota, UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "A migration cria o schema autorizacao sem chave estrangeira para outro schema")]
    public async Task Migration_NaoCriaFkEntreSchemas()
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        AutorizacaoDbContext db = scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>();

        int fkParaFora = await db.Database.SqlQuery<int>($"""
            SELECT count(*)::int AS "Value"
            FROM pg_constraint c
            JOIN pg_class origem ON origem.oid = c.conrelid
            JOIN pg_namespace no ON no.oid = origem.relnamespace
            JOIN pg_class alvo ON alvo.oid = c.confrelid
            JOIN pg_namespace na ON na.oid = alvo.relnamespace
            WHERE c.contype = 'f' AND no.nspname = 'autorizacao' AND na.nspname <> 'autorizacao'
            """).SingleAsync();
        int tabelas = await db.Database.SqlQuery<int>($"""
            SELECT count(*)::int AS "Value" FROM information_schema.tables
            WHERE table_schema = 'autorizacao'
              AND table_name IN ('perfis', 'perfil_permissoes', 'versoes_do_perfil', 'vigencias_do_perfil',
                                 'documentos_da_versao', 'leituras_do_documento_da_versao', 'versao_do_perfil_permissoes')
            """).SingleAsync();

        tabelas.Should().Be(7);
        fkParaFora.Should().Be(0);
    }

    [Fact(DisplayName = "O banco barra duas versões vigentes, duas pendentes e duas vigências abertas do mesmo perfil")]
    public async Task Banco_BarraDuplicidadeDeVigenciaEPendencia()
    {
        Guid id = await SemearPerfilVigenteAsync(ConsultarCatalogo);
        Guid vigente = await VersaoVigenteAsync(id);

        Func<Task> outraVigente = () => InserirVersaoAsync(id, 2, "vigente");
        Func<Task> vigenciaAberta = () => InserirVigenciaAsync(id, vigente);
        await InserirVersaoAsync(id, 3, "pendente");
        Func<Task> outraPendente = () => InserirVersaoAsync(id, 4, "pendente");

        await outraVigente.Should().ThrowAsync<Npgsql.PostgresException>();
        await vigenciaAberta.Should().ThrowAsync<Npgsql.PostgresException>();
        await outraPendente.Should().ThrowAsync<Npgsql.PostgresException>();
    }

    [Fact(DisplayName = "O banco barra vigência que aponta para versão de outro perfil")]
    public async Task Banco_BarraVigenciaDeVersaoDeOutroPerfil()
    {
        Guid um = await SemearPerfilVigenteAsync(ConsultarCatalogo);
        Guid outro = await SemearPerfilVigenteAsync(ConsultarCatalogo);
        Guid versaoDoOutro = await VersaoVigenteAsync(outro);
        await EncerrarVigenciasAsync(um);

        Func<Task> acao = () => InserirVigenciaAsync(um, versaoDoOutro);

        await acao.Should().ThrowAsync<Npgsql.PostgresException>();
    }

    [Fact(DisplayName = "O banco barra dois documentos vivos na mesma versão e admite outro depois da recusa")]
    public async Task Banco_BarraDoisDocumentosVivos()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        using HttpResponseMessage criada = await EnviarAsync(diogo, HttpMethod.Post, Rota, Corpo(Nome(), [ConsultarCatalogo]));
        Envio envio = LerEnvio(await criada.Content.ReadAsStringAsync());

        Func<Task> segundoVivo = () => InserirDocumentoAsync(envio, "aguardando_envio");
        await ExecutarAsync("UPDATE autorizacao.documentos_da_versao SET situacao = 'recusado' WHERE id = {0}", envio.DocumentoId);

        await InserirDocumentoAsync(envio, "aguardando_envio");
        await segundoVivo.Should().ThrowAsync<Npgsql.PostgresException>();
    }

    private const string Descricao = "Perfil de teste do cadastro de acesso.";

    private static string Nome() => $"Perfil {Guid.NewGuid():N}";

    private static object Corpo(string nome, string[] permissoes) =>
        new { nome, descricao = Descricao, permissoes, protocolo = "23479.000123/2026-11", tamanhoDoDocumento = 2048 };

    private static Envio LerEnvio(string json)
    {
        using JsonDocument documento = JsonDocument.Parse(json);
        JsonElement raiz = documento.RootElement;

        return new Envio(raiz.GetProperty("perfilId").GetGuid(), raiz.GetProperty("versaoId").GetGuid(), raiz.GetProperty("documentoId").GetGuid());
    }

    private sealed record Envio(Guid PerfilId, Guid VersaoId, Guid DocumentoId);

    private Task<Guid> SemearPerfilVigenteAsync(params string[] permissoes) =>
        ConcessoesDeTeste.CriarPerfilAsync(_fixture.Factory.Services, permissoes);

    private async Task<Guid> VersaoVigenteAsync(Guid perfilId)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        AutorizacaoDbContext db = scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>();

        return await db.VersoesDosPerfis.Where(v => v.PerfilId == perfilId && v.Situacao == SituacaoDaVersao.Vigente).Select(v => v.Id).SingleAsync();
    }

    private async Task<int> ContarLeiturasAsync(Guid perfilId)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        AutorizacaoDbContext db = scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>();

        return await db.LeiturasDosDocumentosDasVersoes.CountAsync(l => db.DocumentosDasVersoes.Any(d => d.Id == l.DocumentoId && d.PerfilId == perfilId));
    }

    private async Task ExecutarAsync(string sql, params object[] parametros)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().Database.ExecuteSqlRawAsync(sql, parametros);
    }

    private Task InserirVersaoAsync(Guid perfilId, int numero, string situacao) =>
        ExecutarAsync(
            """
            INSERT INTO autorizacao.versoes_do_perfil
                (id, perfil_id, numero, nome, descricao, protocolo, situacao, criada_por_emissor, criada_por_sub, criada_em, created_at)
            VALUES ({0}, {1}, {2}, {3}, 'Descrição de teste.', 'P-1', {4}, 'emissor', 'diogo.souza', now(), now())
            """,
            Guid.CreateVersion7(), perfilId, numero, Nome(), situacao);

    private Task InserirVigenciaAsync(Guid perfilId, Guid versaoId) =>
        ExecutarAsync(
            "INSERT INTO autorizacao.vigencias_do_perfil (id, perfil_id, versao_id, desde, created_at) VALUES ({0}, {1}, {2}, now(), now())",
            Guid.CreateVersion7(), perfilId, versaoId);

    private Task EncerrarVigenciasAsync(Guid perfilId) =>
        ExecutarAsync("UPDATE autorizacao.vigencias_do_perfil SET ate = now() WHERE perfil_id = {0}", perfilId);

    private Task InserirDocumentoAsync(Envio envio, string situacao) =>
        ExecutarAsync(
            "INSERT INTO autorizacao.documentos_da_versao (id, versao_id, perfil_id, situacao, tamanho_declarado, created_at) VALUES ({0}, {1}, {2}, {3}, 10, now())",
            Guid.CreateVersion7(), envio.VersaoId, envio.PerfilId, situacao);

    private static async Task<JsonElement[]> ListarTudoAsync(HttpClient client)
    {
        using HttpResponseMessage resposta = await client.GetAsync(new Uri($"{Rota}?limit=100", UriKind.Relative));
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument pagina = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        JsonElement itens = pagina.RootElement.ValueKind == JsonValueKind.Array ? pagina.RootElement : pagina.RootElement.GetProperty("items");

        return [.. itens.EnumerateArray().Select(i => i.Clone())];
    }

    private async Task SemearConcessaoAsync(Guid perfilId, EscopoDaConcessao escopo)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        IConcessaoRepository repositorio = scope.ServiceProvider.GetRequiredService<IConcessaoRepository>();
        Perfil perfil = (await repositorio.ObterParaConcederAsync(perfilId))!;
        Identidade diogo = new(TestAuthHandler.DefaultIssuer, "diogo.souza");

        Concessao concessao = Concessao.Conceder(
            new Identidade(TestAuthHandler.DefaultIssuer, "leandro.viana"),
            perfil,
            escopo,
            new OrigemDaConcessao(TipoDeOrigem.Pedido, Guid.NewGuid()),
            diogo,
            DateTimeOffset.UtcNow,
            scope.ServiceProvider.GetRequiredService<ICatalogoDePermissoesConsultavel>()).Value!;
        await repositorio.AdicionarAsync(concessao);
        await scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().SaveChangesAsync();
    }

    private static async Task<Guid> CriarAsync(HttpClient client, string[]? permissoes = null)
    {
        using HttpResponseMessage resposta = await EnviarAsync(
            client, HttpMethod.Post, Rota, Corpo(Nome(), permissoes ?? [ConsultarCatalogo]));
        resposta.StatusCode.Should().Be(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync());
        return LerEnvio(await resposta.Content.ReadAsStringAsync()).PerfilId;
    }

    private static async Task<JsonDocument> LerAsync(HttpClient client, Guid id)
    {
        using HttpResponseMessage resposta = await client.GetAsync(new Uri($"{Rota}/{id}", UriKind.Relative));
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
    }

    private static async Task<HttpResponseMessage> EnviarAsync(
        HttpClient client, HttpMethod metodo, string url, object? corpo = null, string? chave = null)
    {
        using HttpRequestMessage request = new(metodo, new Uri(url, UriKind.Relative));
        if (corpo is not null)
        {
            request.Content = JsonContent.Create(corpo);
        }

        if (metodo != HttpMethod.Get)
        {
            request.Headers.Add("Idempotency-Key", chave ?? Guid.NewGuid().ToString());
        }

        return await client.SendAsync(request);
    }

    // O token só identifica a pessoa; o que ela pode vem da concessão gravada, como em produção.
    private async Task<HttpClient> ClienteDoGestorAsync()
    {
        await ConcessoesDeTeste.ConcederGovernancaAsync(_fixture.Factory.Services, Diogo, TipoDeGovernanca.GestorDeAcessos);
        return Cliente(Diogo);
    }

    private async Task<HttpClient> ClienteComPermissoesAsync(string sub, params string[] permissoes)
    {
        await ConcessoesDeTeste.ConcederPermissoesAsync(_fixture.Factory.Services, sub, permissoes);
        return Cliente(sub);
    }

    private HttpClient Cliente(string sub)
    {
        HttpClient client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, sub);
        client.DefaultRequestHeaders.Add(TestAuthHandler.NameHeader, sub);
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, "teste@unifesspa.edu.br");
        return client;
    }
}
