namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;
using Domain.ValueObjects;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A unidade que responde por cada fase do cronograma, pelo HTTP e contra Postgres real: o
/// cadastro de unidades é de outro módulo (ADR-0056), então só a costura inteira prova que a
/// fase guarda a cópia de id, sigla e nome, devolve-a na leitura e recusa a unidade que o
/// cadastro não tem.
/// </summary>
/// <remarks>
/// As duas fases usadas são de data definida na execução, o que dispensa a janela e deixa o
/// assunto sendo só a unidade. A fase canônica pertence a um vocabulário fechado e está
/// semeada por migration.
/// </remarks>
[Collection(CascadingCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "OutboxCascading")]
public sealed class UnidadeResponsavelPorFaseEndpointTests
{
    private const string CodigoFaseDaChamada = "CHAMADA";
    private const string CodigoFaseDaListaDeEspera = "LISTA_ESPERA";

    private readonly CascadingFixture _fixture;

    public UnidadeResponsavelPorFaseEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "Fases com unidades diferentes gravam a cópia de cada uma, e a leitura devolve id, sigla e nome por fase")]
    public async Task FasesComUnidadesDiferentes_GravamEDevolvemACopiaDeCadaUma()
    {
        Contexto ctx = await SemearAsync(nameof(FasesComUnidadesDiferentes_GravamEDevolvemACopiaDeCadaUma));
        Unidade ceps = await ctx.SemearUnidadeAsync("CEPS", "Centro de Processos Seletivos");
        Unidade cga = await ctx.SemearUnidadeAsync("CGA", "Centro de Gestão Acadêmica");

        HttpResponseMessage resposta = await ctx.PutCronogramaAsync(ceps.Id, cga.Id);

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);

        IReadOnlyList<FaseCronograma> gravadas = await ctx.ObterFasesGravadasAsync();
        gravadas[0].UnidadeResponsavelOrigemId.Should().Be(ceps.Id);
        gravadas[0].UnidadeResponsavelSigla.Should().Be(ceps.Sigla);
        gravadas[0].UnidadeResponsavelNome.Should().Be(ceps.Nome);
        gravadas[1].UnidadeResponsavelOrigemId.Should().Be(cga.Id);

        IReadOnlyList<FaseCronogramaDto> lidas = await ctx.ObterFasesLidasPelaApiAsync();
        lidas[0].UnidadeResponsavel.Should().Be(new UnidadeResponsavelDto(ceps.Id, ceps.Sigla, ceps.Nome));
        lidas[1].UnidadeResponsavel.Should().Be(new UnidadeResponsavelDto(cga.Id, cga.Sigla, cga.Nome));
    }

    [Fact(DisplayName = "O rascunho aceita fase sem unidade responsável, e a leitura a devolve nula")]
    public async Task RascunhoSemUnidadeResponsavel_AceitaELeituraDevolveNula()
    {
        Contexto ctx = await SemearAsync(nameof(RascunhoSemUnidadeResponsavel_AceitaELeituraDevolveNula));

        HttpResponseMessage resposta = await ctx.PutCronogramaAsync(null, null);

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        IReadOnlyList<FaseCronogramaDto> lidas = await ctx.ObterFasesLidasPelaApiAsync();
        lidas.Should().OnlyContain(f => f.UnidadeResponsavel == null);
    }

    [Fact(DisplayName = "Unidade que o cadastro não tem é recusada no campo da fase, com o código próprio")]
    public async Task UnidadeInexistente_Recusa422NoCampoDaFase()
    {
        Contexto ctx = await SemearAsync(nameof(UnidadeInexistente_Recusa422NoCampoDaFase));
        Unidade cga = await ctx.SemearUnidadeAsync("CGA", "Centro de Gestão Acadêmica");

        HttpResponseMessage resposta = await ctx.PutCronogramaAsync(cga.Id, Guid.CreateVersion7());

        await AfirmarRecusaDaUnidadeAsync(resposta, campo: "fases[1].unidadeResponsavelId");
        (await ctx.ObterFasesGravadasAsync()).Should().BeEmpty("a recusa não grava o cronograma");
    }

    [Fact(DisplayName = "Unidade excluída do cadastro é recusada como inexistente")]
    public async Task UnidadeExcluidaDoCadastro_Recusa422()
    {
        Contexto ctx = await SemearAsync(nameof(UnidadeExcluidaDoCadastro_Recusa422));
        Unidade extinta = await ctx.SemearUnidadeAsync("EXT", "Coordenação extinta");
        await ctx.ExcluirUnidadeAsync(extinta.Id);

        HttpResponseMessage resposta = await ctx.PutCronogramaAsync(extinta.Id, null);

        await AfirmarRecusaDaUnidadeAsync(resposta, campo: "fases[0].unidadeResponsavelId");
    }

    [Fact(DisplayName = "Mudar sigla e nome no cadastro depois de definir o cronograma não altera a cópia da fase")]
    public async Task MudancaNoCadastroDepoisDeDefinir_NaoAlteraACopiaDaFase()
    {
        Contexto ctx = await SemearAsync(nameof(MudancaNoCadastroDepoisDeDefinir_NaoAlteraACopiaDaFase));
        Unidade cga = await ctx.SemearUnidadeAsync("CGA", "Centro de Gestão Acadêmica");
        (await ctx.PutCronogramaAsync(cga.Id, null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await ctx.RenomearUnidadeAsync(cga.Id, $"{cga.Sigla}X", "Centro de Gestão Acadêmica (renomeado)");

        IReadOnlyList<FaseCronogramaDto> lidas = await ctx.ObterFasesLidasPelaApiAsync();
        lidas[0].UnidadeResponsavel.Should().Be(new UnidadeResponsavelDto(cga.Id, cga.Sigla, cga.Nome));
    }

    [Fact(DisplayName = "No rascunho, definir o cronograma de novo atualiza a cópia da unidade pelo cadastro")]
    public async Task DefinirDeNovo_NoRascunho_AtualizaACopiaDaUnidade()
    {
        Contexto ctx = await SemearAsync(nameof(DefinirDeNovo_NoRascunho_AtualizaACopiaDaUnidade));
        Unidade cga = await ctx.SemearUnidadeAsync("CGA", "Centro de Gestão Acadêmica");
        (await ctx.PutCronogramaAsync(cga.Id, null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await ctx.RenomearUnidadeAsync(cga.Id, $"{cga.Sigla}X", "Centro de Gestão Acadêmica (renomeado)");

        (await ctx.PutCronogramaAsync(cga.Id, null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        IReadOnlyList<FaseCronogramaDto> lidas = await ctx.ObterFasesLidasPelaApiAsync();
        lidas[0].UnidadeResponsavel!.Nome.Should().Be("Centro de Gestão Acadêmica (renomeado)");
        lidas[0].UnidadeResponsavel!.Sigla.Should().Be($"{cga.Sigla}X");
    }

    [Fact(DisplayName = "No rascunho, a unidade gravada que saiu do cadastro é recusada ao definir de novo")]
    public async Task DefinirDeNovo_NoRascunho_UnidadeExcluidaDepois_Recusa()
    {
        Contexto ctx = await SemearAsync(nameof(DefinirDeNovo_NoRascunho_UnidadeExcluidaDepois_Recusa));
        Unidade cga = await ctx.SemearUnidadeAsync("CGA", "Centro de Gestão Acadêmica");
        (await ctx.PutCronogramaAsync(cga.Id, null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await ctx.ExcluirUnidadeAsync(cga.Id);

        HttpResponseMessage resposta = await ctx.PutCronogramaAsync(cga.Id, null);

        await AfirmarRecusaDaUnidadeAsync(resposta, campo: "fases[0].unidadeResponsavelId");
    }

    private static async Task AfirmarRecusaDaUnidadeAsync(HttpResponseMessage resposta, string campo)
    {
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        JsonElement erro = problema.RootElement.GetProperty("errors").EnumerateArray().Should().ContainSingle().Subject;
        erro.GetProperty("field").GetString().Should().Be(campo);
        erro.GetProperty("code").GetString()
            .Should().Be("uniplus.selecao.fase_cronograma.unidade_responsavel_nao_encontrada");
    }

    private async Task<Contexto> SemearAsync(string nome)
    {
        CascadingApiFactory api = _fixture.Factory;
        HttpClient client = api.CreateClient();

        Guid chamadaId = await ObterFaseCanonicaAsync(api, CodigoFaseDaChamada);
        Guid listaDeEsperaId = await ObterFaseCanonicaAsync(api, CodigoFaseDaListaDeEspera);

        Guid processoId;
        await using (AsyncServiceScope scope = api.Services.CreateAsyncScope())
        {
            SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
            ProcessoSeletivo processo = ProcessoSeletivo.Criar(
                $"PS unidade por fase — {nome} {Guid.CreateVersion7()}",
                Unifesspa.UniPlus.Selecao.Domain.Enums.TipoProcesso.SiSU,
                Unifesspa.UniPlus.Selecao.Domain.Enums.OrigemCandidatos.ImportacaoExterna,
                Guid.CreateVersion7(),
                UnidadeAdministradoraSnapshot.Criar(
                    "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
                LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!,
                IdentificadoresDeTeste.Novo());
            await db.ProcessosSeletivos.AddAsync(processo).ConfigureAwait(false);
            await db.SaveChangesAsync().ConfigureAwait(false);
            processoId = processo.Id;
        }

        return new Contexto(api, client, processoId, chamadaId, listaDeEsperaId);
    }

    private static async Task<Guid> ObterFaseCanonicaAsync(CascadingApiFactory api, string codigo)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        ConfiguracaoDbContext config = scope.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();

        // O catálogo de fases vivas é pequeno — materializar é mais barato que traduzir a
        // comparação do value object Codigo para SQL.
        List<FaseCanonica> vivas = await config.FasesCanonicas.AsNoTracking().ToListAsync().ConfigureAwait(false);
        FaseCanonica? fase = vivas.Find(f => string.Equals(f.Codigo.Valor, codigo, StringComparison.Ordinal));

        fase.Should().NotBeNull($"'{codigo}' faz parte do catálogo canônico semeado");
        return fase!.Id;
    }

    private sealed record Contexto(
        CascadingApiFactory Api, HttpClient Client, Guid ProcessoId, Guid PrimeiraFaseCanonicaId, Guid SegundaFaseCanonicaId)
    {
        public async Task<HttpResponseMessage> PutCronogramaAsync(Guid? primeiraUnidadeId, Guid? segundaUnidadeId)
        {
            object[] fases =
            [
                Fase(1, PrimeiraFaseCanonicaId, primeiraUnidadeId),
                Fase(2, SegundaFaseCanonicaId, segundaUnidadeId),
            ];

            using HttpRequestMessage request = new(
                HttpMethod.Put,
                new Uri($"/api/selecao/processos-seletivos/{ProcessoId}/cronograma-fases", UriKind.Relative))
            {
                Content = JsonContent.Create(fases),
            };
            Autenticar(request);
            request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
            return await Client.SendAsync(request).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<FaseCronograma>> ObterFasesGravadasAsync()
        {
            await using AsyncServiceScope scope = Api.Services.CreateAsyncScope();
            SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
            return await db.Set<FaseCronograma>().AsNoTracking()
                .Where(f => f.ProcessoSeletivoId == ProcessoId)
                .OrderBy(f => f.Ordem)
                .ToListAsync().ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<FaseCronogramaDto>> ObterFasesLidasPelaApiAsync()
        {
            using HttpRequestMessage request = new(
                HttpMethod.Get,
                new Uri($"/api/selecao/processos-seletivos/{ProcessoId}", UriKind.Relative));
            Autenticar(request);
            using HttpResponseMessage resposta = await Client.SendAsync(request).ConfigureAwait(false);
            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            ProcessoSeletivoDto? processo = await resposta.Content
                .ReadFromJsonAsync<ProcessoSeletivoDto>(JsonDoContrato.Opcoes).ConfigureAwait(false);
            return [.. processo!.CronogramaFases.OrderBy(f => f.Ordem)];
        }

        public async Task<Unidade> SemearUnidadeAsync(string sigla, string nome)
        {
            string sufixo = Guid.CreateVersion7().ToString("N")[^8..];
            await using AsyncServiceScope scope = Api.Services.CreateAsyncScope();
            OrganizacaoInstitucionalDbContext org = scope.ServiceProvider.GetRequiredService<OrganizacaoInstitucionalDbContext>();
            Unidade unidade = Unidade.Criar(
                nome: nome,
                alias: null,
                slug: $"{sigla.ToLowerInvariant()}-{sufixo}",
                sigla: $"{sigla}{sufixo}",
                codigo: $"{sigla}-{sufixo}",
                unidadeSuperiorId: null,
                tipo: TipoUnidade.Centro,
                unidadeAcademica: true,
                vigenciaInicio: new DateOnly(2020, 1, 1),
                vigenciaFim: null,
                cidadeCodigoIbge: "1504208",
                cidadeNome: "Marabá",
                cidadeUf: "PA").Value!;
            org.Unidades.Add(unidade);
            await org.SaveChangesAsync().ConfigureAwait(false);
            return unidade;
        }

        public async Task ExcluirUnidadeAsync(Guid unidadeId)
        {
            await using AsyncServiceScope scope = Api.Services.CreateAsyncScope();
            OrganizacaoInstitucionalDbContext org = scope.ServiceProvider.GetRequiredService<OrganizacaoInstitucionalDbContext>();
            Unidade unidade = await org.Unidades.SingleAsync(u => u.Id == unidadeId).ConfigureAwait(false);
            unidade.MarkAsDeleted("teste", DateTimeOffset.UtcNow);
            await org.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task RenomearUnidadeAsync(Guid unidadeId, string novaSigla, string novoNome)
        {
            await using AsyncServiceScope scope = Api.Services.CreateAsyncScope();
            OrganizacaoInstitucionalDbContext org = scope.ServiceProvider.GetRequiredService<OrganizacaoInstitucionalDbContext>();
            Unidade unidade = await org.Unidades.SingleAsync(u => u.Id == unidadeId).ConfigureAwait(false);
            unidade.Atualizar(
                novoNome, unidade.Alias, unidade.Slug.Valor, novaSigla, unidade.Codigo, unidade.UnidadeSuperiorId,
                unidade.Tipo, unidade.UnidadeAcademica, unidade.VigenciaFim, new DateOnly(2026, 10, 28),
                motivoMudancaIdentificador: "Renomeada pelo cadastro",
                cidadeCodigoIbge: unidade.CidadeCodigoIbge, cidadeNome: unidade.CidadeNome, cidadeUf: unidade.CidadeUf)
                .IsSuccess.Should().BeTrue();
            await org.SaveChangesAsync().ConfigureAwait(false);
        }

        private static object Fase(int ordem, Guid faseCanonicaId, Guid? unidadeResponsavelId) => new
        {
            ordem,
            faseCanonicaId,
            inicio = (DateTimeOffset?)null,
            fim = (DateTimeOffset?)null,
            produtos = Array.Empty<object>(),
            faseConcluinteCodigo = (string?)null,
            emiteParecerIndividual = false,
            bancasRequeridas = Array.Empty<object>(),
            regraRecurso = (object?)null,
            unidadeResponsavelId,
        };

        private static void Autenticar(HttpRequestMessage request)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
            request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        }
    }
}
