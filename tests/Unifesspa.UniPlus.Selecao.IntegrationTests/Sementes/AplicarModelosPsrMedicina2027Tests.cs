namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Sementes;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using AwesomeAssertions;

using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

/// <summary>
/// Os modelos de inscrição e de habilitação do edital de Medicina 2027, semeados pela migration da
/// Configuração, são aplicados a um processo PSR em rascunho, com bônus regional, pelo passo
/// Formulários, sem recusa.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class AplicarModelosPsrMedicina2027Tests
{
    private readonly CascadingFixture _fixture;

    public AplicarModelosPsrMedicina2027Tests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "A inscrição e a habilitação de Medicina são aplicadas ao processo PSR com bônus regional, com o município do bônus e a composição familiar")]
    public async Task ModelosDeMedicina_SaoAplicadosAoProcessoPsr()
    {
        Guid processoId = await SemearProcessoPsrAsync();

        // A habilitação pressupõe a forma de conclusão do ensino médio, que a inscrição coleta.
        HttpResponseMessage inscricao = await AplicarAsync(processoId, await ModeloAsync(SementePsrMedicina2027.ModeloDeInscricao));
        inscricao.StatusCode.Should().Be(HttpStatusCode.OK, await inscricao.Content.ReadAsStringAsync());
        HttpResponseMessage habilitacao = await AplicarAsync(processoId, await ModeloAsync(SementePsrMedicina2027.ModeloDeHabilitacao));
        habilitacao.StatusCode.Should().Be(HttpStatusCode.OK, await habilitacao.Content.ReadAsStringAsync());

        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        SelecaoDbContext db = escopo.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        ProcessoSeletivo processo = await db.ProcessosSeletivos.AsNoTracking()
            .Include(static p => p.Formularios)
            .Include(static p => p.Campos)
            .Include(static p => p.GruposColetados)
            .SingleAsync(p => p.Id == processoId);
        processo.Formularios.Select(static f => f.Finalidade)
            .Should().BeEquivalentTo([FinalidadeFormulario.Inscricao, FinalidadeFormulario.Habilitacao]);
        processo.GruposColetados.Should().ContainSingle(static g => g.Codigo == "COMPOSICAO_FAMILIAR");
        processo.Campos.Should().ContainSingle(static c => c.FatoCodigo == "MUNICIPIO_EM_AREA_BONUS");
    }

    private async Task<Guid> SemearProcessoPsrAsync()
    {
        TipoProcessoSnapshot psr = TipoProcessoSnapshot.Criar(
            SementePsrMedicina2027.TipoProcessoId, SementePsrMedicina2027.TipoProcessoCodigo, "Processo Seletivo Regular Unificado").Value!;
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        SelecaoDbContext db = escopo.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(
            db, $"Medicina 2027 {Guid.CreateVersion7()}", complementar: ComBonusRegional, tipo: psr);
        return processo.Id;
    }

    /// <summary>O bônus regional com um município, de onde o campo do município do bônus tira as opções.</summary>
    private static void ComBonusRegional(ProcessoSeletivo processo)
    {
        ConfiguracaoBonusRegional bonus = ConfiguracaoBonusRegional.Criar(
            ReferenciaRegra.Criar(RegraBonusCodigo.Multiplicativo, "v1", new string('e', 64)).Value!, 1.20m, null,
            Guid.CreateVersion7(), "PORTARIA", "Portaria Unifesspa nº 2514/2023", "Institui inclusão regional",
            [("1504208", "Marabá", "PA")]).Value!;
        Result definido = processo.DefinirBonusRegional(aplica: true, bonus: bonus, PrecondicaoIfMatch.Ausente);
        definido.IsSuccess.Should().BeTrue(definido.Error?.Message);
    }

    private async Task<Guid> ModeloAsync(string codigo)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        ConfiguracaoDbContext db = escopo.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        return await db.ModelosFormulario.Where(m => m.Codigo == codigo).Select(static m => m.Id).SingleAsync();
    }

    private async Task<HttpResponseMessage> AplicarAsync(Guid processoId, Guid modeloId)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(
            HttpMethod.Post, new Uri($"/api/selecao/admin/processos-seletivos/{processoId}/formularios/aplicacoes-de-modelo", UriKind.Relative))
        {
            Content = JsonContent.Create(new { modeloId }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        return await client.SendAsync(request);
    }
}
