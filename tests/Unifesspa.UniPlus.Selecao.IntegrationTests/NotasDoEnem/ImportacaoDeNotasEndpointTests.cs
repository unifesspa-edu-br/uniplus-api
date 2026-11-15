namespace Unifesspa.UniPlus.Selecao.IntegrationTests.NotasDoEnem;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Outbox.Cascading;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.NotasDoEnem.ApoioDaImportacaoDeNotasDeTeste;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.NotasDoEnem.ProcessoDoEnemPublicadoDeTeste;
using static Unifesspa.UniPlus.Testes.Compartilhado.ArquivoDoInepSintetico;

/// <summary>
/// A importação do retorno do INEP pela API, sobre um certame publicado de verdade e inscrições
/// reais gravadas: os lotes, a substituição por inteiro, a recusa do arquivo fora do leiaute sem
/// gravar nada, a recusa sem autorização e os três 413.
/// </summary>
[Collection(CascadingCollection.Name)]
public sealed class ImportacaoDeNotasEndpointTests
{
    private readonly CascadingFixture _fixture;
    private readonly ApoioDaImportacaoDeNotasDeTeste _apoio;

    public ImportacaoDeNotasEndpointTests(CascadingFixture fixture)
    {
        _fixture = fixture;
        _apoio = new ApoioDaImportacaoDeNotasDeTeste(fixture);
    }

    [Fact(DisplayName = "sem token é 401 e o administrador da plataforma sem concessão é 404")]
    public async Task Importar_SemAutorizacao_Recusa()
    {
        Cenario cenario = await _apoio.NovoCenarioAsync(0);
        using HttpClient client = _fixture.Factory.CreateClient();

        using MultipartFormDataContent a = Formulario(Arquivo(0));
        using HttpRequestMessage semToken = Requisicao(HttpMethod.Post, cenario.Base, a, papel: null);
        using MultipartFormDataContent b = Formulario(Arquivo(0));
        using HttpRequestMessage semConcessao = Requisicao(HttpMethod.Post, cenario.Base, b, papel: "plataforma-admin");
        semConcessao.Headers.TryAddWithoutValidation(TestAuthHandler.UserIdHeader, "diogo.souza");

        (await client.SendAsync(semToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.SendAsync(semConcessao)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "fluxo completo: importa, consulta, reimporta o vigente, substitui por inteiro e volta a um arquivo substituído")]
    public async Task Importar_FluxoCompleto()
    {
        Cenario cenario = await _apoio.NovoCenarioAsync(0, 1, 2, 3);
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

    [Fact(DisplayName = "arquivo fora do leiaute, com situação desconhecida, vazio ou de outro tipo é recusado e nenhum lote é criado")]
    public async Task Importar_ArquivoInvalido_NaoCriaLote()
    {
        Cenario cenario = await _apoio.NovoCenarioAsync(0);
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

    [Fact(DisplayName = "com as inscrições abertas a importação é 422 e nenhum lote é criado")]
    public async Task Importar_InscricoesAbertas_NaoCriaLote()
    {
        Cenario cenario = await _apoio.NovoCenarioAsync([0], semSegundaOpcao: [], fimDasInscricoes: new DateTimeOffset(2099, 12, 20, 15, 0, 0, TimeSpan.Zero));
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await ImportarAsync(client, cenario, Arquivo(0));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodigoAsync(resposta)).Should().Be("uniplus.selecao.notas_do_enem.inscricoes_em_andamento");
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("20/12/2099 às 12h00");
        (await QuantidadeDeLotesAsync(client, cenario)).Should().Be(0);
    }

    [Fact(DisplayName = "arquivo com BOM e fim de linha CRLF é aceito")]
    public async Task Importar_BomECrlf_Aceito()
    {
        Cenario cenario = await _apoio.NovoCenarioAsync(0, 1);
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
        Cenario cenario = await _apoio.NovoCenarioAsync(0, 1);
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
        Cenario cenario = await _apoio.NovoCenarioAsync(0);
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

    [Fact(DisplayName = "o mesmo arquivo sob outra versão do edital cria lote novo com a versão divulgada; sob a mesma, devolve o existente")]
    public async Task Importar_MesmoArquivoSobOutraVersao_CriaLoteNovo()
    {
        Cenario cenario = await _apoio.NovoCenarioAsync(0);
        using HttpClient client = _fixture.Factory.CreateClient();
        byte[] arquivo = Bytes([Linha(Pessoas[0], 1)]);
        Guid versaoDivulgada;
        await using (AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
            VersaoConfiguracao divulgada = await db.VersoesConfiguracao.AsNoTracking().SingleAsync(v => v.ProcessoSeletivoId == cenario.Processo.ProcessoId);
            versaoDivulgada = divulgada.Id;
            VersaoConfiguracao outra = VersaoConfiguracao.Suceder(
                divulgada, System.Text.Encoding.UTF8.GetBytes("{}"), "1", "sha256@v1", Guid.CreateVersion7(), new string('a', 64),
                divulgada.AtoCriadorId, "integration-test-user", DateTimeOffset.UtcNow);
            db.VersoesConfiguracao.Add(outra);
            db.LotesDeNotasDoEnem.Add(LoteDeNotasDoEnem.Registrar(
                cenario.Processo.ProcessoId, cenario.Processo.EtapaDoEnemId, Edicao, "INEP_ENEM_RETORNO", "v1", new string('b', 64),
                Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(arquivo)), "conta-admin", DateTimeOffset.UtcNow, 0,
                outra.Id, new Unifesspa.UniPlus.Selecao.Domain.Services.ResultadoDoCruzamento([], [], [], []), []));
            await db.SaveChangesAsync();
        }

        HttpResponseMessage sobOutraVersao = await ImportarAsync(client, cenario, arquivo);
        HttpResponseMessage sobAMesma = await ImportarAsync(client, cenario, arquivo);

        sobOutraVersao.StatusCode.Should().Be(HttpStatusCode.Created, await sobOutraVersao.Content.ReadAsStringAsync());
        Guid novo = await IdDoLoteAsync(sobOutraVersao);
        sobAMesma.StatusCode.Should().Be(HttpStatusCode.OK);
        (await IdDoLoteAsync(sobAMesma)).Should().Be(novo);
        await using AsyncServiceScope leitura = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext contexto = leitura.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (await contexto.LotesDeNotasDoEnem.AsNoTracking().SingleAsync(l => l.Id == novo)).VersaoConfiguracaoId.Should().Be(versaoDivulgada);
    }

    [Fact(DisplayName = "inscrição que concorre a curso fora do edital divulgado recusa o lote inteiro, sem gravar nada")]
    public async Task Importar_OfertaForaDoEdital_RecusaOLote()
    {
        Cenario cenario = await _apoio.NovoCenarioAsync(0);
        await using (AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            await InscricoesDoEnemDeTeste.NovaInscricaoAsync(
                scope.ServiceProvider.GetRequiredService<UniPlusDbContext>(),
                scope.ServiceProvider.GetRequiredService<IUniPlusEncryptionService>(),
                scope.ServiceProvider.GetRequiredService<IUniPlusBlindIndexService>(),
                cenario.Processo.ProcessoId, Pessoas[1], SituacaoInscricao.Submetida, Edicao,
                ofertaCursoId: cenario.Processo.OfertaTecnologicaId, ofertaCursoSegundaOpcaoId: Guid.CreateVersion7());
        }

        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await ImportarAsync(client, cenario, Bytes([Linha(Pessoas[0], 1), Linha(Pessoas[1], 2)]));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodigoAsync(resposta)).Should().Be("uniplus.selecao.notas_do_enem.oferta_de_inscricao_fora_do_edital");
        (await QuantidadeDeLotesAsync(client, cenario)).Should().Be(0);
    }

    [Fact(DisplayName = "arquivo com prova ausente e nota diferente de zero é recusado e nenhum lote é criado")]
    public async Task Importar_AusenteComNota_NaoCriaLote()
    {
        Cenario cenario = await _apoio.NovoCenarioAsync(0);
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await ImportarAsync(client, cenario, Bytes([LinhaComMatematicaAusenteEComNota(Pessoas[0], 1)]));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodigoAsync(resposta)).Should().Be("uniplus.selecao.notas_do_enem.arquivo_fora_do_leiaute");
        (await QuantidadeDeLotesAsync(client, cenario)).Should().Be(0);
    }
}
