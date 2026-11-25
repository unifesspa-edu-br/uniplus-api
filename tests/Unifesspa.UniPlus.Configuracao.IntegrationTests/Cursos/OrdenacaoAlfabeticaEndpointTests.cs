namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Cursos;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Pagination;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.Cursos.ApoioDaOrdenacaoAlfabetica;

/// <summary>
/// A ordem alfabética das listagens de Curso e de Oferta de Curso, exercitada
/// por HTTP: a ordem vale para a coleção inteira e não para cada página, sobrevive
/// à travessia nos dois sentidos, e não depende de acentuação nem de caixa.
/// </summary>
/// <remarks>
/// Os nomes usados aqui compartilham um prefixo sorteado por teste, que os agrupa
/// num bloco contíguo da coleção. A tabela é estática dentro da collection e
/// acumula cursos de outros testes; sem o bloco, as linhas alheias se
/// intercalariam e nenhuma sequência esperada seria estável.
/// </remarks>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class OrdenacaoAlfabeticaEndpointTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;
    private readonly ApoioDaOrdenacaoAlfabetica _apoio;

    public OrdenacaoAlfabeticaEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
        _apoio = new ApoioDaOrdenacaoAlfabetica(fixture);
    }

    [Fact(DisplayName = "Cursos saem em ordem alfabética de nome, e não na ordem em que foram cadastrados")]
    public async Task Cursos_SemOrdenacaoPedida_SaemEmOrdemAlfabetica()
    {
        string bloco = Bloco();
        using HttpClient client = _fixture.Factory.CreateClient();

        await CriarCursosAsync(client, bloco, ["Zoologia", "Matematica", "Biologia", "Agronomia"]);

        IReadOnlyList<string> nomes = await ListarNomesDoBlocoAsync(client, Cursos, bloco);

        nomes.Should().Equal(
            $"{bloco} Agronomia",
            $"{bloco} Biologia",
            $"{bloco} Matematica",
            $"{bloco} Zoologia");
    }

    [Fact(DisplayName = "Acento e caixa não mudam a posição: a comparação é sobre o nome normalizado")]
    public async Task Cursos_ComAcentoECaixa_OrdenamComoSeNormalizados()
    {
        string bloco = Bloco();
        using HttpClient client = _fixture.Factory.CreateClient();

        await CriarCursosAsync(client, bloco, ["Zootecnia", "Ástrofisica", "biologia", "Ciências Contábeis"]);

        IReadOnlyList<string> nomes = await ListarNomesDoBlocoAsync(client, Cursos, bloco);

        nomes.Should().Equal(
            $"{bloco} Ástrofisica",
            $"{bloco} biologia",
            $"{bloco} Ciências Contábeis",
            $"{bloco} Zootecnia");
    }

    [Fact(DisplayName = "Cursos de mesmo nome são desempatados pelo código")]
    public async Task Cursos_MesmoNome_DesempatamPeloCodigo()
    {
        string bloco = Bloco();
        string nome = $"{bloco} Homonimo";
        using HttpClient client = _fixture.Factory.CreateClient();

        await CriarCursoAsync(client, nome, codigo: $"{bloco}_C");
        await CriarCursoAsync(client, nome, codigo: $"{bloco}_A");
        await CriarCursoAsync(client, nome, codigo: $"{bloco}_B");

        IReadOnlyList<JsonElement> itens = await ListarDoBlocoAsync(client, Cursos, bloco);

        itens.Select(i => i.GetProperty("codigo").GetString())
            .Should().Equal($"{bloco}_A", $"{bloco}_B", $"{bloco}_C");
    }
}
