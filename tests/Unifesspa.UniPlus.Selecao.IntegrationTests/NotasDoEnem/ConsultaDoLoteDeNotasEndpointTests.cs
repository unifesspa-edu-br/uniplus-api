namespace Unifesspa.UniPlus.Selecao.IntegrationTests.NotasDoEnem;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Outbox.Cascading;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.NotasDoEnem.ApoioDaImportacaoDeNotasDeTeste;
using static Unifesspa.UniPlus.Testes.Compartilhado.ArquivoDoInepSintetico;

/// <summary>
/// A consulta ao lote de notas importado: a identificação mascarada de cada inscrito, da linha sem inscrito
/// e dos ausentes, sem CPF completo em nenhuma coluna, e o lote inexistente da edição.
/// </summary>
[Collection(CascadingCollection.Name)]
public sealed class ConsultaDoLoteDeNotasEndpointTests
{
    private readonly CascadingFixture _fixture;
    private readonly ApoioDaImportacaoDeNotasDeTeste _apoio;

    public ConsultaDoLoteDeNotasEndpointTests(CascadingFixture fixture)
    {
        _fixture = fixture;
        _apoio = new ApoioDaImportacaoDeNotasDeTeste(fixture);
    }

    private static string Mascara(PersonasDeTeste.PessoaFicticia pessoa) =>
        $"***.{pessoa.Cpf[3..6]}.{pessoa.Cpf[6..9]}-**";

    [Fact(DisplayName = "o lote identifica cada inscrito, a linha sem inscrito e os ausentes, e nenhuma coluna guarda CPF completo")]
    public async Task Obter_IdentificaInscritosLinhasEAusentes_SemCpfCompleto()
    {
        // 0: ausente em Matemática; 1: ausente total; 2: com nota; 3: sem linha; 4: conflito e sem 2ª opção; 5: não inscrito na edição.
        Cenario cenario = await _apoio.NovoCenarioAsync([0, 1, 2, 3, 4], semSegundaOpcao: [4]);
        using HttpClient client = _fixture.Factory.CreateClient();
        string[] outraNota = ["100", "722.2", "833.3", "544.4", "960"];
        byte[] arquivo = Bytes(
        [
            LinhaComMatematicaAusente(Pessoas[0], 1),
            LinhaDeAusente(Pessoas[1], 2),
            Linha(Pessoas[2], 3),
            Linha(Pessoas[4], 4),
            Linha(Pessoas[4], 4, outraNota),
            Linha(Pessoas[5], 6),
        ]);

        HttpResponseMessage importado = await ImportarAsync(client, cenario, arquivo);
        importado.StatusCode.Should().Be(HttpStatusCode.Created, await importado.Content.ReadAsStringAsync());
        Guid loteId = await IdDoLoteAsync(importado);

        using HttpRequestMessage consulta = Requisicao(HttpMethod.Get, $"{cenario.Base}/{loteId}");
        string corpo = await (await client.SendAsync(consulta)).Content.ReadAsStringAsync();
        corpo.Should().NotContain(Pessoas[5].Cpf);
        using JsonDocument doc = JsonDocument.Parse(corpo);
        JsonElement raiz = doc.RootElement;
        raiz.GetProperty("importadoPorNome").GetString().Should().Be("Test User");

        JsonElement[] ocorrencias = [.. raiz.GetProperty("ocorrencias").EnumerateArray()];
        JsonElement semLinha = ocorrencias.Single(o => o.GetProperty("tipo").GetString() == "INSCRITO_SEM_LINHA");
        semLinha.GetProperty("nomeDoCandidato").GetString().Should().Be(Pessoas[3].Nome);
        semLinha.GetProperty("cpfMascarado").GetString().Should().Be(Mascara(Pessoas[3]));
        OpcoesDe(semLinha).Should().Equal(
            "1:Medicina (Bacharelado) — Campus Universitário de Marabá — Matutino e Vespertino",
            "2:Pedagogia (Licenciatura) — Campus Universitário de Marabá — Noturno");

        JsonElement conflito = ocorrencias.Single(o => o.GetProperty("tipo").GetString() == "CONFLITO");
        conflito.GetProperty("nomeDoCandidato").GetString().Should().Be(Pessoas[4].Nome);
        OpcoesDe(conflito).Should().Equal("1:Medicina (Bacharelado) — Campus Universitário de Marabá — Matutino e Vespertino");

        JsonElement semInscrito = ocorrencias.Single(o => o.GetProperty("tipo").GetString() == "LINHA_SEM_INSCRITO");
        semInscrito.GetProperty("cpfMascarado").GetString().Should().Be(Mascara(Pessoas[5]));
        semInscrito.GetProperty("nomeDoCandidato").GetString().Should().Be(Pessoas[5].Nome);
        semInscrito.GetProperty("sexo").GetString().Should().Be(Pessoas[5].Sexo[..1]);
        semInscrito.GetProperty("ofertas").ValueKind.Should().Be(JsonValueKind.Null);

        raiz.GetProperty("totalDeInscritosAusentes").GetInt32().Should().Be(2);
        JsonElement[] ausentes = [.. raiz.GetProperty("ausentes").EnumerateArray()];
        JsonElement parcial = ausentes.Single(a => a.GetProperty("inscricaoId").GetGuid() == cenario.Inscricoes[0]);
        parcial.GetProperty("nomeDoCandidato").GetString().Should().Be(Pessoas[0].Nome);
        parcial.GetProperty("cpfMascarado").GetString().Should().Be(Mascara(Pessoas[0]));
        parcial.GetProperty("areas").EnumerateArray().Select(a => a.GetString()).Should().Equal("MATEMATICA");
        OpcoesDe(parcial).Should().HaveCount(2);
        JsonElement total = ausentes.Single(a => a.GetProperty("inscricaoId").GetGuid() == cenario.Inscricoes[1]);
        total.GetProperty("areas").EnumerateArray().Select(a => a.GetString()).Should().HaveCount(5);

        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        string[] linhasGravadas = await db.Database
            .SqlQuery<string>($"""
                SELECT o::text AS "Value" FROM selecao.ocorrencias_do_lote_de_notas o WHERE o.lote_id = {loteId}
                UNION ALL
                SELECT l::text AS "Value" FROM selecao.lotes_de_notas_do_enem l WHERE l.id = {loteId}
                """)
            .ToArrayAsync();
        linhasGravadas.Should().NotBeEmpty();
        foreach (string gravada in linhasGravadas)
        {
            gravada.Should().NotContain(Pessoas[5].Cpf).And.NotContain(Pessoas[4].Cpf);
        }
    }

    private static string[] OpcoesDe(JsonElement elemento) =>
        [.. elemento.GetProperty("ofertas").EnumerateArray()
            .Select(o => $"{o.GetProperty("ordem").GetInt32()}:{o.GetProperty("nome").GetString()}")];

    [Fact(DisplayName = "lote inexistente da edição é 404")]
    public async Task Obter_LoteInexistente_NaoEncontrado()
    {
        Cenario cenario = await _apoio.NovoCenarioAsync(0);
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = Requisicao(HttpMethod.Get, $"{cenario.Base}/{Guid.CreateVersion7()}");

        HttpResponseMessage resposta = await client.SendAsync(request);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await CodigoAsync(resposta)).Should().Be("uniplus.selecao.notas_do_enem.lote_nao_encontrado");
    }
}
