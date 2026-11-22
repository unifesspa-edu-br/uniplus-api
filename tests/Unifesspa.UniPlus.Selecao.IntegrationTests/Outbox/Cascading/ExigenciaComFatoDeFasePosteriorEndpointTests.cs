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
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.FaseDoCronogramaPublicavelDeTeste;

/// <summary>
/// O documento exigido na fase de isenção cujo gatilho cita um fato que só fica conhecido na fase seguinte, pelo HTTP e com a ordem
/// derivada do cronograma: recusado com 422 quando a fase seguinte vem depois, aceito quando ela está marcada para acontecer no mesmo
/// momento da anterior. O fato se situa no catálogo na fase de ensalamento e é coletado pelo formulário de isenção da fase de resultado.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class ExigenciaComFatoDeFasePosteriorEndpointTests(CascadingFixture fixture)
{
    private const string FaseDoFato = "ENSALAMENTO";

    [Fact(DisplayName = "O fato que só fica conhecido na fase seguinte, que vem depois, é recusado no gatilho do documento da fase de isenção")]
    public async Task FatoDaFaseSeguinte_RecusadoNoGatilhoDaFaseAnterior()
    {
        using HttpResponseMessage resposta = await DefinirExigenciaAsync(faseDoFatoNoMesmoMomento: false);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await resposta.Content.ReadAsStringAsync());
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("code").GetString().Should().Be("uniplus.selecao.documento_exigido.fato_resolvido_em_fase_posterior");
    }

    [Fact(DisplayName = "O mesmo fato é aceito quando a fase seguinte está marcada para acontecer no mesmo momento da fase de isenção")]
    public async Task FatoDaFaseNoMesmoMomento_AceitoNoGatilhoDaFaseAnterior()
    {
        using HttpResponseMessage resposta = await DefinirExigenciaAsync(faseDoFatoNoMesmoMomento: true);

        resposta.IsSuccessStatusCode.Should().BeTrue(await resposta.Content.ReadAsStringAsync());
    }

    private async Task<HttpResponseMessage> DefinirExigenciaAsync(bool faseDoFatoNoMesmoMomento)
    {
        string sufixo = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        string fato = $"ELEGIVEL_{sufixo}";
        Guid tipoDocumentoId = await SemearCatalogoAsync(fato, $"COMPROVANTE_{sufixo}");
        ProcessoSeletivo processo = await SemearProcessoAsync(fato, faseDoFatoNoMesmoMomento);
        using HttpClient cliente = fixture.Factory.CreateClient();

        return await cliente.SendAsync(Requisicao(processo, fato, tipoDocumentoId));
    }

    private static HttpRequestMessage Requisicao(ProcessoSeletivo processo, string fato, Guid tipoDocumentoId)
    {
        HttpRequestMessage request = new(HttpMethod.Put, new Uri($"/api/selecao/processos-seletivos/{processo.Id}/documentos-exigidos", UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        request.Content = JsonContent.Create(new[] { new { forma = "DOCUMENTO", documento = DocumentoExigido(processo.CronogramaFases.Single(f => f.Ordem == 1).Id, tipoDocumentoId, fato) } });

        return request;
    }

    private static object DocumentoExigido(Guid faseId, Guid tipoDocumentoId, string fato) => new
    {
        tipoDocumento = new { id = tipoDocumentoId },
        momento = new { faseId, finalidade = "ISENCAO_TAXA" },
        aplicabilidade = new
        {
            tipo = "CONDICIONAL",
            condicoes = new[] { new { clausula = 0, fato, operador = "IGUAL", valor = "true" } },
        },
        indeferimento = new { obrigatorio = true, basesLegais = Array.Empty<object>() },
        requisitos = new { formatosPermitidos = "QUALQUER" },
    };

    private async Task<Guid> SemearCatalogoAsync(string fato, string codigoDoDocumento)
    {
        FatoCandidato elegivel = CriacaoDoFato.CriarDoAdministrador(
            fato, "Elegível", "Descrição do fato.", DominioFato.Booleano, CardinalidadeFato.Escalar, fonteValores: null, formato: null,
            FaseDoFato, EscopoFato.Candidato, ClassificacaoDeProtecao.Pessoal, "Isenção da taxa",
            HipoteseLegalTratamento.CumprimentoObrigacaoLegal).Value!;
        TipoDocumento tipoDocumento = TipoDocumento.Criar(
            codigoDoDocumento, "Comprovante de elegibilidade", descricao: null, categoria: "IDENTIFICACAO",
            formatosAceitos: null, tamanhoMaximoMb: null, tipoEquivalente: null, classificacaoProtecao: ClassificacaoDeProtecao.Pessoal, finalidadeTratamento: "Comprovação documental do candidato.", hipoteseLegal: HipoteseLegalTratamento.CumprimentoObrigacaoLegal).Value!;
        await using AsyncServiceScope escopo = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        db.AddRange(elegivel, tipoDocumento);
        await db.SaveChangesAsync();
        await db.AprovarCatalogoAsync();

        return tipoDocumento.Id;
    }

    private async Task<ProcessoSeletivo> SemearProcessoAsync(string fato, bool faseDoFatoNoMesmoMomento)
    {
        await using AsyncServiceScope escopo = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, DocumentoAPublicar _) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, new OpcoesDoProcessoSeletivoPublicavel
        {
            Nome = $"Fato da fase seguinte {Guid.CreateVersion7()}",
            ColetaSolicitacaoIsencao = true,
            FasesAdicionais = [Criar(FaseDoFato, 2, 3, noMesmoMomentoDaAnterior: faseDoFatoNoMesmoMomento)],
            Complementar = p => ColetarOFatoNaIsencao(p, fato),
        });

        return processo;
    }

    private static void ColetarOFatoNaIsencao(ProcessoSeletivo processo, string fato)
    {
        Guid fase = processo.CronogramaFases.Single(f => f.Ordem == 1).Id;
        processo.DefinirFormulario(FinalidadeFormulario.IsencaoTaxa, fase, null, FormularioDeTeste.Etapas(FinalidadeFormulario.IsencaoTaxa)).IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(
                FinalidadeFormulario.IsencaoTaxa,
                [FatoColetadoDeTeste.Coletando(fato, 0)
                    .ComRotulo("Elegível")
                    .ComObrigatoriedade(Obrigatoriedade.Sempre)
                    .NaSecao(FormularioDeTeste.Secao)
                    .Construir()])
            .IsSuccess.Should().BeTrue();
    }
}
