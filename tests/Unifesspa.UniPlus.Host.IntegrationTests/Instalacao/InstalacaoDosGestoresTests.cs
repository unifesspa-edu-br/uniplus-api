namespace Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;

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

/// <summary>A instalação dos primeiros gestores de acessos pelo host real: o início, o documento e o envio do código.</summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class InstalacaoDosGestoresTests : InstalacaoTestBase
{
    public InstalacaoDosGestoresTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "Instalação confirmada com o código concede os dois gestores, que só valem depois da confirmação")]
    public async Task Instalacao_Feliz_ConcedeOsDoisGestores()
    {
        await ResetarAsync();
        using HttpClient paulo = Cliente(Paulo);
        Guid id = await ChegarAoCodigoEnviadoAsync(paulo);
        (await ListarGestoresAtivosAsync()).Should().BeEmpty("os gestores só valem depois da confirmação");

        using HttpResponseMessage confirmada = await EnviarAsync(paulo, HttpMethod.Post, $"{Rota}/confirmacao", new { codigo = await UltimoCodigoAsync(Paulo) });

        confirmada.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ListarGestoresAtivosAsync()).Should().BeEquivalentTo(Diogo.Sub.ToString(), Isabella.Sub.ToString());
        (await TiposDaTrilhaDaInstalacaoAsync(id)).Should().Equal("instalacao_iniciada", "documento_verificado", "codigo_enviado", "tentativas", "instalacao_confirmada", "termo_excecao_da_instalacao");
        (await TiposDaTrilhaDaConcessaoAsync(id, Diogo)).Should().Contain(["concedida", "instalacao_iniciada", "documento_verificado", "codigo_enviado", "instalacao_confirmada"]);
    }

    [Fact(DisplayName = "A concessão de gestor da instalação guarda o e-mail, e o PDF fica no armazenamento")]
    public async Task Instalacao_Feliz_GuardaOEmailEOPdf()
    {
        (Guid id, _) = await InstalarAsync();

        Concessao deDiogo = await ConcessaoDeGestorAsync(Diogo);
        deDiogo.BeneficiarioEmail.Should().Be(Diogo.Email, "a revogação do gestor pede o e-mail dele de volta");
        deDiogo.ConfirmaEmailDoBeneficiario(Diogo.Email.ToUpperInvariant()).Should().BeTrue();
        using IServiceScope escopo = Fixture.Factory.Services.CreateScope();
        (await escopo.ServiceProvider.GetRequiredService<IStorageService>()
            .ObterMetadadosAsync(MonolitoDaInstalacaoFixture.BucketDeEnvio, $"autorizacao/instalacao/{id}/envio")).Should().NotBeNull("o PDF subiu ao armazenamento");
    }

    [Fact(DisplayName = "A instalação confirmada guarda o resumo do documento e não guarda o código, nem no log")]
    public async Task Instalacao_Feliz_NaoGuardaOCodigo()
    {
        (Guid id, string codigo) = await InstalarAsync();

        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        Instalacao gravada = await scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().Instalacoes.AsNoTracking().SingleAsync(i => i.Id == id);
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

    // A instalação de Diogo e Isabella, por Paulo, até a confirmação com o código.
    private async Task<(Guid Id, string Codigo)> InstalarAsync()
    {
        await ResetarAsync();
        using HttpClient paulo = Cliente(Paulo);
        Guid id = await ChegarAoCodigoEnviadoAsync(paulo);
        string codigo = await UltimoCodigoAsync(Paulo);
        (await EnviarAsync(paulo, HttpMethod.Post, $"{Rota}/confirmacao", new { codigo })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        return (id, codigo);
    }

    private async Task<Concessao> ConcessaoDeGestorAsync(Persona gestor)
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().Concessoes
            .AsNoTracking().SingleAsync(c => c.PerfilId == PerfisDeSistema.GestorDeAcessosId && c.BeneficiarioSub == gestor.Sub.ToString());
    }
}
