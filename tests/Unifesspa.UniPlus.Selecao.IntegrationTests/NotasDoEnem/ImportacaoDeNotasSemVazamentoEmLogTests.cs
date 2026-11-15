namespace Unifesspa.UniPlus.Selecao.IntegrationTests.NotasDoEnem;

using System.Net;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Outbox.Cascading;

using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.NotasDoEnem.ProcessoDoEnemPublicadoDeTeste;
using static Unifesspa.UniPlus.Testes.Compartilhado.ArquivoDoInepSintetico;

/// <summary>
/// A importação do retorno do INEP não escreve em log o CPF, a nota nem o número de inscrição de ninguém, nem na
/// importação aceita nem na recusada por linha fora do leiaute.
/// </summary>
[Collection(CascadingCollection.Name)]
public sealed class ImportacaoDeNotasSemVazamentoEmLogTests
{
    private static readonly IReadOnlyList<PersonasDeTeste.PessoaFicticia> Pessoas = PersonasDeTeste.Candidatas;

    private readonly CascadingFixture _fixture;

    public ImportacaoDeNotasSemVazamentoEmLogTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "nenhum log da importação contém CPF, nota ou número de inscrição do INEP")]
    public async Task Importar_NaoVazaPiiEmLog()
    {
        Publicado processo = await SemearEPublicarAsync(_fixture);
        await ImportacaoDeNotasDeTeste.InscreverAsync(_fixture, processo, [0, 1], semSegundaOpcao: []);
        LogsCapturados logs = new();
        await using WebApplicationFactory<HostAssemblyMarker> comLogs = ComCapturaDeLogs(logs);
        using HttpClient client = comLogs.CreateClient();

        using HttpResponseMessage invalido = await ImportacaoDeNotasDeTeste.ImportarAsync(client, processo, ArquivoComLinhaForaDoLeiaute());
        using HttpResponseMessage valido = await ImportacaoDeNotasDeTeste.ImportarAsync(client, processo, ArquivoValido());

        invalido.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        valido.StatusCode.Should().Be(HttpStatusCode.Created);
        ConferirLogsSemDadoDaImportacao(string.Join('\n', logs.Mensagens));
    }

    private WebApplicationFactory<HostAssemblyMarker> ComCapturaDeLogs(LogsCapturados logs) =>
        _fixture.Factory.WithWebHostBuilder(b => b.ConfigureTestServices(servicos => servicos.AddSingleton<ILoggerFactory>(new FabricaCapturadora(logs))));

    private static byte[] ArquivoValido() => Bytes([Linha(Pessoas[0], 1), Linha(Pessoas[1], 2), Linha(Pessoas[5], 3)]);

    private static void ConferirLogsSemDadoDaImportacao(string todos)
    {
        todos.Should().Contain("de notas do ENEM importado", "a captura precisa ter alcançado o log da importação");
        ConferirSemCpfNemInscricao(todos);
        NotasPadrao.Should().AllSatisfy(nota => NotaEmLog.Aparece(todos, nota).Should().BeFalse($"a nota {nota} não pode estar num log"));
    }

    private static byte[] ArquivoComLinhaForaDoLeiaute() => Bytes(
    [
        Linha(Pessoas[0], 1),
        Linha(Pessoas[1], 2),
        Linha(Pessoas[5], 3),
        Linha(Pessoas[6], 4).Replace("960", "96x", StringComparison.Ordinal),
    ]);

    private static void ConferirSemCpfNemInscricao(string todos)
    {
        foreach (int i in new[] { 0, 1, 5, 6 })
        {
            string cpf = Pessoas[i].Cpf;
            todos.Should().NotContain(cpf).And.NotContain(NumeroDeInscricao(i + 1));
            todos.Should().NotContain($"{cpf[..3]}.{cpf[3..6]}.{cpf[6..9]}-{cpf[9..]}");
        }
    }
}
