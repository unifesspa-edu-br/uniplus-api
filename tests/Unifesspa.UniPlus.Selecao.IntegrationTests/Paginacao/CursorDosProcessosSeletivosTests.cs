namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Paginacao;

using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Paginacao;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Paginacao.ApoioDosCursoresDeSelecao;

/// <summary>
/// O cursor da lista de processos seletivos fica preso ao conjunto de unidades em que quem consulta configura processos:
/// outro conjunto recusa, outra conta com o mesmo conjunto e quem não se identificou continuam.
/// </summary>
[Collection(CascadingCollection.Name)]
public sealed class CursorDosProcessosSeletivosTests : IAsyncLifetime
{
    private const string Rota = "/api/selecao/processos-seletivos";

    private readonly ApoioDosCursoresDeSelecao _apoio;

    public CursorDosProcessosSeletivosTests(CascadingFixture fixture) => _apoio = new ApoioDosCursoresDeSelecao(fixture);

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => _apoio.RemoverSemeadosAsync();

    [Fact(DisplayName = "Outro conjunto de unidades recusa o cursor da lista")]
    public async Task Continuar_OutroConjuntoDeUnidades_Recusa()
    {
        string quemConsulta = NomeUnico("conta.a.");
        await _apoio.ConcederConfiguracaoAsync(quemConsulta, UnidadeCeps);
        string comMaisUnidades = NomeUnico("conta.b.");
        await _apoio.ConcederConfiguracaoAsync(comMaisUnidades, UnidadeCeps, UnidadeCga);
        using ProvaDoCursor prova = await ProvaAsync(quemConsulta);

        string proximo = await prova.ProximoAsync(string.Empty);

        using HttpClient outra = _apoio.Cliente(comMaisUnidades);
        using HttpResponseMessage resposta = await outra.GetAsync(new Uri(proximo, UriKind.RelativeOrAbsolute));
        await RecusaDeCursor.DeOutraConsulta.ConferirAsync(resposta);
    }

    [Fact(DisplayName = "Outra conta com o mesmo conjunto de unidades continua o cursor da lista")]
    public async Task Continuar_OutraContaComOMesmoConjunto_Continua()
    {
        string quemConsulta = NomeUnico("conta.a.");
        string outraConta = NomeUnico("conta.b.");
        await _apoio.ConcederConfiguracaoAsync(quemConsulta, UnidadeCeps, UnidadeCga);
        await _apoio.ConcederConfiguracaoAsync(outraConta, UnidadeCga, UnidadeCeps);
        using ProvaDoCursor prova = await ProvaAsync(quemConsulta);

        string proximo = await prova.ProximoAsync(string.Empty);

        using HttpClient outra = _apoio.Cliente(outraConta);
        using HttpResponseMessage resposta = await outra.GetAsync(new Uri(proximo, UriKind.RelativeOrAbsolute));
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "Quem não se identificou continua o próprio cursor, e o de quem configura em uma unidade o recusa")]
    public async Task Continuar_SemAutenticacao_ContinuaOProprioCursor()
    {
        string quemConsulta = NomeUnico("conta.a.");
        await _apoio.ConcederConfiguracaoAsync(quemConsulta, UnidadeCeps);
        using ProvaDoCursor anonima = await ProvaAsync(null);
        using ProvaDoCursor identificada = await ProvaAsync(quemConsulta);

        await anonima.ContinuaAsync(string.Empty, static link => link);
        await identificada.ConfereRecusaPorDivergenciaAsync(await anonima.ProximoAsync(string.Empty));
    }

    [Fact(DisplayName = "O cursor da lista não leva a conta, o emissor nem as unidades, e nenhum resumo de conta")]
    public async Task Cursor_Payload_NaoLevaAIdentificacao()
    {
        string quemConsulta = NomeUnico("conta.a.");
        await _apoio.ConcederConfiguracaoAsync(quemConsulta, UnidadeCeps);
        using ProvaDoCursor prova = await ProvaAsync(quemConsulta);

        await prova.ConferePayloadAsync(
            string.Empty, vinculadoAConta: false, quemConsulta, UnidadeCeps.ToString(), UnidadeCeps.ToString("N"));
    }

    [Fact(DisplayName = "Cursor sem a chave, de outra coleção, malformado, de outra consulta ou vencido é recusado")]
    public async Task Cursor_Adulterado_Recusa()
    {
        using ProvaDoCursor prova = await ProvaAsync(null);

        await prova.RecusaCursorAdulteradoAsync(string.Empty);
        await prova.RecusaCursorVencidoAsync(string.Empty);
    }

    [Fact(DisplayName = "O link prev continua para quem o recebeu, e o percurso inteiro não pula nem repete processo")]
    public async Task Cursor_LinkAnteriorEPercurso()
    {
        using ProvaDoCursor prova = await ProvaAsync(null);

        string anterior = await prova.AnteriorDaSegundaAsync(string.Empty);
        using HttpResponseMessage resposta = await prova.ObterAsync(anterior);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        await prova.ConfereOPercursoEquivalenteAsync(string.Empty, 1, static link => link);
    }

    private async Task<ProvaDoCursor> ProvaAsync(string? conta)
    {
        await _apoio.PublicarCertamesAsync(NomeUnico("Processo"), 3);

        return _apoio.Prova(conta is null ? _apoio.Anonimo() : _apoio.Cliente(conta), Rota);
    }
}
