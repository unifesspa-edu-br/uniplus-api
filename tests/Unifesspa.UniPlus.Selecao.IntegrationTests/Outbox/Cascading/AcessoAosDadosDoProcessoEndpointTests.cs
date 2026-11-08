namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// As permissões do processo e dos dados das inscrições, decididas pela concessão na unidade dona do processo (ADR-0078,
/// ADR-0151): configurar, publicar e acompanhar; consultar as inscrições; abrir documentos pessoais e sensíveis. Cada
/// persona recebe só a concessão do cenário, e o papel <c>plataforma-admin</c> do token não vale nada.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class AcessoAosDadosDoProcessoEndpointTests : IAsyncLifetime
{
    private const string Leandro = "leandro.viana";
    private const string Betina = "betina.neves";
    private const string Diogo = "diogo.souza";
    private const string Caroline = "caroline.lima";
    private const string FatoSensivel = "QUILOMBOLA";
    private const string FatoIdentificador = "PASSAPORTE_NUMERO";
    private const string PassaporteDeErick = "FX1234567";
    private const string GrupoDeResponsaveis = "RESPONSAVEIS_LEGAIS";
    private const string RgDoPrimeiroResponsavel = "11.111.111-1";
    private const string RgDoSegundoResponsavel = "22.222.222-2";

    private static readonly int PrimeiraOrdem = ConjuntoBasicoDaInscricao.Itens.Count;

    private readonly CascadingFixture _fixture;

    public AcessoAosDadosDoProcessoEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        foreach (string sub in new[] { Leandro, Betina, Diogo, Caroline })
        {
            await ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, sub);
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "Configurar vale na unidade dona do processo; a concessão em outra unidade responde 404, como o processo inexistente")]
    public async Task Configurar_SoNaUnidadeDona()
    {
        Guid processoId = await SemearRascunhoAsync(nameof(Configurar_SoNaUnidadeDona));
        Guid unidade = await UnidadeDoProcessoAsync(processoId);
        await ConcederAsync(Leandro, unidade, PermissaoSelecao.ProcessosConfigurar);
        await ConcederAsync(Betina, Guid.CreateVersion7(), PermissaoSelecao.ProcessosConfigurar);

        using HttpResponseMessage dona = await PutTaxaAsync(Leandro, processoId);
        using HttpResponseMessage outra = await PutTaxaAsync(Betina, processoId);
        using HttpResponseMessage inexistente = await PutTaxaAsync(Leandro, Guid.CreateVersion7());

        dona.StatusCode.Should().Be(HttpStatusCode.NoContent, await dona.Content.ReadAsStringAsync());
        outra.StatusCode.Should().Be(HttpStatusCode.NotFound);
        inexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "O papel plataforma-admin sem concessão não configura nem lê o processo")]
    public async Task AdminSemConcessao_NaoAcessa()
    {
        Guid processoId = await SemearRascunhoAsync(nameof(AdminSemConcessao_NaoAcessa));

        using HttpResponseMessage escrita = await PutTaxaAsync(Diogo, processoId, papel: "plataforma-admin");
        using HttpResponseMessage leitura = await GetAsync(Diogo, $"/api/selecao/processos-seletivos/{processoId}", papel: "plataforma-admin");

        escrita.StatusCode.Should().Be(HttpStatusCode.NotFound);
        leitura.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "Acompanhar não lê o rascunho, nem as telas de trabalho, e não publica nem configura")]
    public async Task Acompanhar_NaoPublicaNemConfigura()
    {
        Guid processoId = await SemearRascunhoAsync(nameof(Acompanhar_NaoPublicaNemConfigura));
        await ConcederAsync(Leandro, await UnidadeDoProcessoAsync(processoId), PermissaoSelecao.ProcessosAcompanhar);

        using HttpResponseMessage leitura = await GetAsync(Leandro, $"/api/selecao/processos-seletivos/{processoId}");
        using HttpResponseMessage publicar = await SendAsync(
            Leandro, HttpMethod.Post, $"/api/selecao/processos-seletivos/{processoId}/publicacao", new { });
        using HttpResponseMessage conformidade = await GetAsync(Leandro, $"/api/selecao/processos-seletivos/{processoId}/conformidade");
        using HttpResponseMessage configurar = await PutTaxaAsync(Leandro, processoId);

        leitura.StatusCode.Should().Be(HttpStatusCode.NotFound);
        conformidade.StatusCode.Should().Be(HttpStatusCode.NotFound);
        publicar.StatusCode.Should().Be(HttpStatusCode.NotFound);
        configurar.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "Com os dados públicos, pessoais e identificadores a lista traz nome e CPF mascarado, só na unidade dona, e não abre documento")]
    public async Task Inscricoes_ListaComCpfMascarado()
    {
        Cenario cenario = await SemearCenarioAsync(nameof(Inscricoes_ListaComCpfMascarado));
        await ConcederAsync(
            Leandro, cenario.UnidadeId, PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPublicosConsultar, PermissaoSelecao.DadosPessoaisConsultar,
            PermissaoSelecao.DadosIdentificadoresConsultar);
        await ConcederAsync(Betina, Guid.CreateVersion7(), PermissaoSelecao.InscricoesConsultar);

        using HttpResponseMessage lista = await GetAsync(Leandro, $"/api/selecao/processos-seletivos/{cenario.ProcessoId}/inscricoes");
        using HttpResponseMessage outraUnidade = await GetAsync(Betina, $"/api/selecao/processos-seletivos/{cenario.ProcessoId}/inscricoes");
        using HttpResponseMessage documento = await GetAsync(Leandro, cenario.RotaDoAcesso(cenario.ArquivoPessoal));

        string corpo = await lista.Content.ReadAsStringAsync();
        lista.StatusCode.Should().Be(HttpStatusCode.OK, corpo);
        using JsonDocument json = JsonDocument.Parse(corpo);
        JsonElement item = json.RootElement.EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == cenario.InscricaoId);
        item.GetProperty("cpf").GetProperty("valor").GetString().Should().Be(PersonasDeTeste.FernandaDrumond.Mascarado);
        item.GetProperty("cpf").GetProperty("podeRevelar").GetBoolean().Should().BeFalse("a revelação é pedida no detalhe, por campo");
        item.TryGetProperty("numeroInscricao", out _).Should().BeFalse("o número é dado interno");
        item.GetProperty("nome").GetProperty("valor").GetString().Should().Be(PersonasDeTeste.NomeSocialFernandaDrumond);
        corpo.Should().NotContain(PersonasDeTeste.FernandaDrumond.Valor);
        json.RootElement.EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).Should().NotContain(cenario.InscricaoEmRascunho, "o rascunho não é inscrição enviada");
        outraUnidade.StatusCode.Should().Be(HttpStatusCode.NotFound);
        documento.StatusCode.Should().Be(HttpStatusCode.NotFound, "consultar as inscrições não abre documento");
    }

    [Fact(DisplayName = "A listagem grava um registro por inscrição enviada exibida, com a decisão de dados pessoais, e quem só tem o acesso vê a linha vazia")]
    public async Task Lista_GravaUmRegistroPorInscricaoExibida()
    {
        Cenario cenario = await SemearCenarioAsync(nameof(Lista_GravaUmRegistroPorInscricaoExibida));
        await ConcederAsync(Diogo, cenario.UnidadeId, PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPessoaisConsultar);
        await ConcederAsync(Leandro, cenario.UnidadeId, PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPublicosConsultar);

        using HttpResponseMessage comDadosPessoais = await GetAsync(Diogo, $"/api/selecao/processos-seletivos/{cenario.ProcessoId}/inscricoes");
        IReadOnlyList<RegistroDecisaoAcesso> doDiogo = RegistrosDeDados(cenario.ProcessoId, PermissaoSelecao.DadosPessoaisConsultar);
        using HttpResponseMessage semDados = await GetAsync(Leandro, $"/api/selecao/processos-seletivos/{cenario.ProcessoId}/inscricoes");
        string corpo = await semDados.Content.ReadAsStringAsync();

        comDadosPessoais.StatusCode.Should().Be(HttpStatusCode.OK, await comDadosPessoais.Content.ReadAsStringAsync());
        comDadosPessoais.Headers.CacheControl!.NoStore.Should().BeTrue("a lista traz nome e CPF mascarado");
        doDiogo.Select(static r => r.ResourceId).Should().BeEquivalentTo(
            [cenario.InscricaoId, cenario.InscricaoDeErick, cenario.InscricaoDaServidora, cenario.InscricaoComGrupo], "um registro por inscrição enviada exibida");
        doDiogo.Should().OnlyContain(r => r.Permitido && r.AtorSubject == Diogo && r.InscricaoId == r.ResourceId);
        semDados.StatusCode.Should().Be(HttpStatusCode.OK, corpo);
        corpo.Should().NotContain(PersonasDeTeste.NomeSocialFernandaDrumond).And.NotContain(PersonasDeTeste.FernandaDrumond.Mascarado);
    }

    [Fact(DisplayName = "Detalhe: o arquivo e o dado de cada classe saem pela permissão dela, e cada acesso permitido fica registrado com a inscrição")]
    public async Task Detalhe_ProjetaPelaPermissaoDaClasse()
    {
        Cenario cenario = await SemearCenarioAsync(nameof(Detalhe_ProjetaPelaPermissaoDaClasse));
        await ConcederAsync(
            Diogo, cenario.UnidadeId, PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPublicosConsultar,
            PermissaoSelecao.DocumentosPessoaisConsultar, PermissaoSelecao.DocumentosPublicosConsultar);

        using HttpResponseMessage resposta = await GetAsync(
            Diogo, $"/api/selecao/processos-seletivos/{cenario.ProcessoId}/inscricoes/{cenario.InscricaoId}");

        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, corpo);
        resposta.Headers.CacheControl!.NoStore.Should().BeTrue();
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.uniplus.inscricao-administrativa.v1+json");
        using JsonDocument json = JsonDocument.Parse(corpo);
        json.RootElement.GetProperty("id").GetGuid().Should().Be(cenario.InscricaoId);
        json.RootElement.TryGetProperty("numeroInscricao", out _).Should().BeFalse("o número é dado interno");
        Guid[] arquivos = [.. json.RootElement.GetProperty("arquivos").EnumerateArray().Select(static a => a.GetProperty("arquivoId").GetGuid())];
        arquivos.Should().Contain([cenario.ArquivoPessoal, cenario.ArquivoPublico])
            .And.NotContain([cenario.ArquivoSensivel, cenario.ArquivoInterno, cenario.ArquivoIdentificador]);
        json.RootElement.GetProperty("arquivos").EnumerateArray()
            .ToDictionary(static a => a.GetProperty("arquivoId").GetGuid(), static a => a.GetProperty("classificacaoProtecao").GetString())
            .Should().Contain(new KeyValuePair<Guid, string?>(cenario.ArquivoPessoal, "PESSOAL"))
            .And.Contain(new KeyValuePair<Guid, string?>(cenario.ArquivoPublico, "PUBLICO"));
        corpo.Should().NotContain(PersonasDeTeste.FernandaDrumond.Valor).And.NotContain(PersonasDeTeste.NomeFernandaDrumond);

        string[] registradas = [.. _fixture.Factory.Services.GetRequiredService<RegistroDeDecisoesDeTeste>().Registrados
            .Where(r => r.ProcessoId == cenario.ProcessoId && r.InscricaoId == cenario.InscricaoId && r.AtorSubject == Diogo)
            .Select(static r => r.Permissao)];
        registradas.Should().Contain([PermissaoSelecao.InscricoesConsultar.Codigo, PermissaoSelecao.DocumentosPessoaisConsultar.Codigo])
            .And.NotContain(PermissaoSelecao.DocumentosSensiveisConsultar.Codigo, "a negativa do projetor é omissão silenciosa");
    }

    [Fact(DisplayName = "Detalhe: a inscrição que não existe, a de outro processo e o rascunho, pedidos por quem consulta inscrições, respondem 404 sem nenhuma decisão registrada")]
    public async Task Detalhe_SemResourcePedidoPorQuemConsulta_404SemDecisao()
    {
        Cenario cenario = await SemearCenarioAsync(nameof(Detalhe_SemResourcePedidoPorQuemConsulta_404SemDecisao));
        Cenario outroProcesso = await SemearCenarioAsync(nameof(Detalhe_SemResourcePedidoPorQuemConsulta_404SemDecisao) + "-outro");
        await ConcederAsync(
            Diogo, cenario.UnidadeId, PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPublicosConsultar, PermissaoSelecao.DadosPessoaisConsultar);

        Guid inscricaoInexistente = Guid.CreateVersion7();
        using HttpResponseMessage inexistente = await GetAsync(Diogo, RotaDoDetalhe(cenario.ProcessoId, inscricaoInexistente));
        using HttpResponseMessage deOutroProcesso = await GetAsync(Diogo, RotaDoDetalhe(cenario.ProcessoId, outroProcesso.InscricaoId));
        using HttpResponseMessage emRascunho = await GetAsync(Diogo, RotaDoDetalhe(cenario.ProcessoId, cenario.InscricaoEmRascunho));

        foreach (HttpResponseMessage recusa in new[] { inexistente, deOutroProcesso, emRascunho })
        {
            recusa.StatusCode.Should().Be(HttpStatusCode.NotFound);
            recusa.Headers.CacheControl!.NoStore.Should().BeTrue("o erro do detalhe também não é guardado em cache");
        }

        Guid[] envolvidas = [cenario.InscricaoId, inscricaoInexistente, outroProcesso.InscricaoId, cenario.InscricaoEmRascunho];
        _fixture.Factory.Services.GetRequiredService<RegistroDeDecisoesDeTeste>().Registrados
            .Where(r => r.AtorSubject == Diogo && (envolvidas.Contains(r.InscricaoId ?? Guid.Empty) || envolvidas.Contains(r.ResourceId ?? Guid.Empty)))
            .Should().BeEmpty("sem resource não há sobre o que decidir");
    }

    [Fact(DisplayName = "Detalhe: sem consultar inscrições na unidade dona responde o mesmo 404, com a negativa registrada, e a da inscrição inexistente leva a negativa sem o id")]
    public async Task Detalhe_SemAcessoAoResource_404ComNegativaRegistrada()
    {
        Cenario cenario = await SemearCenarioAsync(nameof(Detalhe_SemAcessoAoResource_404ComNegativaRegistrada));
        await ConcederAsync(Betina, Guid.CreateVersion7(), PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPessoaisConsultar);

        using HttpResponseMessage resposta = await GetAsync(Betina, RotaDoDetalhe(cenario.ProcessoId, cenario.InscricaoId));
        using HttpResponseMessage inexistente = await GetAsync(Betina, RotaDoDetalhe(cenario.ProcessoId, Guid.CreateVersion7()));

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await resposta.Content.ReadAsStringAsync()).Should().NotContain(PersonasDeTeste.NomeFernandaDrumond);
        resposta.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await RecusaAsync(resposta)).Should().BeEquivalentTo(await RecusaAsync(inexistente), "a recusa não distingue a falta de permissão da inscrição que não existe");
        RegistroDecisaoAcesso[] negativas = [.. _fixture.Factory.Services.GetRequiredService<RegistroDeDecisoesDeTeste>().Registrados
            .Where(r => r.AtorSubject == Betina && r.ProcessoId == cenario.ProcessoId)];
        negativas.Should().HaveCount(2, "só o acesso ao resource é decidido, e o conteúdo não é carregado").And.OnlyContain(r => !r.Permitido);
        string consultar = PermissaoSelecao.InscricoesConsultar.Codigo;
        negativas.Should().OnlyContain(r => r.Permissao == consultar);
        negativas.Should().ContainSingle(r => r.InscricaoId == cenario.InscricaoId);
        negativas.Should().ContainSingle(r => r.InscricaoId == null && r.ResourceId == null, "o consumidor reconhece a sondagem pela ausência do id");
    }

    [Fact(DisplayName = "Com o registro de acesso indisponível, a recusa do detalhe e a consulta permitida de dado pessoal respondem 503")]
    public async Task Detalhe_RegistroIndisponivel_503()
    {
        Cenario cenario = await SemearCenarioAsync(nameof(Detalhe_RegistroIndisponivel_503));
        await ConcederAsync(Betina, Guid.CreateVersion7(), PermissaoSelecao.InscricoesConsultar);
        await ConcederAsync(Diogo, cenario.UnidadeId, PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPublicosConsultar);

        using HttpResponseMessage recusada = await GetComRegistroIndisponivelAsync(Betina, RotaDoDetalhe(cenario.ProcessoId, cenario.InscricaoId));
        using HttpResponseMessage inexistente = await GetComRegistroIndisponivelAsync(Betina, RotaDoDetalhe(cenario.ProcessoId, Guid.CreateVersion7()));
        using HttpResponseMessage permitida = await GetComRegistroIndisponivelAsync(Diogo, RotaDoDetalhe(cenario.ProcessoId, cenario.InscricaoId));

        recusada.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        inexistente.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable, "a inscrição inexistente e a existente respondem o mesmo");
        permitida.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    private static async Task<(int Status, string? Code, string? Type, string? Title, string? Detail)> RecusaAsync(HttpResponseMessage resposta)
    {
        using JsonDocument json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        JsonElement raiz = json.RootElement;
        string? Texto(string nome) => raiz.TryGetProperty(nome, out JsonElement valor) ? valor.GetString() : null;
        return ((int)resposta.StatusCode, Texto("code"), Texto("type"), Texto("title"), Texto("detail"));
    }

    private static string RotaDoDetalhe(Guid processoId, Guid inscricaoId) =>
        $"/api/selecao/processos-seletivos/{processoId}/inscricoes/{inscricaoId}";

    [Fact(DisplayName = "Detalhe: com o identificador mascarado o CPF de Fernanda sai mascarado, com o componente cpf, e nunca em claro")]
    public async Task Detalhe_CpfSaiMascaradoComOComponente()
    {
        Cenario cenario = await SemearCenarioAsync(nameof(Detalhe_CpfSaiMascaradoComOComponente));
        await ConcederAsync(Leandro, cenario.UnidadeId, PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosIdentificadoresConsultar);

        using HttpResponseMessage resposta = await GetAsync(
            Leandro, $"/api/selecao/processos-seletivos/{cenario.ProcessoId}/inscricoes/{cenario.InscricaoId}");

        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, corpo);
        using JsonDocument json = JsonDocument.Parse(corpo);
        JsonElement cpf = json.RootElement.GetProperty("secoes").EnumerateArray()
            .SelectMany(static s => s.GetProperty("itens").EnumerateArray())
            .Single(static i => i.GetProperty("fatoCodigo").GetString() == "CPF");
        cpf.GetProperty("componente").GetString().Should().Be("cpf");
        cpf.GetProperty("valor").GetString().Should().Be(PersonasDeTeste.FernandaDrumond.Mascarado);
        cpf.GetProperty("podeRevelar").GetBoolean().Should().BeFalse();
        cpf.GetProperty("descricao").GetString().Should().NotBeNullOrWhiteSpace();
        corpo.Should().NotContain(PersonasDeTeste.FernandaDrumond.Valor).And.NotContain(PersonasDeTeste.FernandaDrumond.Formatado);
    }

    [Fact(DisplayName = "Só com o acesso às inscrições, a linha e o detalhe trazem o id e o processo, nenhum dado, e o detalhe diz que não pode emitir o comprovante e qual é o último")]
    public async Task SoComAcesso_TrazSoOId()
    {
        Cenario cenario = await SemearCenarioAsync(nameof(SoComAcesso_TrazSoOId));
        await ConcederAsync(Leandro, cenario.UnidadeId, PermissaoSelecao.InscricoesConsultar);

        using HttpResponseMessage lista = await GetAsync(Leandro, $"/api/selecao/processos-seletivos/{cenario.ProcessoId}/inscricoes");
        using HttpResponseMessage detalhe = await GetAsync(
            Leandro, $"/api/selecao/processos-seletivos/{cenario.ProcessoId}/inscricoes/{cenario.InscricaoId}");

        using JsonDocument jsonLista = JsonDocument.Parse(await lista.Content.ReadAsStringAsync());
        using JsonDocument jsonDetalhe = JsonDocument.Parse(await detalhe.Content.ReadAsStringAsync());
        jsonLista.RootElement.EnumerateArray().Select(static i => string.Join(",", i.EnumerateObject().Select(static p => p.Name)))
            .Should().OnlyContain(static nomes => nomes == "id,processo");
        // podeEmitirComprovante é capacidade de quem consulta e numeroDoUltimoComprovante é metadado da ordem de emissão, sem dado do candidato: saem ainda que nenhum dado seja permitido.
        jsonDetalhe.RootElement.EnumerateObject().Select(static p => p.Name)
            .Should().BeEquivalentTo(["id", "processo", "secoes", "derivados", "arquivos", "podeEmitirComprovante", "numeroDoUltimoComprovante"]);
        jsonDetalhe.RootElement.GetProperty("numeroDoUltimoComprovante").GetInt32().Should().Be(1, "o cenário emitiu um só comprovante");
        jsonDetalhe.RootElement.GetProperty("podeEmitirComprovante").GetBoolean().Should().BeFalse("consultar as inscrições não dá a permissão de emitir");
        jsonDetalhe.RootElement.GetProperty("processo").GetProperty("nome").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "Detalhe: quem só pode revelar, sem ver o identificador mascarado, não recebe o item do CPF")]
    public async Task Detalhe_SoRevelar_SemItemDoCpf()
    {
        Cenario cenario = await SemearCenarioAsync(nameof(Detalhe_SoRevelar_SemItemDoCpf));
        await ConcederAsync(Diogo, cenario.UnidadeId, PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosIdentificadoresRevelar);

        using HttpResponseMessage resposta = await GetAsync(
            Diogo, $"/api/selecao/processos-seletivos/{cenario.ProcessoId}/inscricoes/{cenario.InscricaoId}");

        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, corpo);
        using JsonDocument json = JsonDocument.Parse(corpo);
        json.RootElement.GetProperty("secoes").EnumerateArray()
            .SelectMany(static s => s.GetProperty("itens").EnumerateArray())
            .Should().NotContain(static i => i.GetProperty("fatoCodigo").GetString() == "CPF");
        corpo.Should().NotContain(PersonasDeTeste.FernandaDrumond.Mascarado);
    }

    [Fact(DisplayName = "Revelar o CPF só com a permissão de revelar, sem a do identificador mascarado, responde 404, registra só a negativa da classe e não entrega o valor")]
    public async Task Revelacao_SemAPermissaoDaClasse_404ESoANegativaDaClasse()
    {
        Cenario cenario = await SemearCenarioAsync(nameof(Revelacao_SemAPermissaoDaClasse_404ESoANegativaDaClasse));
        await ConcederAsync(Betina, cenario.UnidadeId, PermissaoSelecao.DadosPessoaisConsultar, PermissaoSelecao.DadosIdentificadoresRevelar);

        using HttpResponseMessage resposta = await SendAsync(
            Betina, HttpMethod.Post, cenario.RotaDaRevelacao(cenario.InscricaoId, ConjuntoBasicoDaInscricao.Cpf), null);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await resposta.Content.ReadAsStringAsync()).Should().NotContain(PersonasDeTeste.FernandaDrumond.Valor);
        RegistrosDaRevelacao(cenario, cenario.InscricaoId).Should().BeEmpty("nada foi revelado");
        _fixture.Factory.Services.GetRequiredService<RegistroDeDecisoesDeTeste>().Registrados
            .Where(r => r.AtorSubject == Betina && r.ProcessoId == cenario.ProcessoId && r.InscricaoId == cenario.InscricaoId
                && r.Permissao == PermissaoSelecao.DadosIdentificadoresConsultar.Codigo)
            .Should().ContainSingle().Which.Permitido.Should().BeFalse();
    }

    [Fact(DisplayName = "Diogo revela o passaporte de Erick: recebe o número, sem cache, e fica registrado que revelou o campo daquela inscrição")]
    public async Task Revelacao_DoPassaporte_RevelaERegistra()
    {
        Cenario cenario = await SemearCenarioAsync(nameof(Revelacao_DoPassaporte_RevelaERegistra));
        await ConcederAsync(Diogo, cenario.UnidadeId, PermissaoSelecao.DadosIdentificadoresConsultar, PermissaoSelecao.DadosIdentificadoresRevelar);

        using HttpResponseMessage resposta = await SendAsync(
            Diogo, HttpMethod.Post, cenario.RotaDaRevelacao(cenario.InscricaoDeErick, FatoIdentificador), null);

        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, corpo);
        resposta.Headers.CacheControl!.NoStore.Should().BeTrue();
        using JsonDocument json = JsonDocument.Parse(corpo);
        json.RootElement.GetProperty("fatoCodigo").GetString().Should().Be(FatoIdentificador);
        json.RootElement.GetProperty("valor").GetString().Should().Be(PassaporteDeErick);

        RegistroDecisaoAcesso decisao = RegistrosDaRevelacao(cenario, cenario.InscricaoDeErick).Should().ContainSingle().Subject;
        decisao.Permitido.Should().BeTrue();
        decisao.Permissao.Should().Be(PermissaoSelecao.DadosIdentificadoresRevelar.Codigo);
        decisao.AtorSubject.Should().Be(Diogo);
        decisao.FatoCodigo.Should().Be(FatoIdentificador);
        decisao.InscricaoId.Should().Be(cenario.InscricaoDeErick);
        decisao.GrantId.Should().NotBeNull();
        JsonSerializer.Serialize(decisao).Should().NotContain(PassaporteDeErick);
    }

    [Fact(DisplayName = "O campo dentro de grupo repetível se revela pela ocorrência informada: o valor é o da ocorrência e o registro nomeia o identificador dela")]
    public async Task Revelacao_DeCampoEmGrupo_PelaOcorrencia()
    {
        Cenario cenario = await SemearCenarioAsync(nameof(Revelacao_DeCampoEmGrupo_PelaOcorrencia));
        await ConcederAsync(Diogo, cenario.UnidadeId, PermissaoSelecao.DadosIdentificadoresConsultar, PermissaoSelecao.DadosIdentificadoresRevelar);

        using HttpResponseMessage resposta = await SendAsync(
            Diogo, HttpMethod.Post, cenario.RotaDaRevelacao(cenario.InscricaoComGrupo, cenario.FatoDoGrupo, "membro-2"), null);
        using HttpResponseMessage semOcorrencia = await SendAsync(
            Diogo, HttpMethod.Post, cenario.RotaDaRevelacao(cenario.InscricaoComGrupo, cenario.FatoDoGrupo), null);

        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, corpo);
        using JsonDocument json = JsonDocument.Parse(corpo);
        json.RootElement.GetProperty("valor").GetString().Should().Be(RgDoSegundoResponsavel);
        semOcorrencia.StatusCode.Should().Be(HttpStatusCode.NotFound, "o campo em grupo exige a ocorrência");
        RegistroDecisaoAcesso decisao = RegistrosDaRevelacao(cenario, cenario.InscricaoComGrupo)
            .Should().ContainSingle(r => r.Ocorrencia == "membro-2").Subject;
        decisao.Permitido.Should().BeTrue();
        decisao.FatoCodigo.Should().Be(cenario.FatoDoGrupo);
        decisao.Ocorrencia.Should().Be("membro-2");
    }

    [Fact(DisplayName = "O CPF de Fernanda se revela pela mesma operação, com o valor completo")]
    public async Task Revelacao_DoCpf_UsaAMesmaOperacao()
    {
        Cenario cenario = await SemearCenarioAsync(nameof(Revelacao_DoCpf_UsaAMesmaOperacao));
        await ConcederAsync(Diogo, cenario.UnidadeId, PermissaoSelecao.DadosIdentificadoresConsultar, PermissaoSelecao.DadosIdentificadoresRevelar);

        using HttpResponseMessage resposta = await SendAsync(
            Diogo, HttpMethod.Post, cenario.RotaDaRevelacao(cenario.InscricaoId, ConjuntoBasicoDaInscricao.Cpf), null);

        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, corpo);
        using JsonDocument json = JsonDocument.Parse(corpo);
        json.RootElement.GetProperty("valor").GetString().Should().Be(PersonasDeTeste.FernandaDrumond.Formatado);
    }

    [Fact(DisplayName = "Revelação recusada: Kevin sem a permissão recebe 404 e fica registrada a negativa com o código do fato")]
    public async Task Revelacao_SemAPermissao_404ERegistraANegativa()
    {
        Cenario cenario = await SemearCenarioAsync(nameof(Revelacao_SemAPermissao_404ERegistraANegativa));
        await ConcederAsync(Leandro, cenario.UnidadeId, PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosIdentificadoresConsultar);

        using HttpResponseMessage resposta = await SendAsync(
            Leandro, HttpMethod.Post, cenario.RotaDaRevelacao(cenario.InscricaoId, ConjuntoBasicoDaInscricao.Cpf), null);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await resposta.Content.ReadAsStringAsync()).Should().NotContain(PersonasDeTeste.FernandaDrumond.Valor);
        RegistroDecisaoAcesso negativa = RegistrosDaRevelacao(cenario, cenario.InscricaoId).Should().ContainSingle().Subject;
        negativa.Permitido.Should().BeFalse();
        negativa.FatoCodigo.Should().Be(ConjuntoBasicoDaInscricao.Cpf);
    }

    [Fact(DisplayName = "Revelação de inscrição que não existe, por quem não tem a permissão de revelar: 404 e negativa registrada sem o id da inscrição")]
    public async Task Revelacao_InscricaoInexistenteSemAPermissao_RegistraANegativaSemId()
    {
        Cenario cenario = await SemearCenarioAsync(nameof(Revelacao_InscricaoInexistenteSemAPermissao_RegistraANegativaSemId));
        await ConcederAsync(Leandro, cenario.UnidadeId, PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosIdentificadoresConsultar);
        Guid inexistente = Guid.CreateVersion7();

        using HttpResponseMessage resposta = await SendAsync(
            Leandro, HttpMethod.Post, cenario.RotaDaRevelacao(inexistente, ConjuntoBasicoDaInscricao.Cpf), null);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        RegistroDecisaoAcesso negativa = _fixture.Factory.Services.GetRequiredService<RegistroDeDecisoesDeTeste>().Registrados
            .Where(r => r.AtorSubject == Leandro && r.ProcessoId == cenario.ProcessoId
                && r.Permissao == PermissaoSelecao.DadosIdentificadoresRevelar.Codigo)
            .Should().ContainSingle().Subject;
        negativa.Permitido.Should().BeFalse();
        negativa.ResourceId.Should().BeNull();
        negativa.InscricaoId.Should().BeNull();
        negativa.FatoCodigo.Should().BeNull();
    }

    [Fact(DisplayName = "A permissão em outra unidade, a inscrição de outro processo, o rascunho e o campo que não é identificador respondem o mesmo 404")]
    public async Task Revelacao_ForaDoAlcance_404()
    {
        Cenario cenario = await SemearCenarioAsync(nameof(Revelacao_ForaDoAlcance_404));
        Cenario outroProcesso = await SemearCenarioAsync(nameof(Revelacao_ForaDoAlcance_404) + "-outro");
        await ConcederAsync(Diogo, cenario.UnidadeId, PermissaoSelecao.DadosIdentificadoresConsultar, PermissaoSelecao.DadosIdentificadoresRevelar);
        await ConcederAsync(Betina, Guid.CreateVersion7(), PermissaoSelecao.DadosIdentificadoresConsultar, PermissaoSelecao.DadosIdentificadoresRevelar);

        using HttpResponseMessage outraUnidade = await SendAsync(
            Betina, HttpMethod.Post, cenario.RotaDaRevelacao(cenario.InscricaoId, ConjuntoBasicoDaInscricao.Cpf), null);
        using HttpResponseMessage deOutroProcesso = await SendAsync(
            Diogo, HttpMethod.Post, cenario.RotaDaRevelacao(outroProcesso.InscricaoId, ConjuntoBasicoDaInscricao.Cpf), null);
        using HttpResponseMessage emRascunho = await SendAsync(
            Diogo, HttpMethod.Post, cenario.RotaDaRevelacao(cenario.InscricaoEmRascunho, ConjuntoBasicoDaInscricao.Cpf), null);
        using HttpResponseMessage naoIdentificador = await SendAsync(
            Diogo, HttpMethod.Post, cenario.RotaDaRevelacao(cenario.InscricaoId, ConjuntoBasicoDaInscricao.Nome), null);

        foreach (HttpResponseMessage recusa in new[] { outraUnidade, deOutroProcesso, emRascunho, naoIdentificador })
        {
            recusa.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        RegistrosDaRevelacao(cenario, cenario.InscricaoEmRascunho).Should().BeEmpty("a trilha nunca nomeia inscrição de outro processo nem em rascunho");
        RegistrosDaRevelacao(cenario, cenario.InscricaoId).Should().Contain(
            r => r.FatoCodigo == ConjuntoBasicoDaInscricao.Nome, "a decisão de revelar vem antes de procurar a resposta, ainda que o campo não seja identificador");
    }

    [Fact(DisplayName = "A servidora inscrita no processo, ainda que com a permissão de revelar, responde 403 impedida")]
    public async Task Revelacao_ServidoraInscrita_Impedida()
    {
        Cenario cenario = await SemearCenarioAsync(nameof(Revelacao_ServidoraInscrita_Impedida));
        await ConcederAsync(Caroline, cenario.UnidadeId, PermissaoSelecao.DadosIdentificadoresConsultar, PermissaoSelecao.DadosIdentificadoresRevelar);

        using HttpResponseMessage resposta = await SendAsync(
            Caroline, HttpMethod.Post, cenario.RotaDaRevelacao(cenario.InscricaoId, ConjuntoBasicoDaInscricao.Cpf), null);

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private IReadOnlyList<RegistroDecisaoAcesso> RegistrosDaRevelacao(Cenario cenario, Guid inscricaoId) =>
        [.. _fixture.Factory.Services.GetRequiredService<RegistroDeDecisoesDeTeste>().Registrados
            .Where(r => r.ProcessoId == cenario.ProcessoId
                && r.Permissao == PermissaoSelecao.DadosIdentificadoresRevelar.Codigo
                && r.InscricaoId == inscricaoId)];

    [Fact(DisplayName = "Serialização defensiva: a resposta que não passou pelo projetor sai sem nenhum campo classificado")]
    public async Task Serializacao_OmiteCampoClassificadoSemDecisao()
    {
        Cenario cenario = await SemearCenarioAsync(nameof(Serializacao_OmiteCampoClassificadoSemDecisao));
        await ConcederAsync(Diogo, cenario.UnidadeId, PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPessoaisConsultar);

        using HttpResponseMessage resposta = await GetAsync(
            Diogo, $"/api/selecao/_teste/processos-seletivos/{cenario.ProcessoId}/inscricoes/{cenario.InscricaoId}/esquecida");

        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, corpo);
        using JsonDocument json = JsonDocument.Parse(corpo);
        json.RootElement.EnumerateObject().Select(static p => p.Name).Should().Equal(
            ["id", "processo"], "sem decisão permitida na requisição só o id e o processo, que não são classificados, saem");
    }

    private IReadOnlyList<RegistroDecisaoAcesso> RegistrosDeDados(Guid processoId, PermissaoSelecao permissao) =>
        [.. _fixture.Factory.Services.GetRequiredService<RegistroDeDecisoesDeTeste>().Registrados
            .Where(r => r.ProcessoId == processoId && r.Permissao == permissao.Codigo && r.ResourceId is not null)];

    [Fact(DisplayName = "Documento pessoal exige documentos pessoais; o laudo sensível exige documentos sensíveis")]
    public async Task Documentos_PermissaoPelaClassificacaoCongeladaDaExigencia()
    {
        Cenario cenario = await SemearCenarioAsync(nameof(Documentos_PermissaoPelaClassificacaoCongeladaDaExigencia));
        await ConcederAsync(Leandro, cenario.UnidadeId, PermissaoSelecao.DocumentosPessoaisConsultar);
        await ConcederAsync(Betina, cenario.UnidadeId, PermissaoSelecao.DocumentosSensiveisConsultar);

        using HttpResponseMessage pessoalComPessoais = await GetAsync(Leandro, cenario.RotaDoAcesso(cenario.ArquivoPessoal));
        using HttpResponseMessage laudoComPessoais = await GetAsync(Leandro, cenario.RotaDoAcesso(cenario.ArquivoSensivel));
        using HttpResponseMessage laudoComSensiveis = await GetAsync(Betina, cenario.RotaDoAcesso(cenario.ArquivoSensivel));

        pessoalComPessoais.StatusCode.Should().Be(HttpStatusCode.OK, await pessoalComPessoais.Content.ReadAsStringAsync());
        laudoComPessoais.StatusCode.Should().Be(HttpStatusCode.NotFound, "documentos pessoais não abrem o laudo sensível");
        laudoComSensiveis.StatusCode.Should().Be(HttpStatusCode.OK, await laudoComSensiveis.Content.ReadAsStringAsync());
        laudoComSensiveis.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    [Fact(DisplayName = "Cada classificação do arquivo abre só com a sua permissão, sem hierarquia; a não resolvida exige a de sensíveis")]
    public async Task Documentos_CadaClassificacaoExigeASuaPermissao()
    {
        Cenario cenario = await SemearCenarioAsync(nameof(Documentos_CadaClassificacaoExigeASuaPermissao));
        (Guid Arquivo, PermissaoSelecao Exigida)[] arquivos =
        [
            (cenario.ArquivoPublico, PermissaoSelecao.DocumentosPublicosConsultar),
            (cenario.ArquivoInterno, PermissaoSelecao.DocumentosInternosConsultar),
            (cenario.ArquivoPessoal, PermissaoSelecao.DocumentosPessoaisConsultar),
            (cenario.ArquivoIdentificador, PermissaoSelecao.DocumentosIdentificadoresConsultar),
            (cenario.ArquivoSensivel, PermissaoSelecao.DocumentosSensiveisConsultar),
            (cenario.ArquivoNaoResolvido, PermissaoSelecao.DocumentosSensiveisConsultar),
        ];

        foreach (PermissaoSelecao concedida in arquivos.Select(static a => a.Exigida).Distinct())
        {
            await ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, Leandro);
            await ConcederAsync(Leandro, cenario.UnidadeId, PermissaoSelecao.InscricoesConsultar, concedida);

            foreach ((Guid arquivo, PermissaoSelecao exigida) in arquivos)
            {
                using HttpResponseMessage resposta = await GetAsync(Leandro, cenario.RotaDoAcesso(arquivo));

                resposta.StatusCode.Should().Be(
                    exigida == concedida ? HttpStatusCode.OK : HttpStatusCode.NotFound,
                    $"o arquivo que exige {exigida.Codigo} com só {concedida.Codigo} concedida");
            }
        }
    }

    [Fact(DisplayName = "Arquivo de outra inscrição ou de outro processo responde o mesmo 404 de quem não tem a permissão")]
    public async Task Documento_DeOutraInscricaoOuProcesso_404()
    {
        Cenario cenario = await SemearCenarioAsync(nameof(Documento_DeOutraInscricaoOuProcesso_404));
        await ConcederAsync(Leandro, cenario.UnidadeId, PermissaoSelecao.DocumentosPessoaisConsultar, PermissaoSelecao.DocumentosSensiveisConsultar);

        using HttpResponseMessage outraInscricao = await GetAsync(
            Leandro, $"/api/selecao/processos-seletivos/{cenario.ProcessoId}/inscricoes/{Guid.CreateVersion7()}/documentos/arquivos/{cenario.ArquivoSensivel}/acesso");
        using HttpResponseMessage inexistente = await GetAsync(
            Leandro, $"/api/selecao/processos-seletivos/{cenario.ProcessoId}/inscricoes/{cenario.InscricaoId}/documentos/arquivos/{Guid.CreateVersion7()}/acesso");

        outraInscricao.StatusCode.Should().Be(HttpStatusCode.NotFound);
        inexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "Quem tem só documentos pessoais abre o documento pessoal com uma única decisão permitida e nenhuma negativa de documento sensível")]
    public async Task Documento_GravaUmaUnicaDecisaoDaPermissaoExigida()
    {
        Cenario cenario = await SemearCenarioAsync(nameof(Documento_GravaUmaUnicaDecisaoDaPermissaoExigida));
        await ConcederAsync(Leandro, cenario.UnidadeId, PermissaoSelecao.DocumentosPessoaisConsultar);
        await ConcederAsync(Betina, cenario.UnidadeId, PermissaoSelecao.DocumentosSensiveisConsultar);

        using HttpResponseMessage pessoalAberto = await GetAsync(Leandro, cenario.RotaDoAcesso(cenario.ArquivoPessoal));
        IReadOnlyList<RegistroDecisaoAcesso> doPessoal = DecisoesDoDocumento(cenario);
        using HttpResponseMessage laudo = await GetAsync(Betina, cenario.RotaDoAcesso(cenario.ArquivoSensivel));
        IReadOnlyList<RegistroDecisaoAcesso> todas = DecisoesDoDocumento(cenario);

        pessoalAberto.StatusCode.Should().Be(HttpStatusCode.OK);
        RegistroDecisaoAcesso pessoal = doPessoal.Should().ContainSingle().Subject;
        pessoal.Permissao.Should().Be(PermissaoSelecao.DocumentosPessoaisConsultar.Codigo);
        pessoal.Permitido.Should().BeTrue();
        pessoal.InscricaoId.Should().Be(cenario.InscricaoId, "o registro do arquivo nomeia a inscrição, para a consulta por inscrição achá-lo");

        laudo.StatusCode.Should().Be(HttpStatusCode.OK);
        RegistroDecisaoAcesso sensivel = todas.Skip(1).Should().ContainSingle().Subject;
        sensivel.Permissao.Should().Be(PermissaoSelecao.DocumentosSensiveisConsultar.Codigo);
        sensivel.Permitido.Should().BeTrue();
        sensivel.Sensibilidade.Should().BeEquivalentTo("sensivel", "o laudo é registrado com a sensibilidade real, e não como pessoal");
    }

    private IReadOnlyList<RegistroDecisaoAcesso> DecisoesDoDocumento(Cenario cenario) =>
        [.. _fixture.Factory.Services.GetRequiredService<RegistroDeDecisoesDeTeste>().Registrados
            .Where(r => r.ProcessoId == cenario.ProcessoId && r.ResourceTipo == ResourceTiposSelecao.DocumentoDaInscricao)];

    [Fact(DisplayName = "Documento de inscrição em rascunho responde o mesmo 404, ainda que a pessoa tenha as duas permissões")]
    public async Task Documento_DeInscricaoEmRascunho_404()
    {
        Cenario cenario = await SemearCenarioAsync(nameof(Documento_DeInscricaoEmRascunho_404));
        await ConcederAsync(Leandro, cenario.UnidadeId, PermissaoSelecao.DocumentosPessoaisConsultar, PermissaoSelecao.DocumentosSensiveisConsultar);

        using HttpResponseMessage rascunho = await GetAsync(Leandro, cenario.RotaDoAcessoDoRascunho());
        using HttpResponseMessage enviada = await GetAsync(Leandro, cenario.RotaDoAcesso(cenario.ArquivoPessoal));

        rascunho.StatusCode.Should().Be(HttpStatusCode.NotFound);
        enviada.StatusCode.Should().Be(HttpStatusCode.OK, "a inscrição enviada do mesmo processo continua aberta");
    }

    private async Task ConcederAsync(string sub, Guid unidade, params PermissaoSelecao[] permissoes)
    {
        Guid perfil = await ConcessoesDeTeste.CriarPerfilAsync(_fixture.Factory.Services, [.. permissoes.Select(static p => p.Codigo)]);
        await ConcessoesDeTeste.ConcederAsync(_fixture.Factory.Services, sub, perfil, EscopoDaConcessao.DaUnidade(unidade));
    }

    private async Task<Guid> SemearRascunhoAsync(string nome)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, $"{nome} {Guid.CreateVersion7()}");
        return processo.Id;
    }

    private async Task<Guid> UnidadeDoProcessoAsync(Guid processoId)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<SelecaoDbContext>().ProcessosSeletivos.AsNoTracking()
            .Where(p => p.Id == processoId).Select(p => p.UnidadeAdministradoraOrigemId).SingleAsync();
    }

    private Task<HttpResponseMessage> PutTaxaAsync(string sub, Guid processoId, string? papel = null) =>
        SendAsync(sub, HttpMethod.Put, $"/api/selecao/processos-seletivos/{processoId}/taxa-inscricao", new { cobra = false, valor = (decimal?)null, fundamentos = (string[]?)null }, papel);

    private Task<HttpResponseMessage> GetAsync(string sub, string rota, string? papel = null) => SendAsync(sub, HttpMethod.Get, rota, null, papel);

    private async Task<HttpResponseMessage> GetComRegistroIndisponivelAsync(string sub, string rota)
    {
        await using WebApplicationFactory<HostAssemblyMarker> comFalha = _fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IRegistroOperacionalRestrito>();
                services.AddSingleton<IRegistroOperacionalRestrito, RegistroQueFalha>();
            }));
        using HttpClient client = comFalha.CreateClient();

        return await client.SendAsync(Requisicao(sub, HttpMethod.Get, rota, null, null));
    }

    private sealed class RegistroQueFalha : IRegistroOperacionalRestrito
    {
        public Task RegistrarAsync(IReadOnlyList<RegistroDecisaoAcesso> registros, CancellationToken cancellationToken) =>
            throw new RegistroDeAcessoNaoGravadoException(new InvalidOperationException("broker fora"));
    }

    private async Task<HttpResponseMessage> SendAsync(string sub, HttpMethod metodo, string rota, object? corpo, string? papel = null)
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        return await client.SendAsync(Requisicao(sub, metodo, rota, corpo, papel));
    }

    private static HttpRequestMessage Requisicao(string sub, HttpMethod metodo, string rota, object? corpo, string? papel)
    {
        HttpRequestMessage request = new(metodo, new Uri(rota, UriKind.Relative));
        if (corpo is not null)
        {
            request.Content = JsonContent.Create(corpo);
        }

        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.UserIdHeader, sub);
        if (papel is not null)
        {
            request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, papel);
        }

        if (metodo != HttpMethod.Get)
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        }

        return request;
    }

    private sealed record Cenario(
        Guid ProcessoId, Guid UnidadeId, Guid InscricaoId, Guid ArquivoPessoal, Guid ArquivoSensivel, Guid InscricaoEmRascunho, Guid ArquivoDoRascunho,
        Guid ArquivoPublico, Guid ArquivoInterno, Guid ArquivoIdentificador, Guid ArquivoNaoResolvido,
        Guid InscricaoDeErick, Guid InscricaoDaServidora, Guid InscricaoComGrupo, string FatoDoGrupo)
    {
        public string RotaDaRevelacao(Guid inscricao, string fatoCodigo, string? ocorrencia = null) =>
            $"/api/selecao/processos-seletivos/{ProcessoId}/inscricoes/{inscricao}/campos/{fatoCodigo}/revelacao"
            + (ocorrencia is null ? string.Empty : $"?ocorrencia={ocorrencia}");

        public string RotaDoAcessoDoRascunho() =>
            $"/api/selecao/processos-seletivos/{ProcessoId}/inscricoes/{InscricaoEmRascunho}/documentos/arquivos/{ArquivoDoRascunho}/acesso";

        public string RotaDoAcesso(Guid arquivo) =>
            $"/api/selecao/processos-seletivos/{ProcessoId}/inscricoes/{InscricaoId}/documentos/arquivos/{arquivo}/acesso";
    }

    /// <summary>
    /// Um processo publicado de verdade, com uma exigência de cada classificação (pública, interna, pessoal, identificadora e
    /// sensível), e a inscrição enviada de Fernanda com um arquivo verificado em cada uma, gravada na versão que a publicação
    /// congelou. Mais um arquivo, de exigência fora da versão congelada, cuja classificação não se resolve.
    /// </summary>
    private async Task<Cenario> SemearCenarioAsync(string nome)
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);

        Guid processoId;
        Guid documentoEdital;
        Guid faseId;
        await using (AsyncServiceScope scope = api.Services.CreateAsyncScope())
        {
            SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
            (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPublicavelSeeder
                .SemearAsync(db, $"{nome} {Guid.CreateVersion7()}");
            processoId = processo.Id;
            documentoEdital = documento.Id;
            faseId = processo.CronogramaFases.Single().Id;
        }

        string fatoDoGrupo = await SemearFatoIdentificadorDeMembroAsync();
        Guid tipoPessoal = await SemearTipoAsync(ClassificacaoDeProtecao.Pessoal, "DOCUMENTO_PESSOAL");
        Guid tipoSensivel = await SemearTipoAsync(ClassificacaoDeProtecao.Sensivel, "LAUDO_MEDICO");
        Guid tipoPublico = await SemearTipoAsync(ClassificacaoDeProtecao.Publico, "ARTIGO_PUBLICADO");
        Guid tipoInterno = await SemearTipoAsync(ClassificacaoDeProtecao.Interno, "ATA_INTERNA");
        Guid tipoIdentificador = await SemearTipoAsync(ClassificacaoDeProtecao.Identificador, "RG_IDENTIFICACAO");
        string rota = $"/api/selecao/processos-seletivos/{processoId}";

        using (HttpResponseMessage itens = await SendAdminAsync(HttpMethod.Put, $"/api/selecao/admin/processos-seletivos/{processoId}/formularios/INSCRICAO/itens", ItensDoFormulario(fatoDoGrupo)))
        {
            itens.StatusCode.Should().Be(HttpStatusCode.NoContent, await itens.Content.ReadAsStringAsync());
        }

        using (HttpResponseMessage exigencias = await SendAdminAsync(
            HttpMethod.Put, $"{rota}/documentos-exigidos", new[]
            {
                Folha(faseId, tipoPessoal, []), Folha(faseId, tipoSensivel, [FatoSensivel]), Folha(faseId, tipoPublico, []),
                Folha(faseId, tipoInterno, []), Folha(faseId, tipoIdentificador, []),
            }))
        {
            exigencias.StatusCode.Should().Be(HttpStatusCode.NoContent, await exigencias.Content.ReadAsStringAsync());
        }

        using (HttpResponseMessage publicacao = await SendAdminAsync(HttpMethod.Post, $"{rota}/publicacao", CorpoDoAto(documentoEdital)))
        {
            publicacao.StatusCode.Should().Be(HttpStatusCode.NoContent, await publicacao.Content.ReadAsStringAsync());
        }

        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
        IServiceProvider sp = escopo.ServiceProvider;
        SelecaoDbContext contexto = sp.GetRequiredService<SelecaoDbContext>();
        Guid versaoId = await contexto.VersoesConfiguracao.AsNoTracking().Where(v => v.ProcessoSeletivoId == processoId)
            .OrderByDescending(v => v.NumeroVersao).Select(v => v.Id).FirstAsync();
        Guid unidadeId = await contexto.ProcessosSeletivos.AsNoTracking().Where(p => p.Id == processoId)
            .Select(p => p.UnidadeAdministradoraOrigemId).SingleAsync();
        List<DocumentoExigido> exigidos = await contexto.Set<DocumentoExigido>().AsNoTracking()
            .Where(d => d.ProcessoSeletivoId == processoId).ToListAsync();

        IInscricaoRepository inscricoes = sp.GetRequiredService<IInscricaoRepository>();
        IDocumentoDaInscricaoRepository documentos = sp.GetRequiredService<IDocumentoDaInscricaoRepository>();
        Inscricao inscricao = Inscricao.Iniciar(processoId, PersonasDeTeste.FernandaDrumond, "sub-fernanda", "https://idp.test", versaoId).Value!;
        Result<ComprovanteInscricao> submissao = inscricao.Submeter(
            "000001", null, null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                [ConjuntoBasicoDaInscricao.Nome] = JsonSerializer.SerializeToElement(PersonasDeTeste.NomeFernandaDrumond),
                [ConjuntoBasicoDaInscricao.DesejaNomeSocial] = JsonSerializer.SerializeToElement(true),
                [ConjuntoBasicoDaInscricao.NomeSocial] = JsonSerializer.SerializeToElement(PersonasDeTeste.NomeSocialFernandaDrumond),
            },
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal), [],
            AdmitidasDoNome(), FormularioDeTeste.AvaliacaoSemOcultos,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal), new Dictionary<string, DefinicaoDaResposta>(StringComparer.Ordinal),
            PrecondicaoIfMatch.DeTags([inscricao.ETag]), DateTimeOffset.UtcNow, []);
        submissao.IsSuccess.Should().BeTrue(string.Join("; ", submissao.Errors.Select(e => e.Error.Message)));
        await inscricoes.AdicionarAsync(inscricao);
        await inscricoes.AdicionarComprovanteAsync(submissao.Value!);

        Guid arquivoPessoal = await AdicionarArquivoVerificadoAsync(documentos, inscricao.Id, exigidos.Single(d => d.TipoDocumentoCodigo.StartsWith("DOCUMENTO_PESSOAL", StringComparison.Ordinal)));
        Guid arquivoSensivel = await AdicionarArquivoVerificadoAsync(documentos, inscricao.Id, exigidos.Single(d => d.TipoDocumentoCodigo.StartsWith("LAUDO_MEDICO", StringComparison.Ordinal)));

        Guid arquivoPublico = await AdicionarArquivoVerificadoAsync(documentos, inscricao.Id, exigidos.Single(d => d.TipoDocumentoCodigo.StartsWith("ARTIGO_PUBLICADO", StringComparison.Ordinal)));
        Guid arquivoInterno = await AdicionarArquivoVerificadoAsync(documentos, inscricao.Id, exigidos.Single(d => d.TipoDocumentoCodigo.StartsWith("ATA_INTERNA", StringComparison.Ordinal)));
        Guid arquivoIdentificador = await AdicionarArquivoVerificadoAsync(documentos, inscricao.Id, exigidos.Single(d => d.TipoDocumentoCodigo.StartsWith("RG_IDENTIFICACAO", StringComparison.Ordinal)));
        DocumentoExigido foraDaVersao = DocumentoExigido.Criar(
            faseId, Guid.CreateVersion7(), "COMPROVANTE_FORA_DA_VERSAO", "Comprovante", "SAUDE", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null,
            [], [], null, FormatosPermitidos.Criar(false, [("PDF", null)]).Value!, null).Value!;
        Guid arquivoNaoResolvido = await AdicionarArquivoVerificadoAsync(documentos, inscricao.Id, foraDaVersao);

        Inscricao rascunho = Inscricao.Iniciar(processoId, PersonasDeTeste.KevinCarvalho, "sub-kevin", "https://idp.test", versaoId).Value!;
        await inscricoes.AdicionarAsync(rascunho);
        Guid arquivoDoRascunho = await AdicionarArquivoVerificadoAsync(documentos, rascunho.Id, exigidos.Single(d => d.TipoDocumentoCodigo.StartsWith("DOCUMENTO_PESSOAL", StringComparison.Ordinal)));
        Guid erick = await AdicionarInscricaoEnviadaAsync(inscricoes, processoId, versaoId, CpfDeErick, "sub-erick", PassaporteDeErick);
        Guid servidora = await AdicionarInscricaoEnviadaAsync(inscricoes, processoId, versaoId, CpfDeHelena, Caroline, null);
        Guid comGrupo = await AdicionarInscricaoComGrupoAsync(inscricoes, processoId, versaoId, fatoDoGrupo);
        await sp.GetRequiredService<ISelecaoUnitOfWork>().SalvarAlteracoesAsync();

        return new Cenario(processoId, unidadeId, inscricao.Id, arquivoPessoal, arquivoSensivel, rascunho.Id, arquivoDoRascunho,
            arquivoPublico, arquivoInterno, arquivoIdentificador, arquivoNaoResolvido, erick, servidora, comGrupo, fatoDoGrupo);
    }

    private static readonly Cpf CpfDeErick = CpfDaPersona("Erick");
    private static readonly Cpf CpfDeHelena = CpfDaPersona("Helena");

    private static readonly Cpf CpfDoResponsavel = Cpf.Criar(PersonasDeTeste.Candidatas
        .First(static c => new[] { "Fernanda", "Erick", "Helena", "Kevin" }.All(n => !c.Nome.StartsWith(n, StringComparison.Ordinal))).Cpf).Value!;

    private static Cpf CpfDaPersona(string primeiroNome) =>
        Cpf.Criar(PersonasDeTeste.Candidatas.Single(c => c.Nome.StartsWith(primeiroNome, StringComparison.Ordinal)).Cpf).Value!;

    /// <summary>Uma inscrição enviada, na versão publicada, com o CPF e, se houver, o número do passaporte em claro para o repositório cifrar.</summary>
    private static async Task<Guid> AdicionarInscricaoEnviadaAsync(
        IInscricaoRepository inscricoes, Guid processoId, Guid versaoId, Cpf cpf, string sub, string? passaporte)
    {
        Dictionary<string, JsonElement> respostas = new(StringComparer.Ordinal)
        {
            [ConjuntoBasicoDaInscricao.Cpf] = JsonSerializer.SerializeToElement(cpf.Valor),
        };
        Dictionary<string, DefinicaoDaResposta> definicoes = new(StringComparer.Ordinal)
        {
            [ConjuntoBasicoDaInscricao.Cpf] = new(TipoDoValorDeResposta.Texto, Multivalorado: false, Identificador: true),
        };
        if (passaporte is not null)
        {
            respostas[FatoIdentificador] = JsonSerializer.SerializeToElement(passaporte);
            definicoes[FatoIdentificador] = new(TipoDoValorDeResposta.Texto, Multivalorado: false, Identificador: true);
        }

        Inscricao inscricao = Inscricao.Reidratar(
            Guid.CreateVersion7(), processoId, cpf, sub, TestAuthHandler.DefaultIssuer, SituacaoInscricao.Submetida, versaoId, null, null,
            "000002", null, null, null, respostas, new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal), [], definicoes,
            revisao: 1, DateTimeOffset.UtcNow, null);
        await inscricoes.AdicionarAsync(inscricao);
        return inscricao.Id;
    }

    /// <summary>Uma inscrição enviada com duas ocorrências do grupo repetível, cada uma com o RG do responsável.</summary>
    private static async Task<Guid> AdicionarInscricaoComGrupoAsync(
        IInscricaoRepository inscricoes, Guid processoId, Guid versaoId, string fatoDoGrupo)
    {
        Dictionary<string, JsonElement> respostas = new(StringComparer.Ordinal)
        {
            [ConjuntoBasicoDaInscricao.Cpf] = JsonSerializer.SerializeToElement(CpfDoResponsavel.Valor),
        };
        Dictionary<string, DefinicaoDaResposta> definicoes = new(StringComparer.Ordinal)
        {
            [ConjuntoBasicoDaInscricao.Cpf] = new(TipoDoValorDeResposta.Texto, Multivalorado: false, Identificador: true),
            [fatoDoGrupo] = new(TipoDoValorDeResposta.Texto, Multivalorado: false, Identificador: true),
        };
        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos = new(StringComparer.Ordinal)
        {
            [GrupoDeResponsaveis] =
            [
                new OcorrenciaDeGrupoInscricao("membro-1", new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                {
                    [fatoDoGrupo] = JsonSerializer.SerializeToElement(RgDoPrimeiroResponsavel),
                }),
                new OcorrenciaDeGrupoInscricao("membro-2", new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                {
                    [fatoDoGrupo] = JsonSerializer.SerializeToElement(RgDoSegundoResponsavel),
                }),
            ],
        };

        Inscricao inscricao = Inscricao.Reidratar(
            Guid.CreateVersion7(), processoId, CpfDoResponsavel, "sub-helena", TestAuthHandler.DefaultIssuer, SituacaoInscricao.Submetida, versaoId,
            null, null, "000003", null, null, null, respostas, new Dictionary<string, JsonElement>(StringComparer.Ordinal), grupos, [],
            definicoes, revisao: 1, DateTimeOffset.UtcNow, null);
        await inscricoes.AdicionarAsync(inscricao);
        return inscricao.Id;
    }

    private static RespostasAdmitidas AdmitidasDoNome() => new(
        new Dictionary<string, DefinicaoDaResposta>(StringComparer.Ordinal)
        {
            [ConjuntoBasicoDaInscricao.Cpf] = new(TipoDoValorDeResposta.Texto, Multivalorado: false, Identificador: true),
            [ConjuntoBasicoDaInscricao.Nome] = new(TipoDoValorDeResposta.Texto, Multivalorado: false, Identificador: false),
            [ConjuntoBasicoDaInscricao.DesejaNomeSocial] = new(TipoDoValorDeResposta.Booleano, Multivalorado: false, Identificador: false),
            [ConjuntoBasicoDaInscricao.NomeSocial] = new(TipoDoValorDeResposta.Texto, Multivalorado: false, Identificador: false),
        },
        new HashSet<string>(StringComparer.Ordinal)
        {
            ConjuntoBasicoDaInscricao.Cpf, ConjuntoBasicoDaInscricao.Nome, ConjuntoBasicoDaInscricao.DesejaNomeSocial, ConjuntoBasicoDaInscricao.NomeSocial,
        },
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal),
        new HashSet<Guid>(),
        new Dictionary<string, TermoAdmitido>(StringComparer.Ordinal),
        PersonasDeTeste.NomeFernandaDrumond,
        PersonasDeTeste.NomeSocialFernandaDrumond);

    private static async Task<Guid> AdicionarArquivoVerificadoAsync(
        IDocumentoDaInscricaoRepository documentos, Guid inscricaoId, DocumentoExigido exigencia)
    {
        ApresentacaoDeDocumento apresentacao = ApresentacaoDeDocumento.Criar(inscricaoId, exigencia, null);
        ArquivoDaApresentacao arquivo = ArquivoDaApresentacao.IniciarPendente(
            apresentacao.Id, inscricaoId, exigencia, "application/pdf", 1024, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        arquivo.Confirmar("\"etag\"", TimeProvider.System).IsSuccess.Should().BeTrue();
        arquivo.RegistrarVerificado(1024, new string('c', 64), FormatoPermitido.Pdf).Should().BeTrue();
        await documentos.AdicionarApresentacaoAsync(apresentacao);
        await documentos.AdicionarArquivoAsync(arquivo);
        return arquivo.Id;
    }

    /// <summary>Um fato de membro de grupo classificado como identificador, aprovado no catálogo.</summary>
    private async Task<string> SemearFatoIdentificadorDeMembroAsync()
    {
        string codigo = $"RG_RESPONSAVEL_{Guid.NewGuid():N}"[..30].ToUpperInvariant();
        Result<FatoCandidato> criado = FatoCandidato.CriarDoAdministrador(
            codigo, "RG do responsável", "Descrição do fato.", DominioFato.Texto, CardinalidadeFato.Escalar, fonteValores: null, formato: FormatoTexto.Livre,
            "INSCRICAO", EscopoFato.MembroGrupo, ClassificacaoDeProtecao.Identificador, "Identificação do responsável legal",
            HipoteseLegalTratamento.CumprimentoObrigacaoLegal);
        criado.IsSuccess.Should().BeTrue(string.Join("; ", criado.Errors.Select(e => e.Error.Message)));
        FatoCandidato fato = criado.Value!;
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        ConfiguracaoDbContext db = escopo.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        db.Add(fato);
        await db.SaveChangesAsync();
        await db.AprovarCatalogoAsync();
        return codigo;
    }

    private async Task<Guid> SemearTipoAsync(ClassificacaoDeProtecao classificacao, string prefixo)
    {
        TipoDocumento tipo = TipoDocumento.Criar(
            $"{prefixo}_{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}", prefixo, null, "SAUDE", null, null, null,
            classificacao, "Comprovação documental do candidato.", HipoteseLegalTratamento.CumprimentoObrigacaoLegal).Value!;
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        ConfiguracaoDbContext db = escopo.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        db.Add(tipo);
        await db.SaveChangesAsync();
        await db.AprovarCatalogoAsync();
        return tipo.Id;
    }

    private static object ItensDoFormulario(string fatoDoGrupo) => new
    {
        itens = new object[]
        {
            new
            {
                fatoCodigo = FatoSensivel,
                ordem = PrimeiraOrdem,
                rotulo = FatoSensivel,
                tipoRenderizacao = "BOOLEANO",
                obrigatoriedade = "NUNCA",
                precondicao = (object?)null,
                etapaCodigo = FormularioDeInscricaoHttp.Secao,
            },
        },
        grupos = new object[]
        {
            new
            {
                codigo = GrupoDeResponsaveis,
                ordem = PrimeiraOrdem + 1,
                rotulo = "Responsáveis legais",
                etapaCodigo = FormularioDeInscricaoHttp.Secao,
                minimo = 0,
                maximo = 3,
                exibicao = (object?)null,
                obrigatoriedade = "NUNCA",
                predicadoObrigatoriedade = (object?)null,
                subitens = new object[]
                {
                    new
                    {
                        fatoCodigo = fatoDoGrupo,
                        ordem = 0,
                        rotulo = "RG do responsável",
                        tipoRenderizacao = "TEXTO",
                        obrigatoriedade = "NUNCA",
                    },
                },
            },
        },
    };

    private static object Folha(Guid faseId, Guid tipoDocumentoId, string[] comprova) => new
    {
        tipo = "FOLHA",
        documento = new
        {
            exigidoNaFaseId = faseId,
            finalidade = "INSCRICAO",
            tipoDocumentoId,
            aplicabilidade = "GERAL",
            obrigatorio = false,
            condicoes = Array.Empty<object>(),
            basesLegais = Array.Empty<object>(),
            formatosPermitidos = "QUALQUER",
            fatosComprovados = comprova,
        },
    };

    private static object CorpoDoAto(Guid documentoId) => new
    {
        numero = "001/2026",
        documentoEditalId = documentoId,
        ato = new
        {
            orgao = "CEPS",
            serie = "EDITAL",
            ano = 2026,
            dataPublicacao = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            assinante = "Diretor do CEPS",
            tipoAtoCodigo = "EDITAL_ABERTURA",
        },
    };

    private Task<HttpResponseMessage> SendAdminAsync(HttpMethod metodo, string rota, object corpo)
    {
        HttpClient client = _fixture.Factory.CreateClient();
        HttpRequestMessage request = new(metodo, new Uri(rota, UriKind.Relative)) { Content = JsonContent.Create(corpo) };
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        return client.SendAsync(request);
    }
}
