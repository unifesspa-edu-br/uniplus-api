namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Concessoes;

using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Paginacao;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Paginacao;

using static Unifesspa.UniPlus.Autorizacao.IntegrationTests.Paginacao.ApoioDosCursoresDeAutorizacao;

/// <summary>
/// O cursor da lista de pedidos de concessão fica preso à conta de quem o recebeu, em todas as filas: outra conta, ou a
/// mesma conta em outro emissor, é recusada como cursor inválido, sem dizer qual conferência falhou. Na fila dos próprios
/// pedidos, o e-mail verificado também escolhe os pedidos, e trocá-lo recomeça a lista.
/// </summary>
[Collection(AutorizacaoEndpointCollection.Name)]
public sealed class CursorDosPedidosDeConcessaoTests : IAsyncLifetime
{
    private const string Solicitante = "marina.sousa";
    private const string Outra = "rafael.lima";
    private const string Gestor = "diogo.souza";
    private const string Representante = "marlene.martins";

    private readonly ApoioDosCursoresDeAutorizacao _apoio;

    public CursorDosPedidosDeConcessaoTests(AutorizacaoEndpointFixture fixture) => _apoio = new ApoioDosCursoresDeAutorizacao(fixture);

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => _apoio.RemoverSemeadosAsync();

    [Theory(DisplayName = "O cursor de cada fila continua para a mesma conta e emissor")]
    [InlineData("meus")]
    [InlineData("gestor")]
    [InlineData("protecao_de_dados")]
    [InlineData("todos")]
    public async Task Cursor_MesmaContaEEmissor_Continua(string fila)
    {
        (string conta, string proximo) = await ProximaPaginaDaFilaAsync(fila);

        using HttpClient cliente = await ClienteDaFilaAsync(fila, conta);
        using HttpResponseMessage resposta = await cliente.GetAsync(new Uri(proximo, UriKind.RelativeOrAbsolute));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
    }

    [Theory(DisplayName = "O cursor de cada fila é recusado como inválido quando outra conta o apresenta")]
    [InlineData("meus")]
    [InlineData("gestor")]
    [InlineData("protecao_de_dados")]
    [InlineData("todos")]
    public async Task Cursor_OutraConta_RecusaComoInvalido(string fila)
    {
        (_, string proximo) = await ProximaPaginaDaFilaAsync(fila);

        using HttpClient cliente = _apoio.Cliente(Outra);
        using HttpResponseMessage resposta = await cliente.GetAsync(new Uri(proximo, UriKind.RelativeOrAbsolute));

        await RecusaDeCursor.Invalida.ConferirAsync(resposta);
    }

    [Theory(DisplayName = "O cursor de cada fila é recusado como inválido quando a mesma conta o apresenta com outro emissor")]
    [InlineData("meus")]
    [InlineData("gestor")]
    [InlineData("protecao_de_dados")]
    [InlineData("todos")]
    public async Task Cursor_MesmaContaEmOutroEmissor_RecusaComoInvalido(string fila)
    {
        (string conta, string proximo) = await ProximaPaginaDaFilaAsync(fila);

        using HttpClient cliente = _apoio.Cliente(conta, EmissorDeOutroProvedor);
        using HttpResponseMessage resposta = await cliente.GetAsync(new Uri(proximo, UriKind.RelativeOrAbsolute));

        await RecusaDeCursor.Invalida.ConferirAsync(resposta);
    }

    [Theory(DisplayName = "Na fila dos próprios pedidos, trocar o e-mail verificado recusa como outra consulta")]
    [InlineData("outra.caixa@unifesspa.edu.br", "true")]
    [InlineData("marina.sousa@unifesspa.edu.br", "false")]
    public async Task Cursor_MeusComOutroEmailVerificado_RecusaComoOutraConsulta(string email, string verificado)
    {
        (_, string proximo) = await ProximaPaginaDaFilaAsync("meus");

        using HttpClient cliente = _apoio.Cliente(Solicitante, null, email, verificado);
        using HttpResponseMessage resposta = await cliente.GetAsync(new Uri(proximo, UriKind.RelativeOrAbsolute));

        await RecusaDeCursor.DeOutraConsulta.ConferirAsync(resposta);
    }

    [Theory(DisplayName = "A fila omitida é a dos próprios pedidos: dizê-la, com espaços ou outra caixa, continua")]
    [InlineData("meus")]
    [InlineData("MEUS")]
    [InlineData("%20meus%20")]
    public async Task Cursor_FilaPadraoExplicita_Continua(string fila)
    {
        using ProvaDoCursor prova = await ProvaDoSolicitanteAsync();

        await prova.ContinuaComAsync(string.Empty, "fila", Uri.UnescapeDataString(fila));
    }

    [Theory(DisplayName = "Acrescentar ou trocar a fila ou a situação no meio do percurso recusa como outra consulta")]
    [InlineData("", "situacao", "AGUARDANDO_GESTOR")]
    [InlineData("situacao=AGUARDANDO_GESTOR", "situacao", "APROVADO")]
    [InlineData("fila=meus", "situacao", "AGUARDANDO_GESTOR")]
    public async Task Cursor_FiltroTrocado_RecusaComoOutraConsulta(string consulta, string parametro, string valor)
    {
        using ProvaDoCursor prova = await ProvaDoSolicitanteAsync();

        await prova.RecusaTrocaAsync(consulta, parametro, valor);
    }

    [Theory(DisplayName = "Em cada fila, acrescentar a situação no meio do percurso recusa como outra consulta")]
    [InlineData("meus")]
    [InlineData("gestor")]
    [InlineData("protecao_de_dados")]
    [InlineData("todos")]
    public async Task Cursor_SituacaoAcrescentadaEmCadaFila_RecusaComoOutraConsulta(string fila)
    {
        using ProvaDoCursor prova = await ProvaDaFilaAsync(fila);

        await prova.RecusaTrocaAsync($"fila={fila}", "situacao", "APROVADO");
    }

    [Theory(DisplayName = "Em cada fila, a fila dita com espaços e outra caixa é a mesma consulta e continua")]
    [InlineData("meus")]
    [InlineData("gestor")]
    [InlineData("protecao_de_dados")]
    [InlineData("todos")]
    public async Task Cursor_FilaComEspacosEOutraCaixa_Continua(string fila)
    {
        ArgumentNullException.ThrowIfNull(fila);
        using ProvaDoCursor prova = await ProvaDaFilaAsync(fila);

        await prova.ContinuaComAsync($"fila={fila}", "fila", $" {fila.ToUpperInvariant()} ");
    }

    [Theory(DisplayName = "Em cada fila, a coluna do instante de criação que não é um instante é recusada como outra consulta")]
    [InlineData("meus")]
    [InlineData("gestor")]
    [InlineData("protecao_de_dados")]
    [InlineData("todos")]
    public async Task Cursor_ColunaTrocadaEmCadaFila_RecusaComoOutraConsulta(string fila)
    {
        using ProvaDoCursor prova = await ProvaDaFilaAsync(fila);

        await prova.RecusaColunaComCodigoDesconhecidoAsync($"fila={fila}", 0, "não é um instante");
    }

    [Theory(DisplayName = "Em cada fila, o cursor adulterado ou vencido é recusado")]
    [InlineData("meus")]
    [InlineData("gestor")]
    [InlineData("protecao_de_dados")]
    [InlineData("todos")]
    public async Task Cursor_AdulteradoEmCadaFila_Recusa(string fila)
    {
        using ProvaDoCursor prova = await ProvaDaFilaAsync(fila);

        await prova.RecusaCursorAdulteradoAsync($"fila={fila}");
        await prova.RecusaCursorVencidoAsync($"fila={fila}");
    }

    [Fact(DisplayName = "Trocar a fila do gestor de gestor para todos recusa como outra consulta")]
    public async Task Cursor_FilaTrocada_RecusaComoOutraConsulta()
    {
        await _apoio.SemearPedidosParaSiMesmaAsync(Solicitante, 3);
        using ProvaDoCursor prova = new(new ConsultaPaginada(await ClienteDaFilaAsync("gestor", Gestor), RotaDosPedidos), _apoio.Cursores);

        await prova.RecusaTrocaAsync("fila=gestor", "fila", "todos");
    }

    [Theory(DisplayName = "Filtro inválido com o cursor de outra consulta é recusado pelo filtro, não pelo cursor")]
    [InlineData("situacao", "desconhecida", "uniplus.autorizacao.pedido.filtro_de_situacao_invalido")]
    [InlineData("fila", "outra", "uniplus.autorizacao.pedido.fila_invalida")]
    public async Task Cursor_FiltroInvalido_RecusaPeloFiltro(string parametro, string valor, string codigo)
    {
        using ProvaDoCursor prova = await ProvaDoSolicitanteAsync();

        await prova.RecusaPeloFiltroAsync(string.Empty, parametro, valor, HttpStatusCode.BadRequest, codigo);
    }

    [Fact(DisplayName = "O cursor dos pedidos leva só o resumo da conta, sem conta, e-mail nem emissor")]
    public async Task Cursor_Payload_NaoLevaAIdentificacao()
    {
        using ProvaDoCursor prova = await ProvaDoSolicitanteAsync();

        await prova.ConferePayloadAsync(
            string.Empty, vinculadoAConta: true, Solicitante, EmailDe(Solicitante), TestAuthHandler.DefaultIssuer);
    }

    [Fact(DisplayName = "O cursor dos pedidos sem a chave, de outra coleção, malformado ou de outra consulta é recusado")]
    public async Task Cursor_Adulterado_Recusa()
    {
        using ProvaDoCursor prova = await ProvaDoSolicitanteAsync();

        await prova.RecusaCursorAdulteradoAsync(string.Empty);
    }

    [Fact(DisplayName = "O cursor vencido dos pedidos responde 410")]
    public async Task Cursor_Vencido_Responde410()
    {
        using ProvaDoCursor prova = await ProvaDoSolicitanteAsync();

        await prova.RecusaCursorVencidoAsync(string.Empty);
    }

    [Fact(DisplayName = "A conferência vale também no link prev dos pedidos")]
    public async Task Cursor_LinkAnterior_ConfereAConsulta()
    {
        using ProvaDoCursor prova = await ProvaDoSolicitanteAsync();

        await prova.ConfereOLinkAnteriorAsync(string.Empty, static link => LinkDePaginacao.ComParametro(link, "situacao", "AGUARDANDO_GESTOR"));
    }

    [Fact(DisplayName = "O percurso inteiro dizendo a fila padrão a cada página não pula nem repete pedido")]
    public async Task Percorrer_ComAFilaPadraoDita_NaoPulaNemRepete()
    {
        using ProvaDoCursor prova = await ProvaDoSolicitanteAsync();

        await prova.ConfereOPercursoEquivalenteAsync(string.Empty, 1, static link => LinkDePaginacao.ComParametro(link, "fila", "meus"));
    }

    // Três pedidos da conta e página de um item: a primeira página emite a seguinte.
    private async Task<(string Conta, string Proximo)> ProximaPaginaDaFilaAsync(string fila)
    {
        await PrepararAFilaAsync(fila);

        string conta = ContaDaFila(fila);
        using ConsultaPaginada consulta = new(await ClienteDaFilaAsync(fila, conta), RotaDosPedidos);

        return (conta, await consulta.ProximoAsync($"fila={fila}"));
    }

    private async Task PrepararAFilaAsync(string fila)
    {
        await _apoio.SemearPedidosParaSiMesmaAsync(Solicitante, 3);
        if (fila == "protecao_de_dados")
        {
            await _apoio.AguardarAProtecaoDeDadosAsync(Solicitante);
        }
    }

    private async Task<ProvaDoCursor> ProvaDaFilaAsync(string fila)
    {
        await PrepararAFilaAsync(fila);
        HttpClient cliente = await ClienteDaFilaAsync(fila, ContaDaFila(fila));

        return new ProvaDoCursor(new ConsultaPaginada(cliente, RotaDosPedidos), _apoio.Cursores);
    }

    private async Task<ProvaDoCursor> ProvaDoSolicitanteAsync()
    {
        await _apoio.SemearPedidosParaSiMesmaAsync(Solicitante, 3);

        return new ProvaDoCursor(new ConsultaPaginada(_apoio.Cliente(Solicitante), RotaDosPedidos), _apoio.Cursores);
    }

    private static string ContaDaFila(string fila) => fila switch
    {
        "meus" => Solicitante,
        "protecao_de_dados" => Representante,
        _ => Gestor,
    };

    private Task<HttpClient> ClienteDaFilaAsync(string fila, string conta) => fila switch
    {
        "meus" => Task.FromResult(_apoio.Cliente(conta)),
        "protecao_de_dados" => _apoio.ClienteDeGovernancaAsync(conta, TipoDeGovernanca.RepresentanteDoControlador),
        _ => _apoio.ClienteDeGovernancaAsync(conta, TipoDeGovernanca.GestorDeAcessos),
    };
}
