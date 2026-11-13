namespace Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;
using Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>
/// A governança montada na ordem em que um ambiente novo a recebe, pelo host real: a instalação concede os dois primeiros
/// gestores de acessos (Diogo e Isabella), o primeiro Representante do controlador (Manoel) é designado só pelo gestor, o
/// segundo (a gestora Isabella, que acumula os dois papéis) passa pela segunda etapa decidida pelo primeiro, e a
/// Encarregada de dados (Betina) também. A designação cruzada do Representante como Encarregado é recusada (ADR-0162).
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class GovernancaDesignadaAposAInstalacaoTests : InstalacaoTestBase
{
    private const string Pedidos = PedidosDeConcessaoTestBase.Rota;

    // Quem pede as designações não é quem instala: os avisos dos pedidos não chegam à caixa em que os outros testes da
    // instalação esperam o código.
    private static readonly Persona Leandro = new("leandro.viana", "leandro-viana84@uniplus-test.local");

    private static readonly Guid Representante = PerfisDeSistema.RepresentanteDoControladorId;
    private static readonly Guid Encarregado = PerfisDeSistema.EncarregadoDeDadosId;

    public GovernancaDesignadaAposAInstalacaoTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "Instalados os gestores, as designações de Representante e de Encarregado concluem nessa ordem, e a cruzada é recusada")]
    public async Task GestoresRepresentanteEncarregado_ConcluemNaOrdem()
    {
        try
        {
            await DesignarNaOrdemAsync();
        }
        finally
        {
            await DescartarOQueCriouAsync();
        }
    }

    private async Task DesignarNaOrdemAsync()
    {
        await InstalarAsync();
        using HttpClient diogo = Cliente(Diogo, papel: false);
        using HttpClient manoel = Cliente(Manoel, papel: false);

        (await AprovarAsync(diogo, await DesignacaoAceitaAsync(Representante, Manoel))).Should().Be("aprovado", "o primeiro Representante só passa pelo gestor");
        (await AprovarNasDuasEtapasAsync(diogo, manoel, await DesignacaoAceitaAsync(Representante, Isabella))).Should().Be("aprovado");
        (await AprovarNasDuasEtapasAsync(diogo, manoel, await DesignacaoAceitaAsync(Encarregado, Betina))).Should().Be("aprovado");

        Guid cruzado = await DesignacaoComDocumentoAsync(Encarregado, Manoel);
        using HttpResponseMessage aceite = await TermoNoHostDeTeste.AceitarAsync(Fixture.Factory.Services, manoel, cruzado);
        await PedidosDeConcessaoTestBase.ConferirRecusaAsync(aceite, HttpStatusCode.Conflict, "papeis_incompativeis");
    }

    // A instalação de Diogo e Isabella, por Paulo, até a confirmação com o código, sobre um ambiente sem designação alguma.
    private async Task InstalarAsync()
    {
        await ResetarAsync();
        await LimparDesignacoesAsync();
        using HttpClient paulo = Cliente(Paulo);
        await ChegarAoCodigoEnviadoAsync(paulo);
        string codigo = await UltimoCodigoAsync(Paulo);

        (await EnviarAsync(paulo, HttpMethod.Post, $"{Rota}/confirmacao", new { codigo })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await TermoNoHostDeTeste.PublicarAsync(Fixture.Factory.Services, "publicada pelos gestores instalados");
    }

    // Os avisos dos pedidos saem pelo outbox depois das respostas: a caixa só se limpa quando para de receber, e as
    // designações, as concessões de Representante e de Encarregado e os gestores instalados não ficam para os outros testes.
    private async Task DescartarOQueCriouAsync()
    {
        int antes = -1;
        for (int agora = await ContarMensagensAsync(); agora != antes; agora = await ContarMensagensAsync())
        {
            antes = agora;
            await Task.Delay(TimeSpan.FromSeconds(3));
        }

        await LimparDesignacoesAsync();
        await ResetarAsync();
    }

    private async Task<int> ContarMensagensAsync()
    {
        using HttpClient mailpit = new() { BaseAddress = Fixture.ApiDoMailpit };
        return (await mailpit.GetFromJsonAsync<JsonElement>("/api/v1/messages")).GetProperty("total").GetInt32();
    }

    private async Task LimparDesignacoesAsync()
    {
        await TermoNoHostDeTeste.ReiniciarAsync(Fixture.Factory.Services);
        foreach (string tabela in new[] { "pedido_anexo_leituras", "pedido_decisoes", "pedido_anexos", "pedidos_de_concessao" })
        {
            await ExecutarSqlAsync($"DELETE FROM autorizacao.{tabela}");
        }

        await ExecutarSqlAsync("DELETE FROM autorizacao.concessoes WHERE perfil_id IN ({0}, {1})", Representante, Encarregado);
    }

    private static async Task<string> AprovarNasDuasEtapasAsync(HttpClient gestor, HttpClient representante, Guid id)
    {
        (await AprovarAsync(gestor, id)).Should().Be("aguardando_protecao_de_dados");
        return await AprovarAsync(representante, id);
    }

    private static async Task<string> AprovarAsync(HttpClient decisor, Guid id)
    {
        using HttpResponseMessage resposta = await PedidosDeConcessaoTestBase.EnviarAsync(decisor, HttpMethod.Post, $"{Pedidos}/{id}/aprovacoes");
        return (await PedidosDeConcessaoTestBase.CorpoAsync(resposta, HttpStatusCode.OK)).GetProperty("situacao").GetString()!;
    }

    // O pedido de designação feito por Leandro, com o documento verificado e aceito pelo beneficiário.
    private async Task<Guid> DesignacaoAceitaAsync(Guid perfilId, Persona beneficiario)
    {
        Guid id = await DesignacaoComDocumentoAsync(perfilId, beneficiario);
        using HttpClient cliente = Cliente(beneficiario, papel: false);
        (await TermoNoHostDeTeste.AceitarAsync(Fixture.Factory.Services, cliente, id)).StatusCode.Should().Be(HttpStatusCode.OK);

        return id;
    }

    private async Task<Guid> DesignacaoComDocumentoAsync(Guid perfilId, Persona beneficiario)
    {
        using HttpClient leandro = Cliente(Leandro, papel: false);
        using HttpResponseMessage criado = await PedidosDeConcessaoTestBase.EnviarAsync(
            leandro, HttpMethod.Post, Pedidos, new { beneficiarioEmail = beneficiario.Email, perfilId, justificativa = PedidosDeConcessaoTestBase.Justificativa });
        Guid id = (await PedidosDeConcessaoTestBase.CorpoAsync(criado, HttpStatusCode.Created)).GetProperty("id").GetGuid();
        Guid anexoId = await PedidosDeConcessaoTestBase.AnexarEmVerificacaoAsync(leandro, id);
        await InjetarResultadoAsync(anexoId, VeredictoDaVerificacao.Verificado);
        await AguardarAsync(
            async () => await PedidosDeConcessaoTestBase.SituacaoDoAnexoAsync(leandro, id, anexoId) == "verificado", $"o anexo {anexoId} não chegou a verificado");

        return id;
    }
}
