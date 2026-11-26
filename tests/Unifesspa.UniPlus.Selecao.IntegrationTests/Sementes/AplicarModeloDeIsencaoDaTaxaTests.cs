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
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O modelo de solicitação de isenção da taxa, semeado pela migration da Configuração, é aplicado ao
/// formulário facultativo de pedido de isenção de um processo em rascunho que cobra taxa, de um tipo qualquer,
/// cuja inscrição coleta a origem escolar.
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

        HttpResponseMessage resposta = await AplicarAsync(processoId, await ModeloAsync(), FormulariosDoProcessoDeTeste.PedidoDeIsencao);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        ProcessoSeletivo processo = await LerComFormulariosECamposAsync(processoId);
        FormularioProcesso pedido = processo.FormularioDoCodigo(FormulariosDoProcessoDeTeste.PedidoDeIsencao)!;
        pedido.Obrigatorio.Should().BeFalse("o pedido de isenção é facultativo");
        pedido.ModeloDeOrigem.Should().NotBeNull();
        processo.Campos.Should().ContainSingle(c =>
            c.FatoCodigo == IsencaoPorCarenciaSocioeconomica.FatoRenda && c.FormularioId == pedido.Id);
    }

    private async Task<ProcessoSeletivo> LerComFormulariosECamposAsync(Guid processoId)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        return await db.ProcessosSeletivos.AsNoTracking()
            .Include(static p => p.Formularios)
            .Include(static p => p.Campos)
            .SingleAsync(p => p.Id == processoId);
    }

    /// <summary>
    /// O processo SiSU com a fase única dividida entre inscrição e isenção, a taxa cobrada com a isenção
    /// por carência socioeconômica, e a inscrição que coleta as duas perguntas da origem escolar.
    /// </summary>
    private async Task<Guid> SemearProcessoQueCobraTaxaAsync()
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, new OpcoesDoProcessoSeletivoPublicavel
        {
            Nome = $"Processo que cobra taxa {Guid.CreateVersion7()}",
            ColetaSolicitacaoIsencao = true,
            Complementar = static p => CobrarTaxaEColetarAOrigemEscolar(p),
        });
        return processo.Id;
    }

    private static void CobrarTaxaEColetarAOrigemEscolar(ProcessoSeletivo processo)
    {
        CobrarTaxa(processo);
        processo.DefinirFormularioSemConjuntoBasico(
            FormulariosDoProcessoDeTeste.PedidoDeIsencao, processo.CronogramaFases.Single().Id, obrigatorio: false);

        int ordem = FormularioDeTeste.PrimeiraOrdemDeInscricao;
        Result itens = processo.DefinirItens(
        [
            PerguntaDeEscolhaUnica(OrigemEscolar.FatoFormaDeConclusao, ordem, "Como você concluiu o ensino médio?"),
            PerguntaDeEscolhaUnica(OrigemEscolar.FatoOndeCursou, ordem + 1, "Onde você cursou o ensino médio?"),
        ]);
        itens.IsSuccess.Should().BeTrue(itens.Error?.Message);
    }

    private static void CobrarTaxa(ProcessoSeletivo processo)
    {
        ConfiguracaoTaxaInscricao taxa = ConfiguracaoTaxaInscricao.Criar(
            cobra: true, valor: 100m, fundamentosCodigos: ["CARENCIA_SOCIOECONOMICA"]).Value!;
        Result taxaDefinida = processo.DefinirTaxaInscricao(taxa);
        taxaDefinida.IsSuccess.Should().BeTrue(taxaDefinida.Error?.Message);
    }

    private static FatoColetado PerguntaDeEscolhaUnica(string fatoCodigo, int ordem, string rotulo) =>
        FatoColetadoDeTeste.Coletando(fatoCodigo, ordem)
            .ComRotulo(rotulo)
            .DoTipo(TipoDeCampo.EscolhaUnica)
            .ComObrigatoriedade(Obrigatoriedade.Sempre)
            .Construir();

    private async Task<Guid> ModeloAsync()
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        return await db.ModelosFormulario.Where(m => m.Codigo == SementeIsencaoDaTaxa.ModeloDeIsencao).Select(static m => m.Id).SingleAsync();
    }

    private async Task<HttpResponseMessage> AplicarAsync(Guid processoId, Guid modeloId, string formularioCodigo)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(
            HttpMethod.Post, new Uri($"/api/selecao/admin/processos-seletivos/{processoId}/formularios/aplicacoes-de-modelo", UriKind.Relative))
        {
            Content = JsonContent.Create(new { modeloId, formularioCodigo }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        return await client.SendAsync(request);
    }
}
