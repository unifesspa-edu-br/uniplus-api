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
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
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

        HttpResponseMessage resposta = await PutDocumentosExigidosAsync(
            client, processo.Id, [RegraDaInscricaoCondicionadaA(bolsista, tipoDocumentoId, processo.CronogramaFases.Single().Id)]);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await resposta.Content.ReadAsStringAsync());
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("code").GetString().Should().Be("uniplus.selecao.documento_exigido.fato_da_isencao_em_outra_finalidade");
    }

    private static async Task<HttpResponseMessage> PutDocumentosExigidosAsync(HttpClient client, Guid processoId, object[] corpo)
    {
        using HttpRequestMessage request = new(HttpMethod.Put, new Uri($"/api/selecao/processos-seletivos/{processoId}/documentos-exigidos", UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        request.Content = JsonContent.Create(corpo);

        return await client.SendAsync(request);
    }

    private static object RegraDaInscricaoCondicionadaA(string fato, Guid tipoDocumentoId, Guid faseId) => new
    {
        forma = "DOCUMENTO",
        documento = new
        {
            tipoDocumento = new { id = tipoDocumentoId },
            momento = new { faseId, finalidade = "INSCRICAO" },
            aplicabilidade = new
            {
                tipo = "CONDICIONAL",
                condicoes = new[] { new { clausula = 0, fato, operador = "IGUAL", valor = "true" } },
            },
            indeferimento = new { obrigatorio = true, basesLegais = Array.Empty<object>() },
            requisitos = new { formatosPermitidos = "QUALQUER" },
        },
    };

    /// <summary>Grava no catálogo o fato da isenção e o tipo de documento; devolve o tipo de documento.</summary>
    private async Task<Guid> SemearCatalogoAsync(string fato, string codigoDoDocumento)
    {
        FatoCandidato bolsista = CriacaoDoFato.CriarDoAdministrador(
            new DescritivoDoFato(fato, "Bolsista", "Descrição do fato."),
            new DefinicaoDoFato(TipoDoDado.SimOuNao, QuantidadeDeValores.ValorUnico, null, null, "RESULTADO_FINAL", EscopoFato.Candidato, ClassificacaoDeProtecao.Pessoal, "Isenção da taxa", HipoteseLegalTratamento.CumprimentoObrigacaoLegal)).Value!;
        TipoDocumento tipoDocumento = TipoDocumento.Criar(new DeclaracaoDoTipoDocumento(
            codigoDoDocumento, "Comprovante de bolsa", Descricao: null, Categoria: "IDENTIFICACAO",
            FormatosAceitos: null, TamanhoMaximoMb: null, TipoEquivalente: null, ClassificacaoProtecao: ClassificacaoDeProtecao.Pessoal, FinalidadeTratamento: "Comprovação documental do candidato.", HipoteseLegal: HipoteseLegalTratamento.CumprimentoObrigacaoLegal)).Value!;
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        db.AddRange(bolsista, tipoDocumento);
        await db.SaveChangesAsync();
        await db.AprovarCatalogoAsync();
        return tipoDocumento.Id;
    }

    /// <summary>O processo com a fase única dividida entre inscrição e isenção, e o fato que só a isenção coleta.</summary>
    private async Task<ProcessoSeletivo> SemearProcessoAsync(string fatoDaIsencao)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, DocumentoAPublicar _) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, new OpcoesDoProcessoSeletivoPublicavel
        {
            Nome = $"Inscrição e isenção na mesma fase {Guid.CreateVersion7()}",
            ColetaSolicitacaoIsencao = true,
            Complementar = p => ColetarFatoNaIsencao(p, fatoDaIsencao),
        });
        return processo;
    }

    private static void ColetarFatoNaIsencao(ProcessoSeletivo processo, string fato)
    {
        processo.DefinirFormulario(
                FinalidadeFormulario.IsencaoTaxa, processo.CronogramaFases.Single().Id, null, FormularioDeTeste.Etapas(FinalidadeFormulario.IsencaoTaxa))
            .IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(
                FinalidadeFormulario.IsencaoTaxa,
                [FatoColetadoDeTeste.Coletando(fato, 0)
                    .ComRotulo("Bolsista")
                    .ComObrigatoriedade(Obrigatoriedade.Sempre)
                    .NaSecao(FormularioDeTeste.Secao)
                    .Construir()])
            .IsSuccess.Should().BeTrue();
    }
}
