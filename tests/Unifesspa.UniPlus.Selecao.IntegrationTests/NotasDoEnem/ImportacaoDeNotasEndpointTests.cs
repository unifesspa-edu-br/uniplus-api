namespace Unifesspa.UniPlus.Selecao.IntegrationTests.NotasDoEnem;

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Host;

using Outbox.Cascading;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Readers;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.NotasDoEnem.ProcessoDoEnemPublicadoDeTeste;
using static Unifesspa.UniPlus.Testes.Compartilhado.ArquivoDoInepSintetico;

/// <summary>
/// A importação do retorno do INEP pela API, sobre um certame publicado de verdade e inscrições
/// reais gravadas: os lotes, a substituição por inteiro, a recusa do arquivo fora do leiaute sem
/// gravar nada, os três 413 e a ausência de CPF, nota e número de inscrição em log.
/// </summary>
[Collection(CascadingCollection.Name)]
public sealed class ImportacaoDeNotasEndpointTests
{
    private static readonly IReadOnlyList<PersonasDeTeste.PessoaFicticia> Pessoas = PersonasDeTeste.Candidatas;

    private readonly CascadingFixture _fixture;

    public ImportacaoDeNotasEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    private sealed record Cenario(Publicado Processo, IReadOnlyDictionary<int, Guid> Inscricoes)
    {
        public string Base => $"/api/selecao/processos-seletivos/{Processo.ProcessoId}/etapas/{Processo.EtapaDoEnemId}/edicoes-do-enem/{Edicao}/lotes-de-notas";
    }

    private async Task<Cenario> NovoCenarioAsync(params int[] inscritas)
    {
        Publicado publicado = await SemearEPublicarAsync(_fixture);
        Dictionary<int, Guid> inscricoes = [];
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        foreach (int i in inscritas)
        {
            inscricoes[i] = await InscricoesDoEnemDeTeste.NovaInscricaoAsync(
                scope.ServiceProvider.GetRequiredService<SelecaoDbContext>(),
                scope.ServiceProvider.GetRequiredService<IUniPlusEncryptionService>(),
                scope.ServiceProvider.GetRequiredService<IUniPlusBlindIndexService>(),
                publicado.ProcessoId, Pessoas[i], SituacaoInscricao.Submetida, Edicao);
        }

        return new Cenario(publicado, inscricoes);
    }

    private static MultipartFormDataContent Formulario(
        byte[] arquivo, string tipo = "text/plain", string leiaute = "INEP_ENEM_RETORNO", string versao = "v1")
    {
        MultipartFormDataContent formulario = new();
        ByteArrayContent conteudo = new(arquivo);
        conteudo.Headers.ContentType = new MediaTypeHeaderValue(tipo);
        formulario.Add(conteudo, "arquivo", "retorno.txt");
        formulario.Add(new StringContent(leiaute), "leiauteCodigo");
        formulario.Add(new StringContent(versao), "leiauteVersao");
        return formulario;
    }

    private static HttpRequestMessage Requisicao(HttpMethod metodo, string url, HttpContent? corpo = null, string? papel = "plataforma-admin")
    {
        HttpRequestMessage request = new(metodo, new Uri(url, UriKind.Relative)) { Content = corpo };
        if (papel is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
            request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, papel);
        }

        return request;
    }

    private static async Task<HttpResponseMessage> ImportarAsync(HttpClient client, Cenario cenario, byte[] arquivo, string tipo = "text/plain")
    {
        using MultipartFormDataContent formulario = Formulario(arquivo, tipo);
        using HttpRequestMessage request = Requisicao(HttpMethod.Post, cenario.Base, formulario);
        return await client.SendAsync(request);
    }

    private static async Task<JsonDocument> LerAsync(HttpResponseMessage resposta) =>
        JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());

    private static async Task<string?> CodigoAsync(HttpResponseMessage resposta)
    {
        using JsonDocument doc = await LerAsync(resposta);
        return doc.RootElement.GetProperty("code").GetString();
    }

    private static async Task<int> QuantidadeDeLotesAsync(HttpClient client, Cenario cenario)
    {
        using HttpRequestMessage request = Requisicao(HttpMethod.Get, cenario.Base);
        using JsonDocument doc = await LerAsync(await client.SendAsync(request));
        return doc.RootElement.GetArrayLength();
    }

    [Fact(DisplayName = "sem token é 401 e sem plataforma-admin é 403")]
    public async Task Importar_SemAutorizacao_Recusa()
    {
        Cenario cenario = await NovoCenarioAsync(0);
        using HttpClient client = _fixture.Factory.CreateClient();

        using MultipartFormDataContent a = Formulario(Arquivo(0));
        using HttpRequestMessage semToken = Requisicao(HttpMethod.Post, cenario.Base, a, papel: null);
        using MultipartFormDataContent b = Formulario(Arquivo(0));
        using HttpRequestMessage semPapel = Requisicao(HttpMethod.Post, cenario.Base, b, papel: "candidato");

        (await client.SendAsync(semToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.SendAsync(semPapel)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static byte[] Arquivo(int pessoa) => Bytes([Linha(Pessoas[pessoa], pessoa + 1)]);

    [Fact(DisplayName = "fluxo completo: importa, consulta, reimporta o vigente, substitui por inteiro e volta a um arquivo substituído")]
    public async Task Importar_FluxoCompleto()
    {
        Cenario cenario = await NovoCenarioAsync(0, 1, 2, 3);
        using HttpClient client = _fixture.Factory.CreateClient();
        string[] outraNota = ["100", "722.2", "833.3", "544.4", "960"];
        byte[] primeiro = Bytes(
        [
            Linha(Pessoas[0], 1),
            LinhaDeAusente(Pessoas[1], 2),
            Linha(Pessoas[5], 3),
            Linha(Pessoas[3], 4),
            Linha(Pessoas[3], 4, outraNota),
        ]);
        byte[] segundo = Bytes([Linha(Pessoas[0], 1, ["700", "700", "700", "700", "700"])]);

        HttpResponseMessage criado = await ImportarAsync(client, cenario, primeiro);

        criado.StatusCode.Should().Be(HttpStatusCode.Created, await criado.Content.ReadAsStringAsync());
        criado.Headers.Location.Should().NotBeNull();
        string corpoDoPrimeiro = await criado.Content.ReadAsStringAsync();
        corpoDoPrimeiro.Should().NotContain(Pessoas[0].Cpf).And.NotContain(Pessoas[5].Cpf).And.NotContain(Pessoas[3].Cpf);
        using JsonDocument lote1 = JsonDocument.Parse(corpoDoPrimeiro);
        Guid primeiroId = lote1.RootElement.GetProperty("id").GetGuid();
        lote1.RootElement.GetProperty("totalDeLinhas").GetInt32().Should().Be(5);
        lote1.RootElement.GetProperty("totalDeInscritosComNota").GetInt32().Should().Be(2);
        lote1.RootElement.GetProperty("totalDeLancamentos").GetInt32().Should().Be(10);
        lote1.RootElement.GetProperty("totalDeInscritosSemLinha").GetInt32().Should().Be(1);
        lote1.RootElement.GetProperty("totalDeLinhasSemInscrito").GetInt32().Should().Be(1);
        lote1.RootElement.GetProperty("totalDeInscritosEmConflito").GetInt32().Should().Be(1);
        JsonElement[] ocorrencias = [.. lote1.RootElement.GetProperty("ocorrencias").EnumerateArray()];
        ocorrencias.Select(o => o.GetProperty("tipo").GetString()).Should().BeEquivalentTo(["INSCRITO_SEM_LINHA", "LINHA_SEM_INSCRITO", "CONFLITO"]);
        ocorrencias.Single(o => o.GetProperty("tipo").GetString() == "LINHA_SEM_INSCRITO").GetProperty("numerosDasLinhas")
            .EnumerateArray().Select(n => n.GetInt32()).Should().Equal(3);
        ocorrencias.Single(o => o.GetProperty("tipo").GetString() == "CONFLITO").GetProperty("numerosDasLinhas")
            .EnumerateArray().Select(n => n.GetInt32()).Should().Equal(4, 5);

        using HttpRequestMessage consulta = Requisicao(HttpMethod.Get, $"{cenario.Base}/{primeiroId}");
        HttpResponseMessage detalhe = await client.SendAsync(consulta);
        detalhe.StatusCode.Should().Be(HttpStatusCode.OK);
        using (JsonDocument doc = await LerAsync(detalhe))
        {
            doc.RootElement.GetProperty("ocorrencias").GetArrayLength().Should().Be(3);
            doc.RootElement.GetProperty("vigente").GetBoolean().Should().BeTrue();
        }

        HttpResponseMessage repetido = await ImportarAsync(client, cenario, primeiro);
        repetido.StatusCode.Should().Be(HttpStatusCode.OK);
        using (JsonDocument doc = await LerAsync(repetido))
        {
            doc.RootElement.GetProperty("id").GetGuid().Should().Be(primeiroId);
        }

        (await QuantidadeDeLotesAsync(client, cenario)).Should().Be(1);

        HttpResponseMessage substituto = await ImportarAsync(client, cenario, segundo);
        substituto.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid segundoId;
        using (JsonDocument doc = await LerAsync(substituto))
        {
            segundoId = doc.RootElement.GetProperty("id").GetGuid();
        }

        await using (AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            ILancamentosDeNotaReader leitor = scope.ServiceProvider.GetRequiredService<ILancamentosDeNotaReader>();
            IReadOnlyList<LancamentoVigente> vigentes = await leitor.ListarVigentesAsync(
                cenario.Processo.EtapaDoEnemId, [.. cenario.Inscricoes.Values]);
            vigentes.Should().HaveCount(5).And.OnlyContain(l => l.LoteId == segundoId && l.InscricaoId == cenario.Inscricoes[0] && l.Nota == 700m);
        }

        using HttpRequestMessage listaRequest = Requisicao(HttpMethod.Get, cenario.Base);
        using (JsonDocument lista = await LerAsync(await client.SendAsync(listaRequest)))
        {
            JsonElement[] lotes = [.. lista.RootElement.EnumerateArray()];
            lotes.Select(l => l.GetProperty("id").GetGuid()).Should().Equal(segundoId, primeiroId);
            lotes.Select(l => l.GetProperty("vigente").GetBoolean()).Should().Equal(true, false);
            lotes[0].TryGetProperty("ocorrencias", out JsonElement semOcorrencias).Should().BeTrue();
            semOcorrencias.ValueKind.Should().Be(JsonValueKind.Null);
        }

        HttpResponseMessage volta = await ImportarAsync(client, cenario, primeiro);
        volta.StatusCode.Should().Be(HttpStatusCode.Created, "o arquivo de um lote já substituído cria lote novo, que volta a valer");
        (await QuantidadeDeLotesAsync(client, cenario)).Should().Be(3);
    }

    [Fact(DisplayName = "lote inexistente da edição é 404")]
    public async Task Obter_LoteInexistente_NaoEncontrado()
    {
        Cenario cenario = await NovoCenarioAsync(0);
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = Requisicao(HttpMethod.Get, $"{cenario.Base}/{Guid.CreateVersion7()}");

        HttpResponseMessage resposta = await client.SendAsync(request);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await CodigoAsync(resposta)).Should().Be("uniplus.selecao.notas_do_enem.lote_nao_encontrado");
    }

    [Fact(DisplayName = "arquivo fora do leiaute, com situação desconhecida, vazio ou de outro tipo é recusado e nenhum lote é criado")]
    public async Task Importar_ArquivoInvalido_NaoCriaLote()
    {
        Cenario cenario = await NovoCenarioAsync(0);
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage foraDoLeiaute = await ImportarAsync(client, cenario, Bytes([LinhaDe23Campos(Pessoas[0], 1)]));
        HttpResponseMessage situacao = await ImportarAsync(client, cenario, Bytes([Linha(Pessoas[0], 1, situacoes: ["7", "7", "7", "7", "5"])]));
        HttpResponseMessage pdf = await ImportarAsync(client, cenario, Arquivo(0), tipo: "application/pdf");
        using MultipartFormDataContent semArquivo = new() { { new StringContent("INEP_ENEM_RETORNO"), "leiauteCodigo" }, { new StringContent("v1"), "leiauteVersao" } };
        using HttpRequestMessage semArquivoRequest = Requisicao(HttpMethod.Post, cenario.Base, semArquivo);
        HttpResponseMessage vazio = await client.SendAsync(semArquivoRequest);

        foraDoLeiaute.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodigoAsync(foraDoLeiaute)).Should().Be("uniplus.selecao.notas_do_enem.arquivo_fora_do_leiaute");
        situacao.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodigoAsync(situacao)).Should().Be("uniplus.selecao.notas_do_enem.codigo_de_situacao_desconhecido");
        pdf.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
        (await CodigoAsync(pdf)).Should().Be("uniplus.selecao.notas_do_enem.tipo_de_arquivo_nao_aceito");
        vazio.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodigoAsync(vazio)).Should().Be("uniplus.selecao.notas_do_enem.arquivo_vazio");
        (await QuantidadeDeLotesAsync(client, cenario)).Should().Be(0);
    }

    [Fact(DisplayName = "arquivo com BOM e fim de linha CRLF é aceito")]
    public async Task Importar_BomECrlf_Aceito()
    {
        Cenario cenario = await NovoCenarioAsync(0, 1);
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await ImportarAsync(
            client, cenario, Bytes([Linha(Pessoas[0], 1), Linha(Pessoas[1], 2)], "\r\n", comBom: true));

        resposta.StatusCode.Should().Be(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync());
        using JsonDocument doc = await LerAsync(resposta);
        doc.RootElement.GetProperty("totalDeInscritosComNota").GetInt32().Should().Be(2);
    }

    [Fact(DisplayName = "duas importações simultâneas do mesmo arquivo resultam em um só lote")]
    public async Task Importar_Concorrente_UmSoLote()
    {
        Cenario cenario = await NovoCenarioAsync(0, 1);
        using HttpClient client = _fixture.Factory.CreateClient();
        byte[] arquivo = Bytes([Linha(Pessoas[0], 1), Linha(Pessoas[1], 2)]);

        HttpResponseMessage[] respostas = await Task.WhenAll(
            ImportarAsync(client, cenario, arquivo), ImportarAsync(client, cenario, arquivo));

        respostas.Select(r => r.StatusCode).Should().BeEquivalentTo([HttpStatusCode.Created, HttpStatusCode.OK]);
        (await QuantidadeDeLotesAsync(client, cenario)).Should().Be(1);
    }

    private sealed class ConteudoSemTamanho(byte[] bytes) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context) =>
            stream.WriteAsync(bytes).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    [Fact(DisplayName = "413 em três tamanhos: logo acima do limite, o dobro com Content-Length e o dobro sem Content-Length")]
    public async Task Importar_AcimaDoLimite_413EmTresTamanhos()
    {
        Cenario cenario = await NovoCenarioAsync(0);
        using HttpClient client = _fixture.Factory.CreateClient();
        const int Limite = 10 * 1024 * 1024;

        HttpResponseMessage logoAcima = await ImportarAsync(client, cenario, new byte[Limite + 1]);
        HttpResponseMessage dobroComTamanho = await ImportarAsync(client, cenario, new byte[2 * Limite]);

        using MultipartFormDataContent formulario = Formulario(new byte[2 * Limite]);
        byte[] corpo = await formulario.ReadAsByteArrayAsync();
        using ConteudoSemTamanho semTamanho = new(corpo);
        semTamanho.Headers.ContentType = formulario.Headers.ContentType;
        using HttpRequestMessage chunked = Requisicao(HttpMethod.Post, cenario.Base, semTamanho);
        chunked.Headers.TransferEncodingChunked = true;
        HttpResponseMessage dobroSemTamanho = await client.SendAsync(chunked);

        foreach (HttpResponseMessage resposta in new[] { logoAcima, dobroComTamanho, dobroSemTamanho })
        {
            resposta.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
            resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
            (await CodigoAsync(resposta)).Should().Be("uniplus.selecao.notas_do_enem.arquivo_grande_demais");
        }

        (await QuantidadeDeLotesAsync(client, cenario)).Should().Be(0);
    }

    /// <summary>Substitui a fábrica de loggers do host para que todo log, de qualquer categoria, passe pela captura.</summary>
    private sealed class FabricaCapturadora(ILoggerProvider provedor) : ILoggerFactory
    {
        public void AddProvider(ILoggerProvider provider)
        {
        }

        public ILogger CreateLogger(string categoryName) => provedor.CreateLogger(categoryName);

        public void Dispose()
        {
        }
    }

    private sealed class LogsCapturados : ILoggerProvider
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<string> _mensagens = new();

        public IReadOnlyCollection<string> Mensagens => _mensagens;

        public ILogger CreateLogger(string categoryName) => new Registrador(_mensagens);

        public void Dispose()
        {
        }

        private sealed class Registrador(System.Collections.Concurrent.ConcurrentQueue<string> destino) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                destino.Enqueue(formatter(state, exception));
                if (state is IEnumerable<KeyValuePair<string, object?>> pares)
                {
                    foreach (KeyValuePair<string, object?> par in pares)
                    {
                        destino.Enqueue($"{par.Key}={par.Value}");
                    }
                }
            }
        }
    }

    [Fact(DisplayName = "nenhum log da importação contém CPF, nota ou número de inscrição do INEP")]
    public async Task Importar_NaoVazaPiiEmLog()
    {
        Cenario cenario = await NovoCenarioAsync(0, 1);
        LogsCapturados logs = new();
        await using WebApplicationFactory<HostAssemblyMarker> comLogs = _fixture.Factory
            .WithWebHostBuilder(b => b.ConfigureTestServices(servicos => servicos.AddSingleton<ILoggerFactory>(new FabricaCapturadora(logs))));
        using HttpClient client = comLogs.CreateClient();
        byte[] arquivo = Bytes(
        [
            Linha(Pessoas[0], 1),
            Linha(Pessoas[1], 2),
            Linha(Pessoas[5], 3),
            Linha(Pessoas[6], 4).Replace("960", "96x", StringComparison.Ordinal),
        ]);

        HttpResponseMessage invalido = await ImportarAsync(client, cenario, arquivo);
        HttpResponseMessage valido = await ImportarAsync(client, cenario, Bytes([Linha(Pessoas[0], 1), Linha(Pessoas[1], 2), Linha(Pessoas[5], 3)]));

        invalido.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        valido.StatusCode.Should().Be(HttpStatusCode.Created);
        string todos = string.Join('\n', logs.Mensagens);
        todos.Should().Contain("de notas do ENEM importado", "a captura precisa ter alcançado o log da importação");
        foreach (int i in new[] { 0, 1, 5, 6 })
        {
            todos.Should().NotContain(Pessoas[i].Cpf).And.NotContain(NumeroDeInscricao(i + 1));
            string cpf = Pessoas[i].Cpf;
            todos.Should().NotContain($"{cpf[..3]}.{cpf[3..6]}.{cpf[6..9]}-{cpf[9..]}");
        }

        foreach (string nota in NotasPadrao.Where(n => n.Length > 2))
        {
            todos.Should().NotContain(nota);
        }
    }
}
