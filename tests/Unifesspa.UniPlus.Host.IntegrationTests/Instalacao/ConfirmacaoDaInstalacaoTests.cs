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

/// <summary>A confirmação da instalação pelo código, com o reenvio, as tentativas e a reversão.</summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class ConfirmacaoDaInstalacaoTests : InstalacaoTestBase
{
    public ConfirmacaoDaInstalacaoTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
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
        (situacao.GetProperty("tentativasRestantes").GetInt32(), situacao.GetProperty("situacao").GetString()).Should().Be((Instalacao.MaximoDeTentativas, "aguardando_codigo"));
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
        (await LerAsync(paulo, $"{Rota}/{id}")).GetProperty("situacao").GetString().Should().Be("aguardando_codigo");
        Dictionary<string, DateTimeOffset> trilha = await InstantesDaTrilhaDaInstalacaoAsync(id);
        trilha.Should().ContainKeys("codigo_enviado", "codigo_reenviado");
        trilha["codigo_enviado"].Should().BeBefore(trilha["codigo_reenviado"].AddHours(-2), "o envio original mantém o instante dele");
    }

    [Fact(DisplayName = "Depois do reenvio, o código anterior é recusado e o novo confirma a instalação")]
    public async Task PedirCodigo_Reenvio_SoONovoConfirma()
    {
        await ResetarAsync();
        using HttpClient paulo = Cliente(Paulo);
        Guid id = await ChegarAoCodigoEnviadoAsync(paulo);
        string antigo = await UltimoCodigoAsync(Paulo);

        string novo = await PedirCodigoAsync(paulo, id, Paulo);

        await ConferirRecusaAsync(await ConfirmarAsync(paulo, antigo), "codigo_invalido");
        (await ConfirmarAsync(paulo, novo)).StatusCode.Should().Be(HttpStatusCode.NoContent);
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

        for (int i = 1; i <= Instalacao.MaximoDeTentativas; i++)
        {
            await ConferirRecusaAsync(await ConfirmarAsync(paulo, Errado(certo)), "codigo_invalido");
        }

        (await LerAsync(paulo, $"{Rota}/{id}")).GetProperty("situacao").GetString().Should().Be("cancelada");
        await ConferirRecusaAsync(await ConfirmarAsync(paulo, certo), "codigo_invalido");
        (await ListarGestoresAtivosAsync()).Should().BeEmpty();
    }

    [Fact(DisplayName = "Retry com a mesma chave depois de código errado reproduz a recusa e não consome outra tentativa")]
    public async Task Confirmar_CodigoErradoComARepeticaoDaChave_NaoConsomeOutraTentativa()
    {
        await ResetarAsync();
        using HttpClient paulo = Cliente(Paulo);
        Guid id = await ChegarAoCodigoEnviadoAsync(paulo);
        string errado = Errado(await UltimoCodigoAsync(Paulo));
        string chave = Guid.NewGuid().ToString();
        await ConferirRecusaAsync(await ConfirmarAsync(paulo, errado, chave), "codigo_invalido");

        using HttpResponseMessage repeticao = await ConfirmarAsync(paulo, errado, chave);

        await ConferirRecusaAsync(repeticao, "codigo_invalido");
        repeticao.Headers.Contains("Idempotency-Replayed").Should().BeTrue();
        (await LerAsync(paulo, $"{Rota}/{id}")).GetProperty("tentativasRestantes").GetInt32()
            .Should().Be(Instalacao.MaximoDeTentativas - 1);
    }

    [Fact(DisplayName = "Tentativas erradas simultâneas do mesmo executor são todas consumidas e registradas")]
    public async Task Confirmar_TentativasSimultaneas_ConsomemTodas()
    {
        await ResetarAsync();
        using HttpClient paulo = Cliente(Paulo);
        Guid id = await ChegarAoCodigoEnviadoAsync(paulo);
        string errado = Errado(await UltimoCodigoAsync(Paulo));

        HttpResponseMessage[] respostas = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => ConfirmarAsync(paulo, errado)));
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

    private static string Errado(string certo) => certo == "00000000" ? "11111111" : "00000000";

    private static Task<HttpResponseMessage> ConfirmarAsync(HttpClient executor, string codigo, string? chave = null) =>
        EnviarAsync(executor, HttpMethod.Post, $"{Rota}/confirmacao", new { codigo }, chave);
}
