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
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O modelo de solicitação de isenção da taxa, semeado pela migration da Configuração, é aplicado a
/// um processo em rascunho que cobra taxa, de um tipo qualquer, cuja inscrição coleta a origem escolar.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class AplicarModeloDeIsencaoDaTaxaTests
{
    private readonly CascadingFixture _fixture;

    public AplicarModeloDeIsencaoDaTaxaTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "O modelo de isenção é aplicado a processo SiSU em rascunho que cobra taxa, com a renda no formulário de isenção")]
    public async Task ModeloDeIsencao_EhAplicadoAProcessoQueCobraTaxa()
    {
        Guid processoId = await SemearProcessoQueCobraTaxaAsync();

        HttpResponseMessage resposta = await AplicarAsync(processoId, await ModeloAsync());

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        SelecaoDbContext db = escopo.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        ProcessoSeletivo processo = await db.ProcessosSeletivos.AsNoTracking()
            .Include(static p => p.Formularios)
            .Include(static p => p.Campos)
            .SingleAsync(p => p.Id == processoId);
        processo.Formularios.Should().Contain(static f => f.Finalidade == FinalidadeFormulario.IsencaoTaxa);
        processo.Campos.Should().ContainSingle(static c =>
            c.FatoCodigo == IsencaoPorCarenciaSocioeconomica.FatoRenda && c.Finalidade == FinalidadeFormulario.IsencaoTaxa);
    }

    /// <summary>
    /// O processo SiSU com a fase única dividida entre inscrição e isenção, a taxa cobrada com a isenção
    /// por carência socioeconômica, e a inscrição que coleta as duas perguntas da origem escolar.
    /// </summary>
    private async Task<Guid> SemearProcessoQueCobraTaxaAsync()
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        SelecaoDbContext db = escopo.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(
            db,
            $"Processo que cobra taxa {Guid.CreateVersion7()}",
            coletaSolicitacaoIsencao: true,
            complementar: static p =>
            {
                ConfiguracaoTaxaInscricao taxa = ConfiguracaoTaxaInscricao.Criar(
                    cobra: true, valor: 100m, fundamentosCodigos: [FundamentoIsencaoCodigo.CarenciaSocioeconomica]).Value!;
                Result taxaDefinida = p.DefinirTaxaInscricao(taxa, PrecondicaoIfMatch.Ausente);
                taxaDefinida.IsSuccess.Should().BeTrue(taxaDefinida.Error?.Message);

                int ordem = FormularioDeTeste.PrimeiraOrdemDeInscricao;
                Result itens = p.DefinirItens(
                [
                    FatoColetado.Criar(OrigemEscolar.FatoFormaDeConclusao, ordem, "Como você concluiu o ensino médio?",
                        TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL").Value!,
                    FatoColetado.Criar(OrigemEscolar.FatoOndeCursou, ordem + 1, "Onde você cursou o ensino médio?",
                        TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL").Value!,
                ]);
                itens.IsSuccess.Should().BeTrue(itens.Error?.Message);
            });
        return processo.Id;
    }

    private async Task<Guid> ModeloAsync()
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        ConfiguracaoDbContext db = escopo.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        return await db.ModelosFormulario.Where(m => m.Codigo == SementeIsencaoDaTaxa.ModeloDeIsencao).Select(static m => m.Id).SingleAsync();
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
