namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.FatosCandidato;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// Contrato HTTP da manutenção do catálogo de fatos pela permissão de manter
/// (UNI-REQ-0143, ADR-0136): valores de domínio, derivado, agregado e alteração da definição.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class FatoCandidatoAdminDefinicaoEndpointTests(ConfiguracaoEndpointFixture fixture) : FatoCandidatoAdminEndpointTestBase(fixture)
{
    [Fact(DisplayName = "Valor de domínio é acrescentado e desativado, nunca removido; desativar de novo é recusado")]
    public async Task ValoresDeDominio()
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await CadastrarAsync(client, FatoCategorico());
        await AcrescentarValorAsync(client, id, "EJA", "Educação de jovens e adultos", 0);
        await ConferirAsync(client, HttpMethod.Delete, $"{Base}/{id}/valores/EJA", corpo: null, HttpStatusCode.NoContent);
        await ConferirAsync(client, HttpMethod.Delete, $"{Base}/{id}/valores/EJA", corpo: null, HttpStatusCode.UnprocessableEntity);

        using JsonDocument fato = await ObterAsync(client, id);
        JsonElement valor = Em(fato, "definicao", "valores", "declarados").EnumerateArray().Single();
        valor.GetProperty("codigo").GetString().Should().Be("EJA");
        valor.GetProperty("ativo").GetBoolean().Should().BeFalse();
    }

    [Fact(DisplayName = "Valor é corrigido e reordenado pelos PUTs; o código não muda")]
    public async Task CorrecaoEReordenacaoDosValores()
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await CadastrarAsync(client, FatoCategorico());
        await AcrescentarValorAsync(client, id, "EJA", "EJA", 0);
        await AcrescentarValorAsync(client, id, "REGULAR", "Regular", 1);
        await ConferirAsync(client, HttpMethod.Put, $"{Base}/{id}/valores/EJA", new { descricao = "Educação de jovens e adultos", orientacao = "Cursou o supletivo." }, HttpStatusCode.NoContent);
        await ConferirAsync(client, HttpMethod.Put, $"{Base}/{id}/ordem-dos-valores", new { codigos = new[] { "REGULAR", "EJA" } }, HttpStatusCode.NoContent);

        using JsonDocument fato = await ObterAsync(client, id);
        JsonElement[] valores = [.. Em(fato, "definicao", "valores", "declarados").EnumerateArray().OrderBy(static v => v.GetProperty("ordem").GetInt32())];
        valores.Select(static v => v.GetProperty("codigo").GetString()).Should().Equal("REGULAR", "EJA");
        valores[1].GetProperty("descricao").GetString().Should().Be("Educação de jovens e adultos");
        valores[1].GetProperty("orientacao").GetString().Should().Be("Cursou o supletivo.");
    }

    [Fact(DisplayName = "A reordenação com lista incompleta ou ausente e a correção de valor inexistente são recusadas")]
    public async Task ReordenacaoECorrecaoInvalidas()
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await CadastrarAsync(client, FatoCategorico());
        await AcrescentarValorAsync(client, id, "EJA", "EJA", 0);
        await AcrescentarValorAsync(client, id, "REGULAR", "Regular", 1);

        await ConferirAsync(client, HttpMethod.Put, $"{Base}/{id}/ordem-dos-valores", new { codigos = new[] { "REGULAR" } }, HttpStatusCode.UnprocessableEntity);
        await ConferirAsync(client, HttpMethod.Put, $"{Base}/{id}/ordem-dos-valores", new { codigos = (string[]?)null }, HttpStatusCode.BadRequest);
        await ConferirAsync(client, HttpMethod.Put, $"{Base}/{id}/valores/NAO_EXISTE", new { descricao = "Qualquer" }, HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "Derivado por regra é cadastrado, recebe regras padrão e as devolve como gravou")]
    public async Task DerivadoComRegrasPadrao()
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        string dependencia = CodigoUnico();
        await CadastrarAsync(client, FatoBooleano(dependencia));
        Guid id = await CadastrarDerivadoComValorEjaAsync(client);
        object regras = new
        {
            regras = new[] { new { contribui = "EJA", quando = new[] { new[] { new { fato = dependencia, operador = "IGUAL", valor = true } } } } },
        };
        await ConferirAsync(client, HttpMethod.Put, $"{Base}/{id}/regras-padrao", regras, HttpStatusCode.NoContent);

        using JsonDocument fato = await ObterAsync(client, id);
        Em(fato, "definicao", "cardinalidade", "codigo").GetString().Should().Be("VARIOS_VALORES");
        ConferirRegraEja(fato, dependencia);
    }

    [Fact(DisplayName = "Regras padrão sem a lista, ou com condição sem valor, são recusadas com 400 e não viram erro interno")]
    public async Task RegrasPadraoInvalidas()
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        string dependencia = CodigoUnico();
        await CadastrarAsync(client, FatoBooleano(dependencia));
        Guid id = await CadastrarDerivadoComValorEjaAsync(client);
        object semValor = new
        {
            regras = new[] { new { contribui = "EJA", quando = new[] { new[] { new { fato = dependencia, operador = "IGUAL" } } } } },
        };

        await ConferirAsync(client, HttpMethod.Put, $"{Base}/{id}/regras-padrao", new { }, HttpStatusCode.BadRequest, "corpo sem a lista não apaga as regras");
        await ConferirAsync(client, HttpMethod.Put, $"{Base}/{id}/regras-padrao", semValor, HttpStatusCode.BadRequest, "condição sem valor é recusada, não vira erro interno");
    }

    [Fact(DisplayName = "Agregado sobre fato de membro é cadastrado com o vínculo ao fato de membro")]
    public async Task AgregadoSobreFatoDeMembro()
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        string membro = CodigoUnico();
        await CadastrarAsync(client, FatoBooleanoDeMembro(membro));

        Guid id = await CadastrarAsync(client, Agregado(membro), "/agregados");

        using JsonDocument fato = await ObterAsync(client, id);
        Em(fato, "definicao", "resolucao", "binding").GetString().Should().Be($"RESUMO_DE_GRUPO:{membro}");
    }

    [Fact(DisplayName = "Agregado sobre fato do candidato é recusado")]
    public async Task AgregadoSobreFatoDoCandidato_Retorna422()
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        string doCandidato = CodigoUnico();
        await CadastrarAsync(client, FatoBooleano(doCandidato));

        await ConferirAsync(client, HttpMethod.Post, $"{Base}/agregados", Agregado(doCandidato), HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "O contrato lido pela Seleção expõe no agregado categórico os valores do fato de membro")]
    public async Task AgregadoCategorico_ReaderExpoeOsValoresDoFatoDeMembro()
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        string membro = CodigoUnico();
        Guid idDoMembro = await CadastrarAsync(client, FatoCategoricoDeMembro(membro));
        await AcrescentarValorAsync(client, idDoMembro, "RURAL", "Trabalho rural", 0);
        string agregado = CodigoUnico();
        await CadastrarAsync(client, Agregado(membro, agregado), "/agregados");

        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();
        IFatoCandidatoReader reader = escopo.ServiceProvider.GetRequiredService<IFatoCandidatoReader>();
        (await reader.ListarAsync()).Single(v => v.Codigo == agregado).ValoresDominio.Should().Equal("RURAL");
        (await reader.ObterPorCodigoAsync(agregado))!.ValoresDominio.Should().Equal("RURAL");
    }

    [Fact(DisplayName = "A definição do fato declarado é alterada pelo PUT /definicao; código, origem e vínculo ficam")]
    public async Task AlterarDefinicao_FatoDeclarado_Retorna204()
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        string codigo = CodigoUnico();
        Guid id = await CadastrarAsync(client, FatoBooleano(codigo));
        await ConferirAsync(client, HttpMethod.Put, $"{Base}/{id}/definicao", DefinicaoCategorica(), HttpStatusCode.NoContent);

        using JsonDocument fato = await ObterAsync(client, id);
        Em(fato, "definicao", "dominio", "codigo").GetString().Should().Be("OPCAO");
        Em(fato, "definicao", "valores", "fonte", "codigo").GetString().Should().Be("CATALOGO");
        Em(fato, "definicao", "resolucao", "pontoResolucao").GetString().Should().Be("INSCRICAO");
        Em(fato, "codigo").GetString().Should().Be(codigo);
        Em(fato, "definicao", "resolucao", "binding").GetString().Should().Be($"CAMPO_FORMULARIO:{codigo}");
    }

    [Fact(DisplayName = "Eixo recusado pela definição volta como 422 no campo do eixo, e o fato não muda")]
    public async Task AlterarDefinicao_EixoIncoerente_Retorna422NoCampo()
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await CadastrarAsync(client, FatoBooleano(CodigoUnico()));

        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Put, $"{Base}/{id}/definicao", DefinicaoBooleanaComFonteDeValores());

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("errors").EnumerateArray().Should().ContainSingle()
            .Which.GetProperty("field").GetString().Should().Be("fonteValores");
        using JsonDocument fato = await ObterAsync(client, id);
        Em(fato, "definicao", "dominio", "codigo").GetString().Should().Be("SIM_OU_NAO");
    }

    [Fact(DisplayName = "Alterar a definição de fato de sistema é recusado e fato inexistente é 404")]
    public async Task AlterarDefinicao_SistemaEInexistente()
    {
        using HttpClient client = Fixture.Factory.CreateClient();

        // A definição do fato de sistema é travada: COR_RACA é categórico, e o domínio não muda pela tela.
        await ConferirAsync(client, HttpMethod.Put, $"{Base}/{IdDeCorRaca}/definicao", DefinicaoBooleana(), HttpStatusCode.UnprocessableEntity);
        await ConferirAsync(client, HttpMethod.Put, $"{Base}/{Guid.NewGuid()}/definicao", DefinicaoCategorica(), HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "Alteração da definição colidindo com outra escrita do mesmo fato responde 409 e não grava")]
    public async Task AlterarDefinicao_EscritaConcorrente_Retorna409()
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await CadastrarAsync(client, FatoBooleano(CodigoUnico()));
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext concorrente = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        await using IDbContextTransaction transacao = await concorrente.Database.BeginTransactionAsync();
        int pid = await SegurarALinhaDoFatoAsync(concorrente, id);

        Task<HttpResponseMessage> alteracao = EnviarAsync(client, HttpMethod.Put, $"{Base}/{id}/definicao", DefinicaoCategorica());
        await ConcorrenciaTestHelpers.WaitForBlockedBackendAsync(Fixture.Factory, alteracao, [pid]);
        await transacao.CommitAsync();

        (await alteracao).StatusCode.Should().Be(HttpStatusCode.Conflict);
        using JsonDocument fato = await ObterAsync(client, id);
        Em(fato, "definicao", "dominio", "codigo").GetString().Should().Be("SIM_OU_NAO");
    }

    private static async Task<Guid> CadastrarDerivadoComValorEjaAsync(HttpClient client)
    {
        Guid id = await CadastrarAsync(client, DerivadoCategorico(), "/derivados");
        await AcrescentarValorAsync(client, id, "EJA", "Educação de jovens e adultos", 0);
        return id;
    }

    private static void ConferirRegraEja(JsonDocument fato, string dependencia)
    {
        JsonElement regra = Em(fato, "regrasPadrao").EnumerateArray().Single();
        regra.GetProperty("contribui").GetString().Should().Be("EJA");
        JsonElement condicao = regra.GetProperty("quando").EnumerateArray().Single().EnumerateArray().Single();
        condicao.GetProperty("fato").GetString().Should().Be(dependencia);
        condicao.GetProperty("operador").GetString().Should().Be("IGUAL");
        condicao.GetProperty("valor").GetBoolean().Should().BeTrue();
    }

    // A escrita concorrente segura a linha do fato sem commitar; a alteração lê o xmin antigo e
    // bloqueia ao gravar, e o commit torna o xmin que ela leu obsoleto.
    private static async Task<int> SegurarALinhaDoFatoAsync(UniPlusDbContext concorrente, Guid id)
    {
        await concorrente.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE configuracao.dados_do_candidato SET updated_at = now() WHERE id = {id}");
        return await ConcorrenciaTestHelpers.GetConnectionPidAsync(concorrente);
    }
}
