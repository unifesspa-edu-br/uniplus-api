namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

/// <summary>
/// A regra de homologação das inscrições que a fase declara no cronograma, pelo HTTP e contra Postgres real: a regra
/// declarada é gravada e devolvida na leitura do processo, a fase que não a devolve a perde, e as recusas saem em
/// <c>errors[]</c> acumuladas, cada uma no campo da regra que a causou. O vocabulário de fatos é do cadastro de outro
/// módulo, então só a costura inteira prova a recusa do enquadramento com fato que ele não tem.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "OutboxCascading")]
public sealed class RegraDeHomologacaoNoCronogramaEndpointTests
{
    private const string CodigoDaFase = "HOMOLOGACAO_RESULTADO_FINAL";
    private const string TipoDaCondicao = "RESULTADO_NO_RETORNO_DO_INEP";
    private const string ResultadoDaFase = DadosDoAtoDeTeste.TipoResultadoDaClassificacao;
    private const string CampoDaCondicao = "fases[0].regraDeHomologacao.alternativas[0].condicoes[0]";
    private static readonly DateTimeOffset Inicio = new(2027, 2, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Fim = new(2027, 3, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly CascadingFixture _fixture;

    public RegraDeHomologacaoNoCronogramaEndpointTests(CascadingFixture ambienteDeTeste) => _fixture = ambienteDeTeste;

    [Fact(DisplayName = "A regra declarada é gravada com a fase e a leitura do processo a devolve com o resultado e a condição")]
    public async Task RegraDeclarada_GravaEDevolveNaLeitura()
    {
        Guid processoId = await SemearAsync();

        HttpResponseMessage resposta = await PutAsync(processoId, Regra(Condicao()));

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        RegraDeHomologacaoDto? lida = (await LerFaseAsync(processoId)).Publicacoes.RegraDeHomologacao;
        lida!.ResultadoAtoCodigo.Should().Be(ResultadoDaFase);
        CondicaoDeHomologacaoDto condicao = lida.Alternativas.Single().Condicoes.Single();
        (condicao.Tipo, condicao.EdicaoDoExame, condicao.MotivoDesfavoravelCodigo).Should().Be((TipoDaCondicao, 2026, "SEM_RESULTADO_INEP"));
    }

    [Fact(DisplayName = "A fase que não devolve a regra a perde: o PUT sem regra remove a que estava gravada")]
    public async Task PutSemRegra_RemoveARegraGravada()
    {
        Guid processoId = await SemearAsync();
        (await PutAsync(processoId, Regra(Condicao()))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage resposta = await PutAsync(processoId, regra: null);

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await LerFaseAsync(processoId)).Publicacoes.RegraDeHomologacao.Should().BeNull();
    }

    [Fact(DisplayName = "Tipo e edição recusados na mesma condição saem juntos em errors[], cada um no seu campo")]
    public async Task CondicaoComVariasRecusas_422ComOsCamposDaCondicao()
    {
        Guid processoId = await SemearAsync();

        HttpResponseMessage resposta = await PutAsync(processoId, Regra(Condicao(tipo: "TAXA_PAGA", edicao: 1997)));

        (await ErrosAsync(resposta)).Should().Equal(
            ($"{CampoDaCondicao}.tipo", "uniplus.selecao.homologacao_das_inscricoes.condicao_nao_disponivel"),
            ($"{CampoDaCondicao}.edicaoDoExame", "uniplus.selecao.homologacao_das_inscricoes.edicao_do_exame_invalida"));
    }

    [Fact(DisplayName = "Alternativa sem condição é recusada no campo das condições dela")]
    public async Task AlternativaSemCondicao_422NoCampoDasCondicoes()
    {
        Guid processoId = await SemearAsync();

        HttpResponseMessage resposta = await PutAsync(processoId, new { resultadoAtoCodigo = ResultadoDaFase, alternativas = new[] { new { condicoes = Array.Empty<object>() } } });

        (await ErrosAsync(resposta)).Should().Equal(
            ("fases[0].regraDeHomologacao.alternativas[0].condicoes", "uniplus.selecao.homologacao_das_inscricoes.alternativa_sem_condicao"));
    }

    [Fact(DisplayName = "Enquadramento com fato que o vocabulário não tem é recusado no campo do enquadramento")]
    public async Task EnquadramentoComFatoForaDoVocabulario_422NoCampoDoEnquadramento()
    {
        Guid processoId = await SemearAsync();
        object enquadramento = new[] { new[] { new { fato = "FATO_QUE_NAO_EXISTE", operador = "IGUAL", valor = true } } };

        HttpResponseMessage resposta = await PutAsync(processoId, Regra(Condicao(enquadramento: enquadramento)));

        (await ErrosAsync(resposta)).Should().Equal(($"{CampoDaCondicao}.enquadramento[0][0].fato", "uniplus.predicado_dnf.fato_desconhecido"));
    }

    [Fact(DisplayName = "Valor recusado é localizado sem aparecer na resposta")]
    public async Task ValorForaDosPermitidos_IndicaCampoSemExporDado()
    {
        Guid processoId = await SemearAsync();
        const string dado = "***.999.999-**";
        object enquadramento = new[] { new[] { new { fato = "SEXO", operador = "IGUAL", valor = dado } } };

        HttpResponseMessage resposta = await PutAsync(processoId, Regra(Condicao(enquadramento: enquadramento)));

        (await ErrosAsync(resposta)).Should().Equal(($"{CampoDaCondicao}.enquadramento[0][0].valor", "uniplus.predicado_dnf.valor_fora_do_dominio"));
        string corpo = await resposta.Content.ReadAsStringAsync();
        corpo.Should().NotContain(dado);
        corpo.Should().Contain("valores permitidos");
    }

    [Theory(DisplayName = "A recusa aponta a posição enviada e a propriedade inválida")]
    [InlineData("[[]]", "[0]", "uniplus.clausula_dnf.clausula_vazia")]
    [InlineData("[[null]]", "[0][0]", "uniplus.predicado_dnf.condicao_nula")]
    [InlineData("[[{\"fato\":\"\",\"operador\":\"IGUAL\",\"valor\":true}]]", "[0][0].fato", "uniplus.condicao_dnf.fato_obrigatorio")]
    [InlineData("[[{\"fato\":\"SEXO\",\"operador\":\"ALGUM_DE\",\"valor\":true}]]", "[0][0].valor", "uniplus.condicao_dnf.forma_incoerente_com_operador")]
    [InlineData("[[{\"fato\":\"SEXO\",\"operador\":\"IGUAL\",\"valor\":\"FEMININO\"}],[{\"fato\":\"SEXO\",\"operador\":\"IGUAL\",\"valor\":\"FEMININO\"},{\"fato\":\"SEXO\",\"operador\":\"INVALIDO\",\"valor\":true}]]", "[1][1].operador", "uniplus.condicao_dnf.operador_invalido")]
    public async Task EnquadramentoInvalido_LocalizaARecusa(string json, string sufixo, string codigo)
    {
        Guid processoId = await SemearAsync();
        using JsonDocument enquadramento = JsonDocument.Parse(json);

        HttpResponseMessage resposta = await PutAsync(processoId, Regra(Condicao(enquadramento: enquadramento.RootElement)));

        (await ErrosAsync(resposta)).Should().Equal(($"{CampoDaCondicao}.enquadramento{sufixo}", codigo));
    }

    [Theory]
    [InlineData("DESEJA_NOME_SOCIAL", "IGUAL", "\"recusado\"", "Informe verdadeiro ou falso.")]
    [InlineData("FAIXA_ETARIA", "IGUAL", "18.5", "Informe um número inteiro.")]
    [InlineData("SEXO", "IGUAL", "867530912345678", "Informe um texto.")]
    [InlineData("SEXO", "ALGUM_DE", "[867530912345678]", "Informe uma lista de textos.")]
    public async Task ValorComTipoInvalido_InformaTipoSemExporValor(string fato, string operador, string valor, string mensagem)
    {
        ArgumentNullException.ThrowIfNull(valor);
        Guid processoId = await SemearAsync();
        using JsonDocument entrada = JsonDocument.Parse(valor);
        object enquadramento = new[] { new[] { new { fato, operador, valor = entrada.RootElement } } };
        HttpResponseMessage resposta = await PutAsync(processoId, Regra(Condicao(tipo: "TAXA_PAGA", edicao: 1997, enquadramento: enquadramento)));
        (await ErrosAsync(resposta)).Should().Equal(
            ($"{CampoDaCondicao}.tipo", "uniplus.selecao.homologacao_das_inscricoes.condicao_nao_disponivel"),
            ($"{CampoDaCondicao}.edicaoDoExame", "uniplus.selecao.homologacao_das_inscricoes.edicao_do_exame_invalida"),
            ($"{CampoDaCondicao}.enquadramento[0][0].valor", "uniplus.predicado_dnf.valor_incompativel_com_tipo"));
        string corpo = await resposta.Content.ReadAsStringAsync();
        using JsonDocument problema = JsonDocument.Parse(corpo);
        problema.RootElement.GetProperty("errors")[2].GetProperty("message").GetString().Should().Be(mensagem);
        corpo.Should().NotContain(valor.Trim('"'));
    }

    [Fact(DisplayName = "Enquadramento vazio e outros campos são recusados na ordem da declaração")]
    public async Task EnquadramentoVazio_AcumulaComOutrasRecusas()
    {
        Guid processoId = await SemearAsync();

        HttpResponseMessage resposta = await PutAsync(processoId, Regra(Condicao(tipo: "TAXA_PAGA", edicao: 1997, enquadramento: Array.Empty<object>())));

        (await ErrosAsync(resposta)).Should().Equal(
            ($"{CampoDaCondicao}.tipo", "uniplus.selecao.homologacao_das_inscricoes.condicao_nao_disponivel"),
            ($"{CampoDaCondicao}.edicaoDoExame", "uniplus.selecao.homologacao_das_inscricoes.edicao_do_exame_invalida"),
            ($"{CampoDaCondicao}.enquadramento", "uniplus.predicado_dnf.forma_json_invalida"));
    }

    [Fact(DisplayName = "Resultado que a fase não produz é recusado no campo do resultado, e a recusa não grava a fase")]
    public async Task ResultadoQueNaoEProduto_422NoCampoDoResultado()
    {
        Guid processoId = await SemearAsync();

        HttpResponseMessage resposta = await PutAsync(processoId, new { resultadoAtoCodigo = "OUTRO_RESULTADO", alternativas = Array.Empty<object>() });

        (await ErrosAsync(resposta)).Should().Equal(
            ("fases[0].regraDeHomologacao.resultadoAtoCodigo", "uniplus.selecao.homologacao_das_inscricoes.resultado_nao_declarado"));
        (await LerProcessoAsync(processoId)).CronogramaFases.Should().BeEmpty();
    }

    private static object Regra(params object[] condicoes) =>
        new { resultadoAtoCodigo = ResultadoDaFase, alternativas = new[] { new { condicoes } } };

    private static object Condicao(string tipo = TipoDaCondicao, int edicao = 2026, object? enquadramento = null) => new
    {
        tipo,
        edicaoDoExame = edicao,
        motivoDesfavoravelCodigo = "SEM_RESULTADO_INEP",
        enquadramento,
    };

    private static void Autenticar(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
    }

    private static async Task<(string? Campo, string? Codigo)[]> ErrosAsync(HttpResponseMessage resposta)
    {
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return [.. problema.RootElement.GetProperty("errors").EnumerateArray()
            .Select(static e => (e.GetProperty("field").GetString(), e.GetProperty("code").GetString()))];
    }

    private static object Fase(Guid faseCanonicaId, object? regra) => new
    {
        ordem = 1,
        faseCanonicaId,
        inicio = Inicio,
        fim = Fim,
        produtos = new object[] { new { atoCodigo = ResultadoDaFase, papel = "DEFINITIVO" } },
        emiteParecerIndividual = false,
        bancasRequeridas = Array.Empty<object>(),
        regraRecurso = (object?)null,
        regraDeHomologacao = regra,
    };

    private async Task<HttpResponseMessage> PutAsync(Guid processoId, object? regra)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Put, new Uri($"/api/selecao/processos-seletivos/{processoId}/cronograma-fases", UriKind.Relative))
        {
            Content = JsonContent.Create(new[] { Fase(await FaseCanonicaAsync(), regra) }),
        };
        Autenticar(request);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        return await _fixture.Factory.CreateClient().SendAsync(request);
    }

    private async Task<ProcessoSeletivoDto> LerProcessoAsync(Guid processoId)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri($"/api/selecao/processos-seletivos/{processoId}", UriKind.Relative));
        Autenticar(request);
        using HttpResponseMessage resposta = await _fixture.Factory.CreateClient().SendAsync(request);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await resposta.Content.ReadFromJsonAsync<ProcessoSeletivoDto>(JsonDoContrato.Opcoes))!;
    }

    private async Task<FaseCronogramaDto> LerFaseAsync(Guid processoId) => (await LerProcessoAsync(processoId)).CronogramaFases.Single();

    private async Task<Guid> FaseCanonicaAsync()
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        List<FaseCanonica> vivas = await db.FasesCanonicas.AsNoTracking().ToListAsync();
        return vivas.Single(static fase => string.Equals(fase.Codigo.Valor, CodigoDaFase, StringComparison.Ordinal)).Id;
    }

    private async Task<Guid> SemearAsync()
    {
        await TiposDeAtoSeeder.SemearAsync(_fixture.Factory.Services);
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        return (await ProcessoSeletivoDeTeste.GravarAsync(db, $"PS regra de homologação {Guid.CreateVersion7()}")).Id;
    }
}
