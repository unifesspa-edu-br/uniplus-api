namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.ApoioDoFormularioPeloEndpoint;

/// <summary>
/// A escrita administrativa do formulário pelo código (UNI-REQ-0144), fim a fim pelo HTTP, sob
/// <c>/admin/processos-seletivos/{id}/formularios/{codigo}</c> (ADR-0064).
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class FormulariosEscritaEndpointTests
{
    private readonly CascadingFixture _fixture;

    public FormulariosEscritaEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "PUT admin sem autenticação é 401")]
    public async Task Definir_SemAutenticacao_401()
    {
        Contexto ctx = await SemearRascunhoAsync(_fixture, nameof(Definir_SemAutenticacao_401));

        HttpResponseMessage resposta = await ctx.PutFormularioAsync(
            "Título", autenticar: Autenticacao.Nenhuma);

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "PUT admin em rascunho é 204 sem ETag e persiste o título")]
    public async Task Definir_EmRascunho_204SemEtagEPersiste()
    {
        Contexto ctx = await SemearRascunhoAsync(_fixture, nameof(Definir_EmRascunho_204SemEtagEPersiste));

        HttpResponseMessage resposta = await ctx.PutFormularioAsync("Formulário de Inscrição");

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using AsyncServiceScope scope = ctx.Api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        ProcessoSeletivo processo = await db.Set<ProcessoSeletivo>().AsNoTracking().Include(static p => p.Formularios)
            .SingleAsync(p => p.Id == ctx.ProcessoId);
        processo.FormularioDoCodigo(FormulariosDoProcessoDeTeste.Inscricao)!.Titulo.Should().Be("Formulário de Inscrição");
    }

    [Fact(DisplayName = "PUT admin com título acima do limite é 422 com a violação em errors[]")]
    public async Task Definir_TituloAcimaDoLimite_422()
    {
        Contexto ctx = await SemearRascunhoAsync(_fixture, nameof(Definir_TituloAcimaDoLimite_422));

        HttpResponseMessage resposta = await ctx.PutFormularioAsync(new string('a', 301));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        // field usa o mesmo casing do payload JSON (camelCase, ADR-0023), não o PascalCase do C#.
        JsonElement erro = doc.RootElement.GetProperty("errors").EnumerateArray().Single();
        erro.GetProperty("field").GetString().Should().Be("titulo");
        erro.GetProperty("code").GetString().Should().Be("uniplus.estrutura_formulario.titulo_tamanho");
    }

    [Fact(DisplayName = "Código fora do formato é 404 na leitura e 422 na criação pelo PUT")]
    public async Task CodigoForaDoFormato_404NaLeituraE422NaCriacao()
    {
        Contexto ctx = await SemearRascunhoAsync(_fixture, nameof(CodigoForaDoFormato_404NaLeituraE422NaCriacao));
        const string foraDoFormato = "inscricao_em_minusculas";

        HttpResponseMessage escrita = await ctx.PutFormularioAsync("Título", codigo: foraDoFormato);
        HttpResponseMessage leitura = await ctx.GetFormularioAsync(foraDoFormato);
        HttpResponseMessage remocao = await ctx.DeleteFormularioAsync(foraDoFormato);

        escrita.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await escrita.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("code").GetString().Should().Be("uniplus.selecao.formulario_processo.codigo_formato_invalido");
        leitura.StatusCode.Should().Be(HttpStatusCode.NotFound);
        remocao.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "Código com mais de 60 caracteres é recusado como fora do formato na criação")]
    public async Task CodigoComMaisDeSessentaCaracteres_422()
    {
        Contexto ctx = await SemearRascunhoAsync(_fixture, nameof(CodigoComMaisDeSessentaCaracteres_422));

        HttpResponseMessage escrita = await ctx.PutFormularioAsync("Título", codigo: new string('A', 61));

        escrita.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "Código no limite de 60 caracteres cria o formulário")]
    public async Task CodigoComSessentaCaracteres_204()
    {
        Contexto ctx = await SemearRascunhoAsync(_fixture, nameof(CodigoComSessentaCaracteres_204));

        HttpResponseMessage escrita = await ctx.PutFormularioAsync("Título", codigo: new string('A', 60), obrigatorio: false);

        escrita.StatusCode.Should().Be(HttpStatusCode.NoContent, await escrita.Content.ReadAsStringAsync());
    }

    [Fact(DisplayName = "Código novo cria o formulário com a obrigatoriedade declarada, ao lado do outro na mesma fase")]
    public async Task Definir_CodigoNovo_CriaAoLadoDoFormularioDaMesmaFase()
    {
        Contexto ctx = await SemearRascunhoAsync(_fixture, nameof(Definir_CodigoNovo_CriaAoLadoDoFormularioDaMesmaFase));
        Guid fase = await ctx.FaseDeInscricaoAsync();
        (await ctx.PutFormularioAsync("Inscrição")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage resposta = await ctx.PutFormularioAsync(
            "Dados complementares", codigo: "DADOS_COMPLEMENTARES", faseId: fase, obrigatorio: false);

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent, await resposta.Content.ReadAsStringAsync());
        ProcessoSeletivo processo = await LerProcessoAsync(ctx);
        processo.Formularios.Select(static f => (f.Codigo.Valor, f.Obrigatorio, f.FaseId))
            .Should().BeEquivalentTo([("INSCRICAO", true, (Guid?)fase), ("DADOS_COMPLEMENTARES", false, (Guid?)fase)]);
    }

    [Fact(DisplayName = "Repetir o PUT do mesmo código substitui o conteúdo e mantém o mesmo formulário")]
    public async Task Definir_MesmoCodigoDeNovo_SubstituiMantendoOIdentificador()
    {
        Contexto ctx = await SemearRascunhoAsync(_fixture, nameof(Definir_MesmoCodigoDeNovo_SubstituiMantendoOIdentificador));
        (await ctx.PutFormularioAsync("Primeiro título")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        Guid identificador = (await LerProcessoAsync(ctx)).FormularioDoCodigo(FormulariosDoProcessoDeTeste.Inscricao)!.Id;

        (await ctx.PutFormularioAsync("Segundo título")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        FormularioProcesso formulario = (await LerProcessoAsync(ctx)).Formularios.Should().ContainSingle().Which;
        formulario.Id.Should().Be(identificador);
        formulario.Titulo.Should().Be("Segundo título");
    }

    [Fact(DisplayName = "Remover, gravar itens e gravar termos de formulário que o processo não tem é 404 de formulário não encontrado")]
    public async Task FormularioQueOProcessoNaoTem_404NasTresRotas()
    {
        Contexto ctx = await SemearRascunhoAsync(_fixture, nameof(FormularioQueOProcessoNaoTem_404NasTresRotas));
        string raiz = $"/api/selecao/admin/processos-seletivos/{ctx.ProcessoId}/formularios/FORMULARIO_QUE_NAO_EXISTE";

        HttpResponseMessage remocao = await ctx.DeleteFormularioAsync("FORMULARIO_QUE_NAO_EXISTE");
        HttpResponseMessage itens = await PutAsync(ctx, $"{raiz}/itens", new { itens = Array.Empty<object>() });
        HttpResponseMessage termos = await PutAsync(ctx, $"{raiz}/termos", new { termos = Array.Empty<object>() });

        foreach (HttpResponseMessage resposta in new[] { remocao, itens, termos })
        {
            string corpo = await resposta.Content.ReadAsStringAsync();
            resposta.StatusCode.Should().Be(HttpStatusCode.NotFound, corpo);
            using JsonDocument problema = JsonDocument.Parse(corpo);
            problema.RootElement.GetProperty("code").GetString().Should().Be("uniplus.selecao.formulario_processo.nao_encontrado");
        }
    }

    private static async Task<HttpResponseMessage> PutAsync(Contexto ctx, string rota, object corpo)
    {
        using HttpRequestMessage request = new(HttpMethod.Put, new Uri(rota, UriKind.Relative)) { Content = JsonContent.Create(corpo) };
        Autenticar(request, Autenticacao.PlataformaAdmin);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", MakeIdempotencyKey());
        return await ctx.Client.SendAsync(request);
    }

    [Fact(DisplayName = "DELETE do formulário sem exigência de documento ligada é 204 e o retira do processo")]
    public async Task Remover_SemExigenciaLigada_204()
    {
        Contexto ctx = await SemearRascunhoAsync(_fixture, nameof(Remover_SemExigenciaLigada_204));
        (await ctx.PutFormularioAsync("Dados complementares", codigo: "DADOS_COMPLEMENTARES", obrigatorio: false, faseId: await ctx.FaseDeInscricaoAsync()))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage resposta = await ctx.DeleteFormularioAsync("DADOS_COMPLEMENTARES");

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent, await resposta.Content.ReadAsStringAsync());
        (await LerProcessoAsync(ctx)).FormularioDoCodigo("DADOS_COMPLEMENTARES").Should().BeNull();
    }

    [Fact(DisplayName = "DELETE do formulário que entrega exigência de documento é 422, e a exigência continua")]
    public async Task Remover_ComExigenciaLigada_422AMantemAExigencia()
    {
        Contexto ctx = await SemearComExigenciaNoFormularioAsync(nameof(Remover_ComExigenciaLigada_422AMantemAExigencia));

        HttpResponseMessage resposta = await ctx.DeleteFormularioAsync("DADOS_COMPLEMENTARES");

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("code").GetString().Should().Be("uniplus.selecao.formulario_processo.remocao_de_formulario_com_exigencia");
        ProcessoSeletivo processo = await LerProcessoAsync(ctx);
        processo.FormularioDoCodigo("DADOS_COMPLEMENTARES").Should().NotBeNull();
        processo.DocumentosExigidos.Should().ContainSingle().Which.FormularioCodigo!.Valor.Should().Be("DADOS_COMPLEMENTARES");
    }

    private async Task<Contexto> SemearComExigenciaNoFormularioAsync(string nome)
    {
        await TiposDeAtoSeeder.SemearAsync(_fixture.Factory.Services);
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, new OpcoesDoProcessoSeletivoPublicavel
        {
            Nome = $"{nome} {Guid.CreateVersion7()}",
            Complementar = static p =>
            {
                Guid fase = p.CronogramaFases.Single().Id;
                p.DefinirFormularioSemConjuntoBasico("DADOS_COMPLEMENTARES", fase, obrigatorio: false);
                ExigenciaNoFormularioDeTeste.Exigir(p, "DADOS_COMPLEMENTARES", fase);
            },
        });
        return new Contexto(_fixture.Factory, _fixture.Factory.CreateClient(), processo.Id, documento.Id);
    }

    private static async Task<ProcessoSeletivo> LerProcessoAsync(Contexto ctx)
    {
        await using AsyncServiceScope scope = ctx.Api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        return await db.Set<ProcessoSeletivo>().AsNoTracking()
            .Include(static p => p.Formularios)
            .Include(static p => p.DocumentosExigidos)
            .SingleAsync(p => p.Id == ctx.ProcessoId);
    }
}
