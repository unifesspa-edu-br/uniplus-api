namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;
using Domain.ValueObjects;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O formulário a que o documento exigido pertence, pelo HTTP: na fase que divide a inscrição e a
/// isenção, o gatilho de documento da inscrição não cita o fato que só o formulário de isenção coleta.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class FinalidadeDaExigenciaEndpointTests
{
    private readonly CascadingFixture _fixture;

    public FinalidadeDaExigenciaEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "Na fase que divide inscrição e isenção, o documento da inscrição que cita fato só da isenção é recusado com 422")]
    public async Task DefinirDocumentosExigidos_DocumentoDaInscricaoCitaFatoDaIsencao_Recusa()
    {
        string sufixo = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        string bolsista = $"BOLSISTA_{sufixo}";
        Guid tipoDocumentoId = await SemearCatalogoAsync(bolsista, $"COMPROVANTE_BOLSA_{sufixo}");
        ProcessoSeletivo processo = await SemearProcessoAsync(bolsista);
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpRequestMessage request = new(HttpMethod.Put, new Uri($"/api/selecao/processos-seletivos/{processo.Id}/documentos-exigidos", UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        request.Content = JsonContent.Create(new[]
        {
            new
            {
                tipo = "FOLHA",
                documento = new
                {
                    exigidoNaFaseId = processo.CronogramaFases.Single().Id,
                    finalidade = "INSCRICAO",
                    tipoDocumentoId,
                    aplicabilidade = "CONDICIONAL",
                    obrigatorio = true,
                    condicoes = new[] { new { clausula = 0, fato = bolsista, operador = "IGUAL", valor = "true" } },
                    basesLegais = Array.Empty<object>(),
                    formatosPermitidos = "QUALQUER",
                },
            },
        });

        HttpResponseMessage resposta = await client.SendAsync(request);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await resposta.Content.ReadAsStringAsync());
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("code").GetString().Should().Be("uniplus.selecao.documento_exigido.fato_da_isencao_em_outra_finalidade");
    }

    /// <summary>Grava no catálogo o fato da isenção e o tipo de documento; devolve o tipo de documento.</summary>
    private async Task<Guid> SemearCatalogoAsync(string fato, string codigoDoDocumento)
    {
        FatoCandidato bolsista = FatoCandidato.CriarDoAdministrador(
            fato, "Bolsista", null, DominioFato.Booleano, CardinalidadeFato.Escalar, fonteValores: null, formato: null,
            "RESULTADO_FINAL", EscopoFato.Candidato, ClassificacaoProtecaoDado.Pessoal, "Isenção da taxa",
            HipoteseLegalTratamento.CumprimentoObrigacaoLegal).Value!;
        TipoDocumento tipoDocumento = TipoDocumento.Criar(
            codigoDoDocumento, "Comprovante de bolsa", descricao: null, categoria: "IDENTIFICACAO",
            formatosAceitos: null, tamanhoMaximoMb: null, tipoEquivalente: null).Value!;
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        ConfiguracaoDbContext db = escopo.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        db.AddRange(bolsista, tipoDocumento);
        await db.SaveChangesAsync();
        return tipoDocumento.Id;
    }

    /// <summary>O processo com a fase única dividida entre inscrição e isenção, e o fato que só a isenção coleta.</summary>
    private async Task<ProcessoSeletivo> SemearProcessoAsync(string fatoDaIsencao)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        SelecaoDbContext db = escopo.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        (ProcessoSeletivo processo, DocumentoEdital _) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(
            db,
            $"Inscrição e isenção na mesma fase {Guid.CreateVersion7()}",
            coletaSolicitacaoIsencao: true,
            complementar: p =>
            {
                Guid fase = p.CronogramaFases.Single().Id;
                p.DefinirFormulario(FinalidadeFormulario.IsencaoTaxa, fase, null, FormularioDeTeste.Etapas(FinalidadeFormulario.IsencaoTaxa), PrecondicaoIfMatch.Ausente)
                    .IsSuccess.Should().BeTrue();
                p.DefinirFatosColetados(
                        FinalidadeFormulario.IsencaoTaxa,
                        [FatoColetado.Criar(fatoDaIsencao, 0, "Bolsista", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null, etapaCodigo: FormularioDeTeste.Secao).Value!],
                        PrecondicaoIfMatch.Ausente)
                    .IsSuccess.Should().BeTrue();
            });
        return processo;
    }
}
