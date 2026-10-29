namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
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
    private const string FatoSensivel = "QUILOMBOLA";

    private static readonly int PrimeiraOrdem = ConjuntoBasicoDaInscricao.Itens.Count;

    private readonly CascadingFixture _fixture;

    public AcessoAosDadosDoProcessoEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        foreach (string sub in new[] { Leandro, Betina, Diogo })
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

    [Fact(DisplayName = "Consultar as inscrições lista com o CPF mascarado, só na unidade dona, e não abre documento")]
    public async Task Inscricoes_ListaComCpfMascarado()
    {
        Cenario cenario = await SemearCenarioAsync(nameof(Inscricoes_ListaComCpfMascarado));
        await ConcederAsync(Leandro, cenario.UnidadeId, PermissaoSelecao.InscricoesConsultar);
        await ConcederAsync(Betina, Guid.CreateVersion7(), PermissaoSelecao.InscricoesConsultar);

        using HttpResponseMessage lista = await GetAsync(Leandro, $"/api/selecao/processos-seletivos/{cenario.ProcessoId}/inscricoes");
        using HttpResponseMessage outraUnidade = await GetAsync(Betina, $"/api/selecao/processos-seletivos/{cenario.ProcessoId}/inscricoes");
        using HttpResponseMessage documento = await GetAsync(Leandro, cenario.RotaDoAcesso(cenario.ArquivoPessoal));

        string corpo = await lista.Content.ReadAsStringAsync();
        lista.StatusCode.Should().Be(HttpStatusCode.OK, corpo);
        using JsonDocument json = JsonDocument.Parse(corpo);
        JsonElement item = json.RootElement.EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == cenario.InscricaoId);
        item.GetProperty("cpfMascarado").GetString().Should().Be(PersonasDeTeste.FernandaDrumond.Mascarado);
        item.GetProperty("nome").GetString().Should().Be(PersonasDeTeste.NomeSocialFernandaDrumond);
        corpo.Should().NotContain(PersonasDeTeste.FernandaDrumond.Valor);
        json.RootElement.EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).Should().NotContain(cenario.InscricaoEmRascunho, "o rascunho não é inscrição enviada");
        outraUnidade.StatusCode.Should().Be(HttpStatusCode.NotFound);
        documento.StatusCode.Should().Be(HttpStatusCode.NotFound, "consultar as inscrições não abre documento");
    }

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

    [Fact(DisplayName = "Quem tem só documentos pessoais abre o RG com uma única decisão permitida e nenhuma negativa de documento sensível")]
    public async Task Documento_GravaUmaUnicaDecisaoDaPermissaoExigida()
    {
        Cenario cenario = await SemearCenarioAsync(nameof(Documento_GravaUmaUnicaDecisaoDaPermissaoExigida));
        await ConcederAsync(Leandro, cenario.UnidadeId, PermissaoSelecao.DocumentosPessoaisConsultar);
        await ConcederAsync(Betina, cenario.UnidadeId, PermissaoSelecao.DocumentosSensiveisConsultar);

        using HttpResponseMessage rg = await GetAsync(Leandro, cenario.RotaDoAcesso(cenario.ArquivoPessoal));
        IReadOnlyList<RegistroDecisaoAcesso> doRg = DecisoesDoDocumento(cenario);
        using HttpResponseMessage laudo = await GetAsync(Betina, cenario.RotaDoAcesso(cenario.ArquivoSensivel));
        IReadOnlyList<RegistroDecisaoAcesso> todas = DecisoesDoDocumento(cenario);

        rg.StatusCode.Should().Be(HttpStatusCode.OK);
        RegistroDecisaoAcesso pessoal = doRg.Should().ContainSingle().Subject;
        pessoal.Permissao.Should().Be(PermissaoSelecao.DocumentosPessoaisConsultar.Codigo);
        pessoal.Permitido.Should().BeTrue();

        laudo.StatusCode.Should().Be(HttpStatusCode.OK);
        RegistroDecisaoAcesso sensivel = todas.Skip(1).Should().ContainSingle().Subject;
        sensivel.Permissao.Should().Be(PermissaoSelecao.DocumentosSensiveisConsultar.Codigo);
        sensivel.Permitido.Should().BeTrue();
        sensivel.Sensibilidade.Should().BeEquivalentTo("sensivel", "o laudo é registrado com a sensibilidade real, e não como pessoal");
    }

    private IReadOnlyList<RegistroDecisaoAcesso> DecisoesDoDocumento(Cenario cenario) =>
        [.. _fixture.Factory.Services.GetRequiredService<RegistroDeDecisoesDeTeste>().Registrados
            .Where(r => r.ProcessoId == cenario.ProcessoId && r.RecursoTipo == AcessoAoProcesso.RecursoDocumento)];

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

    private async Task<HttpResponseMessage> SendAsync(string sub, HttpMethod metodo, string rota, object? corpo, string? papel = null)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(metodo, new Uri(rota, UriKind.Relative));
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

        return await client.SendAsync(request);
    }

    private sealed record Cenario(
        Guid ProcessoId, Guid UnidadeId, Guid InscricaoId, Guid ArquivoPessoal, Guid ArquivoSensivel, Guid InscricaoEmRascunho, Guid ArquivoDoRascunho)
    {
        public string RotaDoAcessoDoRascunho() =>
            $"/api/selecao/processos-seletivos/{ProcessoId}/inscricoes/{InscricaoEmRascunho}/documentos/arquivos/{ArquivoDoRascunho}/acesso";

        public string RotaDoAcesso(Guid arquivo) =>
            $"/api/selecao/processos-seletivos/{ProcessoId}/inscricoes/{InscricaoId}/documentos/arquivos/{arquivo}/acesso";
    }

    /// <summary>
    /// Um processo publicado de verdade, com uma exigência pessoal e outra sensível, e a inscrição enviada de Fernanda com um
    /// arquivo verificado em cada uma, gravada na versão que a publicação congelou.
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

        Guid tipoPessoal = await SemearTipoAsync(ClassificacaoDeProtecao.Pessoal, "DOCUMENTO_PESSOAL");
        Guid tipoSensivel = await SemearTipoAsync(ClassificacaoDeProtecao.Sensivel, "LAUDO_MEDICO");
        string rota = $"/api/selecao/processos-seletivos/{processoId}";

        using (HttpResponseMessage itens = await SendAdminAsync(HttpMethod.Put, $"/api/selecao/admin/processos-seletivos/{processoId}/formularios/INSCRICAO/itens", ItensDoFormulario()))
        {
            itens.StatusCode.Should().Be(HttpStatusCode.NoContent, await itens.Content.ReadAsStringAsync());
        }

        using (HttpResponseMessage exigencias = await SendAdminAsync(
            HttpMethod.Put, $"{rota}/documentos-exigidos", new[] { Folha(faseId, tipoPessoal, []), Folha(faseId, tipoSensivel, [FatoSensivel]) }))
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

        Inscricao rascunho = Inscricao.Iniciar(processoId, PersonasDeTeste.KevinCarvalho, "sub-kevin", "https://idp.test", versaoId).Value!;
        await inscricoes.AdicionarAsync(rascunho);
        Guid arquivoDoRascunho = await AdicionarArquivoVerificadoAsync(documentos, rascunho.Id, exigidos.Single(d => d.TipoDocumentoCodigo.StartsWith("DOCUMENTO_PESSOAL", StringComparison.Ordinal)));
        await sp.GetRequiredService<ISelecaoUnitOfWork>().SalvarAlteracoesAsync();

        return new Cenario(processoId, unidadeId, inscricao.Id, arquivoPessoal, arquivoSensivel, rascunho.Id, arquivoDoRascunho);
    }

    private static RespostasAdmitidas AdmitidasDoNome() => new(
        new Dictionary<string, DefinicaoDaResposta>(StringComparer.Ordinal)
        {
            [ConjuntoBasicoDaInscricao.Nome] = new(TipoDoValorDeResposta.Texto, Multivalorado: false, Identificador: false),
            [ConjuntoBasicoDaInscricao.DesejaNomeSocial] = new(TipoDoValorDeResposta.Booleano, Multivalorado: false, Identificador: false),
            [ConjuntoBasicoDaInscricao.NomeSocial] = new(TipoDoValorDeResposta.Texto, Multivalorado: false, Identificador: false),
        },
        new HashSet<string>(StringComparer.Ordinal)
        {
            ConjuntoBasicoDaInscricao.Nome, ConjuntoBasicoDaInscricao.DesejaNomeSocial, ConjuntoBasicoDaInscricao.NomeSocial,
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

    private async Task<Guid> SemearTipoAsync(ClassificacaoDeProtecao classificacao, string prefixo)
    {
        TipoDocumento tipo = TipoDocumento.Criar(
            $"{prefixo}_{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}", prefixo, null, "SAUDE", null, null, null,
            classificacao, "Comprovação documental do candidato.", HipoteseLegalTratamento.CumprimentoObrigacaoLegal).Value!;
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        ConfiguracaoDbContext db = escopo.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        db.Add(tipo);
        await db.SaveChangesAsync();
        return tipo.Id;
    }

    private static object ItensDoFormulario() => new
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
