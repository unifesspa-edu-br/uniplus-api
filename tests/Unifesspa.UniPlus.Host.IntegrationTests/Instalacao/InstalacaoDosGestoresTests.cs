namespace Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Minio;

using Unifesspa.UniPlus.Autorizacao.Application.Commands;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Instalacoes;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Storage;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosDaInscricao;
using Unifesspa.UniPlus.Verificador.Antivirus;
using Unifesspa.UniPlus.Verificador.Armazenamento;
using Unifesspa.UniPlus.Verificador.Configuracao;
using Unifesspa.UniPlus.Verificador.Contratos;
using Unifesspa.UniPlus.Verificador.Observabilidade;
using Unifesspa.UniPlus.Verificador.Verificacao;

using Wolverine;
using Wolverine.Runtime.Handlers;

/// <summary>
/// A instalação dos primeiros gestores de acessos pelo host real: Postgres, RabbitMQ, MinIO e Mailpit em contêineres.
/// O PDF sobe pela URL assinada e o veredicto do verificador é injetado na fila de resultados da Autorização, como o
/// verificador o publica. As personas são as do portal: Paulo Conceição executa, Manoel Aparício também tem o papel,
/// e Diogo, Isabella e Betina são os gestores designados.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed partial class InstalacaoDosGestoresTests
{
    private const string Rota = "/api/autorizacao/instalacao";
    private const string PapelDeInstalacao = "uniplus-instalacao";
    private const string Numero = "SIPAC 4521/2026";

    private static readonly byte[] Pdf = [.. "%PDF-1.7 documento de designacao de teste"u8];

    private static readonly Persona Paulo = new("paulo.conceicao", "paulo-daconceicao93@uniplus-test.local");
    private static readonly Persona Manoel = new("manoel.aparicio", "manoel.guilherme.aparicio@uniplus-test.local");
    private static readonly Persona Diogo = new("diogo.souza", "diogo.bruno.souza@uniplus-test.local");
    private static readonly Persona Isabella = new("isabella.figueiredo", "isabella.figueiredo@uniplus-test.local");
    private static readonly Persona Betina = new("betina.neves", "betina.neves@uniplus-test.local");

    private readonly MonolitoDaInstalacaoFixture _fixture;

    public InstalacaoDosGestoresTests(MonolitoDaInstalacaoFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "Instalação confirmada com o código concede os dois gestores e a trilha registra tudo, sem o código")]
    public async Task Instalacao_Feliz_ConcedeOsDoisGestores()
    {
        await ResetarAsync();
        using HttpClient paulo = Cliente(Paulo);

        (Guid id, Uri url) = await IniciarAsync(paulo, Diogo, Isabella);
        (await ListarGestoresAtivosAsync()).Should().BeEmpty();
        string etag = await EnviarPdfAsync(url);
        (await EnviarAsync(paulo, HttpMethod.Post, $"{Rota}/{id}/documento/confirmacao", new { etag })).StatusCode.Should().Be(HttpStatusCode.OK);
        await InjetarResultadoAsync(id, VeredictoDaVerificacao.Verificado);
        await AguardarSituacaoAsync(paulo, id, "documento_verificado");

        string codigo = await PedirCodigoAsync(paulo, id, Paulo);
        (await ListarGestoresAtivosAsync()).Should().BeEmpty("os gestores só valem depois da confirmação");

        using HttpResponseMessage confirmada = await EnviarAsync(paulo, HttpMethod.Post, $"{Rota}/confirmacao", new { codigo });

        confirmada.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ListarGestoresAtivosAsync()).Should().BeEquivalentTo(Diogo.Sub.ToString(), Isabella.Sub.ToString());
        await using (AsyncServiceScope escopoDaConcessao = _fixture.Factory.Services.CreateAsyncScope())
        {
            Concessao deDiogo = await escopoDaConcessao.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().Concessoes
                .AsNoTracking().SingleAsync(c => c.PerfilId == PerfisDeSistema.GestorDeAcessosId && c.BeneficiarioSub == Diogo.Sub.ToString());
            deDiogo.BeneficiarioEmail.Should().Be(Diogo.Email, "a revogação do gestor pede o e-mail dele de volta");
            deDiogo.ConfirmaEmailDoBeneficiario(Diogo.Email.ToUpperInvariant()).Should().BeTrue();
        }

        using (IServiceScope escopoDoArmazenamento = _fixture.Factory.Services.CreateScope())
        {
            (await escopoDoArmazenamento.ServiceProvider.GetRequiredService<IStorageService>()
                .ObterMetadadosAsync(MonolitoDaInstalacaoFixture.BucketDeEnvio, $"autorizacao/instalacao/{id}/envio")).Should().NotBeNull("o PDF subiu ao armazenamento");
        }

        string[] tipos = await TiposDaTrilhaDaInstalacaoAsync(id);
        tipos.Should().Equal("instalacao_iniciada", "documento_verificado", "codigo_enviado", "tentativas", "instalacao_confirmada");
        string[] daConcessao = await TiposDaTrilhaDaConcessaoAsync(id, Diogo);
        daConcessao.Should().Contain(["concedida", "instalacao_iniciada", "documento_verificado", "codigo_enviado", "instalacao_confirmada"]);

        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        AutorizacaoDbContext db = scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>();
        Instalacao gravada = await db.Instalacoes.AsNoTracking().SingleAsync(i => i.Id == id);
        gravada.DocumentoSha256.Should().Be(new string('a', 64));
        gravada.CodigoResumo.Should().BeNull("o código vale uma vez");
        LerLog().Should().NotMatchRegex($"(?<!\\d){codigo}(?!\\d)");
    }

    [Fact(DisplayName = "Com gestor de acessos ativo a instalação é recusada")]
    public async Task Iniciar_ComGestorAtivo_Recusa()
    {
        await ResetarAsync();
        await SemearGestorAtivoAsync(Diogo);
        using HttpClient paulo = Cliente(Paulo);

        using HttpResponseMessage resposta = await IniciarBrutoAsync(paulo, Isabella, Betina);

        await ConferirRecusaAsync(resposta, "ja_ha_gestor");
    }

    [Fact(DisplayName = "Quem executa não se instala")]
    public async Task Iniciar_ExecutorEntreGestores_Recusa()
    {
        await ResetarAsync();
        using HttpClient paulo = Cliente(Paulo);

        using HttpResponseMessage resposta = await IniciarBrutoAsync(paulo, Paulo, Isabella);

        await ConferirRecusaAsync(resposta, "executor_entre_gestores");
    }

    [Fact(DisplayName = "Instalação sem documento é recusada")]
    public async Task Iniciar_SemDocumento_Recusa()
    {
        await ResetarAsync();
        using HttpClient paulo = Cliente(Paulo);

        using HttpResponseMessage semNumero = await IniciarBrutoAsync(paulo, Diogo, Isabella, numero: "  ");
        using HttpResponseMessage semPdf = await IniciarBrutoAsync(paulo, Diogo, Isabella, tamanho: 0);

        await ConferirRecusaAsync(semNumero, "documento_invalido");
        await ConferirRecusaAsync(semPdf, "documento_invalido");
    }

    [Fact(DisplayName = "O código não sai antes de o documento ser verificado")]
    public async Task PedirCodigo_AntesDoDocumentoVerificado_Recusa()
    {
        await ResetarAsync();
        using HttpClient paulo = Cliente(Paulo);
        (Guid id, Uri url) = await IniciarAsync(paulo, Diogo, Isabella);
        string etag = await EnviarPdfAsync(url);
        (await EnviarAsync(paulo, HttpMethod.Post, $"{Rota}/{id}/documento/confirmacao", new { etag })).StatusCode.Should().Be(HttpStatusCode.OK);
        await LimparCaixaAsync();

        using HttpResponseMessage resposta = await EnviarAsync(paulo, HttpMethod.Post, $"{Rota}/{id}/codigo");

        await ConferirRecusaAsync(resposta, "situacao_invalida");
        (await MensagensPara(Paulo)).Should().BeEmpty();
    }

    [Fact(DisplayName = "O veredicto de recusa do verificador encerra o documento e o código nunca sai")]
    public async Task Verificador_Recusa_EncerraODocumento()
    {
        await ResetarAsync();
        using HttpClient paulo = Cliente(Paulo);
        (Guid id, Uri url) = await IniciarAsync(paulo, Diogo, Isabella);
        string etag = await EnviarPdfAsync(url);
        (await EnviarAsync(paulo, HttpMethod.Post, $"{Rota}/{id}/documento/confirmacao", new { etag })).StatusCode.Should().Be(HttpStatusCode.OK);

        await InjetarResultadoAsync(id, VeredictoDaVerificacao.Recusado, MotivoDaRecusa.PdfComSenha);
        await AguardarSituacaoAsync(paulo, id, "documento_recusado");
        (await TiposDaTrilhaDaInstalacaoAsync(id)).Should().Equal("instalacao_iniciada", "documento_recusado");

        using HttpResponseMessage resposta = await EnviarAsync(paulo, HttpMethod.Post, $"{Rota}/{id}/codigo");
        await ConferirRecusaAsync(resposta, "situacao_invalida");
    }

    [Theory(DisplayName = "O código só vai a e-mail verificado de domínio institucional")]
    [InlineData("paulo-daconceicao93@uniplus-test.local", "false")]
    [InlineData("paulo.pessoal@example.org", "true")]
    public async Task PedirCodigo_EmailNaoInstitucional_Recusa(string email, string verificado)
    {
        await ResetarAsync();
        using HttpClient paulo = Cliente(Paulo);
        Guid id = await ChegarAoDocumentoVerificadoAsync(paulo);
        await LimparCaixaAsync();
        using HttpClient semEmailInstitucional = Cliente(Paulo, email: email, emailVerificado: verificado);

        using HttpResponseMessage resposta = await EnviarAsync(semEmailInstitucional, HttpMethod.Post, $"{Rota}/{id}/codigo");

        await ConferirRecusaAsync(resposta, "email_nao_institucional");
        (await MensagensPara(Paulo)).Should().BeEmpty();
    }

    [Fact(DisplayName = "Outra pessoa com o papel confirma sem consumir as tentativas de quem iniciou")]
    public async Task Confirmar_OutraPessoa_NaoConsomeTentativasDeQuemIniciou()
    {
        await ResetarAsync();
        using HttpClient paulo = Cliente(Paulo);
        Guid id = await ChegarAoCodigoEnviadoAsync(paulo);
        string codigoDePaulo = await UltimoCodigoAsync(Paulo);
        using HttpClient manoel = Cliente(Manoel);

        using HttpResponseMessage resposta = await EnviarAsync(manoel, HttpMethod.Post, $"{Rota}/confirmacao", new { codigo = codigoDePaulo });

        await ConferirRecusaAsync(resposta, "codigo_invalido");
        JsonElement situacao = await LerAsync(paulo, $"{Rota}/{id}");
        situacao.GetProperty("tentativasRestantes").GetInt32().Should().Be(Instalacao.MaximoDeTentativas);
        situacao.GetProperty("situacao").GetString().Should().Be("aguardando_codigo");
        (await ListarTentativasSemPendenciaAsync()).Should().Contain(t => t.Autor == Manoel.Sub.ToString() && t.Resultado == "sem_pendencia");
        (await EnviarAsync(paulo, HttpMethod.Post, $"{Rota}/confirmacao", new { codigo = codigoDePaulo })).StatusCode
            .Should().Be(HttpStatusCode.NoContent, "o código de Paulo continua valendo");
    }

    [Fact(DisplayName = "Reenviar o código invalida o anterior, reabre o prazo e entra na trilha")]
    public async Task PedirCodigo_Reenvio_InvalidaOAnteriorEReabreOPrazo()
    {
        await ResetarAsync();
        using HttpClient paulo = Cliente(Paulo);
        Guid id = await ChegarAoCodigoEnviadoAsync(paulo);
        string antigo = await UltimoCodigoAsync(Paulo);
        await ExecutarSqlAsync("UPDATE autorizacao.instalacoes SET codigo_enviado_em = now() - interval '3 hours', codigo_expira_em = now() - interval '1 hour' WHERE id = {0}", id);

        string novo = await PedirCodigoAsync(paulo, id, Paulo);

        novo.Should().NotBe(antigo);
        JsonElement reaberta = await LerAsync(paulo, $"{Rota}/{id}");
        reaberta.GetProperty("situacao").GetString().Should().Be("aguardando_codigo");
        Dictionary<string, DateTimeOffset> trilha = await InstantesDaTrilhaDaInstalacaoAsync(id);
        trilha.Should().ContainKeys("codigo_enviado", "codigo_reenviado");
        trilha["codigo_enviado"].Should().BeBefore(trilha["codigo_reenviado"].AddHours(-2), "o envio original mantém o instante dele");
        using HttpResponseMessage comOAntigo = await EnviarAsync(paulo, HttpMethod.Post, $"{Rota}/confirmacao", new { codigo = antigo });
        await ConferirRecusaAsync(comOAntigo, "codigo_invalido");
        (await EnviarAsync(paulo, HttpMethod.Post, $"{Rota}/confirmacao", new { codigo = novo })).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
        (await ListarGestoresAtivosAsync()).Should().BeEquivalentTo(Diogo.Sub.ToString(), Isabella.Sub.ToString());
    }

    [Fact(DisplayName = "Outra pessoa com o papel não reenvia o código da instalação alheia")]
    public async Task PedirCodigo_Reenvio_OutraPessoa_NaoEncontra()
    {
        await ResetarAsync();
        using HttpClient paulo = Cliente(Paulo);
        Guid id = await ChegarAoCodigoEnviadoAsync(paulo);
        string codigoDePaulo = await UltimoCodigoAsync(Paulo);
        await LimparCaixaAsync();
        using HttpClient manoel = Cliente(Manoel);

        using HttpResponseMessage resposta = await EnviarAsync(manoel, HttpMethod.Post, $"{Rota}/{id}/codigo");

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await MensagensPara(Paulo)).Should().BeEmpty();
        (await MensagensPara(Manoel)).Should().BeEmpty();
        (await EnviarAsync(paulo, HttpMethod.Post, $"{Rota}/confirmacao", new { codigo = codigoDePaulo })).StatusCode
            .Should().Be(HttpStatusCode.NoContent, "o código de Paulo continua valendo");
    }

    [Fact(DisplayName = "Código vencido obriga a recomeçar")]
    public async Task Confirmar_CodigoVencido_ExpiraAInstalacao()
    {
        await ResetarAsync();
        using HttpClient paulo = Cliente(Paulo);
        Guid id = await ChegarAoCodigoEnviadoAsync(paulo);
        string codigo = await UltimoCodigoAsync(Paulo);
        await ExecutarSqlAsync("UPDATE autorizacao.instalacoes SET codigo_enviado_em = now() - interval '3 hours', codigo_expira_em = now() - interval '1 hour' WHERE id = {0}", id);

        using HttpResponseMessage resposta = await EnviarAsync(paulo, HttpMethod.Post, $"{Rota}/confirmacao", new { codigo });

        await ConferirRecusaAsync(resposta, "codigo_invalido");
        (await LerAsync(paulo, $"{Rota}/{id}")).GetProperty("situacao").GetString().Should().Be("expirada");
        (await ListarGestoresAtivosAsync()).Should().BeEmpty();
        (await TiposDaTrilhaDaInstalacaoAsync(id)).Should().Contain("instalacao_expirada");
    }

    [Fact(DisplayName = "Iniciar de novo cancela a pendência anterior e o código dela deixa de valer")]
    public async Task Iniciar_DeNovo_CancelaAAnterior()
    {
        await ResetarAsync();
        using HttpClient paulo = Cliente(Paulo);
        Guid primeira = await ChegarAoCodigoEnviadoAsync(paulo);
        string codigoDaPrimeira = await UltimoCodigoAsync(Paulo);

        (Guid segunda, _) = await IniciarAsync(paulo, Diogo, Betina);

        segunda.Should().NotBe(primeira);
        (await LerAsync(paulo, $"{Rota}/{primeira}")).GetProperty("situacao").GetString().Should().Be("cancelada");
        using HttpResponseMessage resposta = await EnviarAsync(paulo, HttpMethod.Post, $"{Rota}/confirmacao", new { codigo = codigoDaPrimeira });
        await ConferirRecusaAsync(resposta, "codigo_invalido");
        (await ListarGestoresAtivosAsync()).Should().BeEmpty();
        (await TiposDaTrilhaDaInstalacaoAsync(primeira)).Should().Contain("instalacao_cancelada", "a trilha vale também para a instalação cancelada");
    }

    [Fact(DisplayName = "A quinta tentativa com código errado cancela a instalação, inclusive para o código certo")]
    public async Task Confirmar_QuintoCodigoErrado_Cancela()
    {
        await ResetarAsync();
        using HttpClient paulo = Cliente(Paulo);
        Guid id = await ChegarAoCodigoEnviadoAsync(paulo);
        string certo = await UltimoCodigoAsync(Paulo);
        string errado = certo == "00000000" ? "11111111" : "00000000";

        for (int i = 1; i <= Instalacao.MaximoDeTentativas; i++)
        {
            using HttpResponseMessage tentativa = await EnviarAsync(paulo, HttpMethod.Post, $"{Rota}/confirmacao", new { codigo = errado });
            await ConferirRecusaAsync(tentativa, "codigo_invalido");
        }

        (await LerAsync(paulo, $"{Rota}/{id}")).GetProperty("situacao").GetString().Should().Be("cancelada");
        using HttpResponseMessage comOCodigoCerto = await EnviarAsync(paulo, HttpMethod.Post, $"{Rota}/confirmacao", new { codigo = certo });
        await ConferirRecusaAsync(comOCodigoCerto, "codigo_invalido");
        (await ListarGestoresAtivosAsync()).Should().BeEmpty();
    }

    [Fact(DisplayName = "Tentativas erradas simultâneas do mesmo executor são todas consumidas e registradas")]
    public async Task Confirmar_TentativasSimultaneas_ConsomemTodas()
    {
        await ResetarAsync();
        using HttpClient paulo = Cliente(Paulo);
        Guid id = await ChegarAoCodigoEnviadoAsync(paulo);
        string certo = await UltimoCodigoAsync(Paulo);
        string errado = certo == "00000000" ? "11111111" : "00000000";

        HttpResponseMessage[] respostas = await Task.WhenAll(Enumerable.Range(0, 3)
            .Select(_ => EnviarAsync(paulo, HttpMethod.Post, $"{Rota}/confirmacao", new { codigo = errado })));
        foreach (HttpResponseMessage resposta in respostas)
        {
            await ConferirRecusaAsync(resposta, "codigo_invalido");
            resposta.Dispose();
        }

        (await LerAsync(paulo, $"{Rota}/{id}")).GetProperty("tentativasRestantes").GetInt32()
            .Should().Be(Instalacao.MaximoDeTentativas - 3);
    }

    [Fact(DisplayName = "Falha na segunda concessão desfaz a confirmação inteira: a instalação segue aguardando o código")]
    public async Task Confirmar_FalhaNaSegundaConcessao_ReverteTudo()
    {
        await ResetarAsync();
        using HttpClient paulo = Cliente(Paulo);
        Guid id = await ChegarAoCodigoEnviadoAsync(paulo);
        string codigo = await UltimoCodigoAsync(Paulo);
        await SemearConcessaoRevogadaDaOrigemAsync(id, Isabella);

        using HttpResponseMessage resposta = await EnviarAsync(paulo, HttpMethod.Post, $"{Rota}/confirmacao", new { codigo });

        await ConferirRecusaAsync(resposta, "ja_ha_gestor");
        (await LerAsync(paulo, $"{Rota}/{id}")).GetProperty("situacao").GetString().Should().Be("aguardando_codigo");
        (await ListarGestoresAtivosAsync()).Should().BeEmpty("a concessão de Diogo, a primeira, foi revertida junto");
    }

    [Theory(DisplayName = "Sem o papel de instalação as operações respondem 403")]
    [InlineData("POST", "")]
    [InlineData("POST", "/019a0000-0000-7000-8000-000000000099/documento/confirmacao")]
    [InlineData("POST", "/019a0000-0000-7000-8000-000000000099/codigo")]
    [InlineData("POST", "/confirmacao")]
    [InlineData("GET", "/019a0000-0000-7000-8000-000000000099")]
    public async Task SemOPapel_Devolve403(string metodo, string sufixo)
    {
        using HttpClient semPapel = Cliente(Diogo, papel: false);

        using HttpResponseMessage resposta = await EnviarAsync(
            semPapel, new HttpMethod(metodo), Rota + sufixo, metodo == "POST" ? new { codigo = "12345678", etag = "x" } : null);

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "A instalação de outra pessoa responde 404, como a inexistente")]
    public async Task Obter_DeOutraPessoa_Devolve404()
    {
        await ResetarAsync();
        using HttpClient paulo = Cliente(Paulo);
        (Guid id, _) = await IniciarAsync(paulo, Diogo, Isabella);
        using HttpClient manoel = Cliente(Manoel);

        using HttpResponseMessage resposta = await manoel.GetAsync(new Uri($"{Rota}/{id}", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "A trilha e as tentativas sem pendência exigem a permissão de administrar concessões")]
    public async Task Trilha_SemPermissao_Devolve403()
    {
        await ResetarAsync();
        using HttpClient paulo = Cliente(Paulo);
        (Guid id, _) = await IniciarAsync(paulo, Diogo, Isabella);

        using HttpResponseMessage trilha = await paulo.GetAsync(new Uri($"/api/autorizacao/instalacoes/{id}/trilha", UriKind.Relative));
        using HttpResponseMessage tentativas = await paulo.GetAsync(new Uri("/api/autorizacao/instalacoes/tentativas-sem-pendencia", UriKind.Relative));

        trilha.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        tentativas.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "O resultado entregue à fila da Seleção não executa o handler da instalação")]
    public async Task Isolamento_ResultadoDaFilaDaSelecao_NaoToucaAInstalacao()
    {
        await ResetarAsync();
        using HttpClient paulo = Cliente(Paulo);
        (Guid id, Uri url) = await IniciarAsync(paulo, Diogo, Isabella);
        string etag = await EnviarPdfAsync(url);
        (await EnviarAsync(paulo, HttpMethod.Post, $"{Rota}/{id}/documento/confirmacao", new { etag })).StatusCode.Should().Be(HttpStatusCode.OK);

        await InjetarResultadoAsync(id, VeredictoDaVerificacao.Verificado, fila: FilasDaVerificacao.Resultados);

        // O handler da Seleção processou o resultado (não achou o arquivo): é o sinal de que a mensagem foi consumida.
        await AguardarLogAsync($"Resultado da verificação do arquivo {id} descartado");
        LerLog().Should().NotContain($"documento {id} da Autorização");
        (await LerAsync(paulo, $"{Rota}/{id}")).GetProperty("situacao").GetString().Should().Be("documento_em_verificacao");
    }

    [Fact(DisplayName = "O resultado entregue à fila da Autorização não executa o handler da Seleção")]
    public async Task Isolamento_ResultadoDaFilaDaAutorizacao_NaoExecutaAAtualizacaoDaSelecao()
    {
        Guid desconhecido = Guid.CreateVersion7();

        await InjetarResultadoAsync(desconhecido, VeredictoDaVerificacao.Verificado, fila: FilasDaVerificacao.ResultadosAutorizacao);

        await AguardarLogAsync($"documento {desconhecido} da Autorização descartado");
        LerLog().Should().NotContain($"Resultado da verificação do arquivo {desconhecido}");
    }

    [Fact(DisplayName = "A política de falha própria alcança a cadeia do handler da instalação, e cada cadeia tem só a sua")]
    public void PoliticaDeFalha_AlcancaACadeiaDoHandlerDaInstalacao()
    {
        HandlerGraph grafo = _fixture.Factory.Services.GetRequiredService<HandlerGraph>();
        HandlerChain[] cadeias = [.. grafo.AllChains().Where(c => c.MessageType == typeof(ResultadoDaVerificacaoDeArquivo))];

        HandlerChain daInstalacao = cadeias.Single(c => c.Handlers.Any(h => h.HandlerType == typeof(RegistrarResultadoDaVerificacaoDoDocumentoDaAutorizacaoHandler)));
        HandlerChain daSelecao = cadeias.Single(c => c.Handlers.Any(h => h.HandlerType == typeof(RegistrarResultadoDaVerificacaoDoArquivoHandler)));

        // Duas regras por cadeia: o resultado inválido direto para a fila de erro, e a reentrega com cooldown.
        daInstalacao.Failures.Count().Should().Be(2);
        daSelecao.Failures.Count().Should().Be(2);
    }

    [Fact(DisplayName = "O verificador real promove o PDF e devolve o veredicto pela fila da Autorização")]
    public async Task Verificador_EmProcesso_PromoveOPdfEPublicaNaFilaDaAutorizacao()
    {
        await ResetarAsync();
        byte[] pdf = await File.ReadAllBytesAsync(Path.Join(AppContext.BaseDirectory, "Instalacao", "sem-senha.pdf"));
        using HttpClient paulo = Cliente(Paulo);
        (Guid id, Uri url) = await IniciarAsync(paulo, Diogo, Isabella, pdf.Length);
        string etag = await EnviarPdfAsync(url, pdf);

        using IHost verificador = await IniciarVerificadorAsync();
        (await EnviarAsync(paulo, HttpMethod.Post, $"{Rota}/{id}/documento/confirmacao", new { etag })).StatusCode.Should().Be(HttpStatusCode.OK);

        await AguardarSituacaoAsync(paulo, id, "documento_verificado");
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        Instalacao gravada = await scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().Instalacoes
            .AsNoTracking().SingleAsync(i => i.Id == id);
        gravada.DocumentoSha256.Should().Be(Convert.ToHexStringLower(SHA256.HashData(pdf)));
        IStorageService storage = scope.ServiceProvider.GetRequiredService<IStorageService>();
        (await storage.ObterMetadadosAsync(MonolitoDaInstalacaoFixture.BucketDefinitivo, $"autorizacao/instalacao/{id}/definitivo"))
            .Should().NotBeNull("o verificador promove o PDF limpo ao bucket definitivo");
    }

    // O verificador que a produção roda como serviço à parte, com o armazenamento e as filas reais do teste e um
    // antivírus que nada acusa.
    private async Task<IHost> IniciarVerificadorAsync()
    {
        VerificadorOptions opcoes = new()
        {
            Paralelismo = 1,
            RabbitMq = { Uri = Environment.GetEnvironmentVariable("RabbitMq__Uri")! },
            Armazenamento =
            {
                Endpoint = _fixture.EnderecoDoMinio,
                AccessKey = MinioContainerFixture.AccessKey,
                SecretKey = MinioContainerFixture.SecretKey,
                BucketEnvios = MonolitoDaInstalacaoFixture.BucketDeEnvio,
                BucketArquivos = MonolitoDaInstalacaoFixture.BucketDefinitivo,
            },
        };

        IHost host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(Options.Create(opcoes));
                services.AddSingleton<IMinioClient>(_ => new MinioClient()
                    .WithEndpoint(opcoes.Armazenamento.Endpoint)
                    .WithCredentials(opcoes.Armazenamento.AccessKey, opcoes.Armazenamento.SecretKey)
                    .Build());
                services.AddSingleton<IArmazenamentoDeArquivos, ArmazenamentoMinio>();
                services.AddSingleton<IClamd, ClamdQueNadaAcusa>();
                services.AddSingleton(TimeProvider.System);
                services.AddSingleton<MetricasDoVerificador>();
            })
            .UseWolverine(wolverine =>
            {
                // O serviço real descobre o handler pelo assembly do próprio executável; aqui o executável é o do teste.
                wolverine.Discovery.IncludeAssembly(typeof(VerificarArquivoHandler).Assembly);
                ConfiguracaoDoWolverine.Configurar(wolverine, opcoes);
            })
            .Build();
        await host.StartAsync();
        return host;
    }

    private sealed class ClamdQueNadaAcusa : IClamd
    {
        public Task<ResultadoDoClamd> VerificarAsync(Stream conteudo, CancellationToken cancellationToken) =>
            Task.FromResult(new ResultadoDoClamd(false, null));

        public Task<DateTimeOffset> LerDataDaBaseAsync(CancellationToken cancellationToken) =>
            Task.FromResult(DateTimeOffset.UtcNow);
    }

    private static Guid SubDe(string username) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes($"persona:{username}"))[..16]);

    // Quem lê a trilha administra concessões por concessão gravada. A encarregada serve: a instalação só conta e
    // apaga as concessões do perfil do gestor.
    private async Task<HttpClient> ClienteQueLeATrilhaAsync()
    {
        await ConcessoesDeTeste.ConcederGovernancaAsync(
            _fixture.Factory.Services, Isabella.Sub.ToString(), TipoDeGovernanca.EncarregadaDeDados);
        return Cliente(Isabella, papel: false);
    }

    private HttpClient Cliente(Persona persona, bool papel = true, string? email = null, string emailVerificado = "true")
    {
        HttpClient client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, persona.Sub.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, email ?? persona.Email);
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailVerifiedHeader, emailVerificado);
        if (papel)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, PapelDeInstalacao);
        }

        return client;
    }

    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient client, HttpMethod metodo, string url, object? corpo = null)
    {
        using HttpRequestMessage request = new(metodo, new Uri(url, UriKind.Relative));
        if (corpo is not null)
        {
            request.Content = JsonContent.Create(corpo);
        }

        if (metodo != HttpMethod.Get)
        {
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        }

        return await client.SendAsync(request);
    }

    private static async Task<JsonElement> LerAsync(HttpClient client, string url)
    {
        using HttpResponseMessage resposta = await client.GetAsync(new Uri(url, UriKind.Relative));
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        return JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    private static async Task<HttpResponseMessage> IniciarBrutoAsync(
        HttpClient client, Persona primeiro, Persona segundo, string numero = Numero, long? tamanho = null) =>
        await EnviarAsync(client, HttpMethod.Post, Rota, new
        {
            gestores = new[] { Identidade(primeiro), Identidade(segundo) },
            numeroDoDocumento = numero,
            tamanhoDoPdf = tamanho ?? Pdf.Length,
        });

    private static object Identidade(Persona p) => new { emissor = TestAuthHandler.DefaultIssuer, sub = p.Sub.ToString(), email = p.Email, nome = p.Username };

    private static async Task<(Guid Id, Uri UrlDeEnvio)> IniciarAsync(HttpClient client, Persona primeiro, Persona segundo, long? tamanho = null)
    {
        using HttpResponseMessage resposta = await IniciarBrutoAsync(client, primeiro, segundo, tamanho: tamanho);
        resposta.StatusCode.Should().Be(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync());
        JsonElement corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement;
        corpo.GetProperty("tipoDeMidia").GetString().Should().Be("application/pdf");
        return (corpo.GetProperty("id").GetGuid(), new Uri(corpo.GetProperty("urlDeEnvio").GetString()!, UriKind.Absolute));
    }

    private static Task<string> EnviarPdfAsync(Uri url) => EnviarPdfAsync(url, Pdf);

    private static async Task<string> EnviarPdfAsync(Uri url, byte[] bytes)
    {
        using HttpClient http = new();
        using ByteArrayContent conteudo = new(bytes);
        conteudo.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        using HttpResponseMessage put = await http.PutAsync(url, conteudo);
        put.StatusCode.Should().Be(HttpStatusCode.OK, await put.Content.ReadAsStringAsync());
        return put.Headers.ETag!.Tag;
    }

    private async Task InjetarResultadoAsync(
        Guid id, VeredictoDaVerificacao veredicto, MotivoDaRecusa motivo = MotivoDaRecusa.NaoSeAplica, string fila = FilasDaVerificacao.ResultadosAutorizacao)
    {
        bool verificado = veredicto == VeredictoDaVerificacao.Verificado;
        ResultadoDaVerificacaoDeArquivo resultado = new(
            id, veredicto, motivo, verificado ? new string('a', 64) : null, verificado ? Pdf.Length : null, verificado ? FormatoDoArquivo.Pdf : null);

        using IServiceScope scope = _fixture.Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IMessageBus>().EndpointFor(fila).SendAsync(resultado);
    }

    private static async Task AguardarSituacaoAsync(HttpClient client, Guid id, string situacao)
    {
        await AguardarAsync(
            async () => (await LerAsync(client, $"{Rota}/{id}")).GetProperty("situacao").GetString() == situacao,
            $"a instalação {id} não chegou a {situacao}");
    }

    private static async Task AguardarAsync(Func<Task<bool>> condicao, string falha)
    {
        DateTime fim = DateTime.UtcNow.AddSeconds(45);
        while (DateTime.UtcNow < fim)
        {
            if (await condicao())
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        throw new TimeoutException(falha);
    }

    private async Task<Guid> ChegarAoDocumentoVerificadoAsync(HttpClient executor)
    {
        (Guid id, Uri url) = await IniciarAsync(executor, Diogo, Isabella);
        string etag = await EnviarPdfAsync(url);
        (await EnviarAsync(executor, HttpMethod.Post, $"{Rota}/{id}/documento/confirmacao", new { etag })).StatusCode.Should().Be(HttpStatusCode.OK);
        await InjetarResultadoAsync(id, VeredictoDaVerificacao.Verificado);
        await AguardarSituacaoAsync(executor, id, "documento_verificado");
        return id;
    }

    private async Task<Guid> ChegarAoCodigoEnviadoAsync(HttpClient executor)
    {
        Guid id = await ChegarAoDocumentoVerificadoAsync(executor);
        await PedirCodigoAsync(executor, id, Paulo);
        return id;
    }

    private async Task<string> PedirCodigoAsync(HttpClient executor, Guid id, Persona destinatario)
    {
        await LimparCaixaAsync();
        using HttpResponseMessage resposta = await EnviarAsync(executor, HttpMethod.Post, $"{Rota}/{id}/codigo");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        (await resposta.Content.ReadAsStringAsync()).Should().NotMatchRegex("(?<![0-9a-f])\\d{8}(?![0-9a-f])", "a resposta nunca traz o código");
        return await UltimoCodigoAsync(destinatario);
    }

    private async Task<string> UltimoCodigoAsync(Persona destinatario)
    {
        string? texto = null;
        await AguardarAsync(
            async () =>
            {
                JsonElement[] mensagens = await MensagensPara(destinatario);
                texto = mensagens.Length == 0 ? null : mensagens[0].GetProperty("Text").GetString();
                return texto is not null;
            },
            "o e-mail com o código não chegou ao Mailpit");

        Match codigo = CodigoNoEmail().Match(texto!);
        codigo.Success.Should().BeTrue();
        return codigo.Groups[1].Value;
    }

    [GeneratedRegex("código de confirmação é (\\d{8})\\.")]
    private static partial Regex CodigoNoEmail();

    private async Task<JsonElement[]> MensagensPara(Persona destinatario)
    {
        using HttpClient mailpit = new() { BaseAddress = _fixture.ApiDoMailpit };
        JsonElement lista = await mailpit.GetFromJsonAsync<JsonElement>("/api/v1/messages");
        List<JsonElement> completas = [];
        foreach (JsonElement resumo in lista.GetProperty("messages").EnumerateArray())
        {
            if (resumo.GetProperty("To").EnumerateArray().Any(t => string.Equals(
                t.GetProperty("Address").GetString(), destinatario.Email, StringComparison.OrdinalIgnoreCase)))
            {
                string idDaMensagem = resumo.GetProperty("ID").GetString()!;
                completas.Add(await mailpit.GetFromJsonAsync<JsonElement>($"/api/v1/message/{idDaMensagem}"));
            }
        }

        return [.. completas];
    }

    private async Task LimparCaixaAsync()
    {
        using HttpClient mailpit = new() { BaseAddress = _fixture.ApiDoMailpit };
        using HttpResponseMessage resposta = await mailpit.DeleteAsync(new Uri("/api/v1/messages", UriKind.Relative));
        resposta.EnsureSuccessStatusCode();
    }

    private static async Task ConferirRecusaAsync(HttpResponseMessage resposta, string motivo)
    {
        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().BeOneOf(HttpStatusCode.UnprocessableEntity, HttpStatusCode.NotFound, HttpStatusCode.Conflict);
        corpo.Should().Contain($"uniplus.autorizacao.instalacao.{motivo}");
    }

    private async Task<string[]> ListarGestoresAtivosAsync()
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        AutorizacaoDbContext db = scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>();
        return await db.Concessoes.AsNoTracking()
            .Where(c => c.PerfilId == PerfisDeSistema.GestorDeAcessosId && c.RevogadaEm == null)
            .Select(c => c.BeneficiarioSub)
            .ToArrayAsync();
    }

    private async Task ResetarAsync()
    {
        await _fixture.ProvisionarBucketsAsync();
        await ExecutarSqlAsync(
            "DELETE FROM autorizacao.concessoes WHERE perfil_id = {0}", PerfisDeSistema.GestorDeAcessosId);
        await ExecutarSqlAsync("DELETE FROM autorizacao.instalacao_tentativas");
        await ExecutarSqlAsync("DELETE FROM autorizacao.instalacoes");
        await LimparCaixaAsync();
    }

    private async Task ExecutarSqlAsync(string sql, params object[] parametros)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().Database.ExecuteSqlRawAsync(sql, parametros);
    }

    private async Task SemearGestorAtivoAsync(Persona gestor)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        Concessao concessao = await MontarConcessaoDeGestorAsync(scope, gestor, new OrigemDaConcessao(TipoDeOrigem.Pedido, Guid.NewGuid()));
        await scope.ServiceProvider.GetRequiredService<IConcessaoRepository>().AdicionarAsync(concessao);
        await scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().SaveChangesAsync();
    }

    // Uma concessão revogada da mesma origem e do mesmo beneficiário é o que faz a segunda gravação da confirmação
    // violar o índice único de origem.
    private async Task SemearConcessaoRevogadaDaOrigemAsync(Guid instalacaoId, Persona gestor)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        Concessao concessao = await MontarConcessaoDeGestorAsync(scope, gestor, new OrigemDaConcessao(TipoDeOrigem.Instalacao, instalacaoId));
        concessao.Revogar(new Identidade(TestAuthHandler.DefaultIssuer, Diogo.Sub.ToString()), "Semente do teste.", DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();
        await scope.ServiceProvider.GetRequiredService<IConcessaoRepository>().AdicionarAsync(concessao);
        await scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().SaveChangesAsync();
    }

    private static async Task<Concessao> MontarConcessaoDeGestorAsync(AsyncServiceScope scope, Persona gestor, OrigemDaConcessao origem)
    {
        IConcessaoRepository repositorio = scope.ServiceProvider.GetRequiredService<IConcessaoRepository>();
        Perfil perfil = (await repositorio.ObterParaConcederAsync(PerfisDeSistema.GestorDeAcessosId))!;
        return Concessao.Conceder(
                new Identidade(TestAuthHandler.DefaultIssuer, gestor.Sub.ToString()),
                perfil,
                EscopoDaConcessao.DaInstituicao(),
                origem,
                new Identidade(TestAuthHandler.DefaultIssuer, Paulo.Sub.ToString()),
                DateTimeOffset.UtcNow,
                scope.ServiceProvider.GetRequiredService<ICatalogoDePermissoesConsultavel>())
            .Value!;
    }

    private async Task<string[]> TiposDaTrilhaDaInstalacaoAsync(Guid id)
    {
        using HttpClient gestor = await ClienteQueLeATrilhaAsync();
        JsonElement eventos = await LerAsync(gestor, $"/api/autorizacao/instalacoes/{id}/trilha");
        return [.. eventos.EnumerateArray().Select(e => e.GetProperty("tipo").GetString()!)];
    }

    private async Task<Dictionary<string, DateTimeOffset>> InstantesDaTrilhaDaInstalacaoAsync(Guid id)
    {
        using HttpClient gestor = await ClienteQueLeATrilhaAsync();
        JsonElement eventos = await LerAsync(gestor, $"/api/autorizacao/instalacoes/{id}/trilha");
        return eventos.EnumerateArray().ToDictionary(e => e.GetProperty("tipo").GetString()!, e => e.GetProperty("instante").GetDateTimeOffset());
    }

    private async Task<string[]> TiposDaTrilhaDaConcessaoAsync(Guid instalacaoId, Persona beneficiario)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        string sub = beneficiario.Sub.ToString();
        Guid concessao = await scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().Concessoes.AsNoTracking()
            .Where(c => c.OrigemId == instalacaoId && c.BeneficiarioSub == sub)
            .Select(c => c.Id)
            .SingleAsync();

        using HttpClient gestor = await ClienteQueLeATrilhaAsync();
        JsonElement eventos = await LerAsync(gestor, $"/api/autorizacao/concessoes/{concessao}/trilha");
        return [.. eventos.EnumerateArray().Select(e => e.GetProperty("tipo").GetString()!)];
    }

    private async Task<(string Autor, string Resultado)[]> ListarTentativasSemPendenciaAsync()
    {
        using HttpClient gestor = await ClienteQueLeATrilhaAsync();
        JsonElement pagina = await LerAsync(gestor, "/api/autorizacao/instalacoes/tentativas-sem-pendencia");
        JsonElement itens = pagina.ValueKind == JsonValueKind.Array ? pagina : pagina.GetProperty("items");
        return [.. itens.EnumerateArray().Select(t => (
            t.GetProperty("autor").GetProperty("sub").GetString()!,
            t.GetProperty("resultado").GetString()!))];
    }

    private string LerLog()
    {
        if (!File.Exists(_fixture.ArquivoDeLog))
        {
            return string.Empty;
        }

        using FileStream arquivo = new(_fixture.ArquivoDeLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using StreamReader leitor = new(arquivo);

        // O Serilog escreve o Guid entre aspas; sem elas o trecho esperado é o do modelo da mensagem.
        return leitor.ReadToEnd().Replace("\"", string.Empty, StringComparison.Ordinal);
    }

    private async Task AguardarLogAsync(string trecho) =>
        await AguardarAsync(() => Task.FromResult(LerLog().Contains(trecho, StringComparison.Ordinal)), $"o log não registrou \"{trecho}\"");

    private sealed record Persona(string Username, string Email)
    {
        public Guid Sub { get; } = SubDe(Username);
    }
}
