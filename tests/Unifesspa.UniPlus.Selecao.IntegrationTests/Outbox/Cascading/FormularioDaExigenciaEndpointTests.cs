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
/// O formulário a que o documento exigido pertence, citado pelo código, pelo HTTP: na fase que divide a
/// inscrição e a isenção, o gatilho do documento entregue pela inscrição não cita o fato que só o pedido de
/// isenção, facultativo, coleta; o documento entregue pelo próprio pedido o cita.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class FormularioDaExigenciaEndpointTests
{
    private readonly CascadingFixture _fixture;

    public FormularioDaExigenciaEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "Na fase que divide inscrição e isenção, o documento entregue pela inscrição que cita fato só do pedido de isenção é recusado com 422")]
    public async Task DefinirDocumentosExigidos_DocumentoDaInscricaoCitaFatoDoPedidoDeIsencao_Recusa()
    {
        (ProcessoSeletivo processo, Guid tipoDocumentoId, string bolsista) = await SemearCenarioAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await PutDocumentosExigidosAsync(
            client, processo.Id, [RegraDoDocumentoCondicionadaA(bolsista, tipoDocumentoId, processo.CronogramaFases.Single().Id, FormulariosDoProcessoDeTeste.Inscricao)]);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await resposta.Content.ReadAsStringAsync());
    }

    [Fact(DisplayName = "Na fase que divide inscrição e isenção, o documento entregue pelo próprio pedido de isenção pode citar o fato que ele coleta")]
    public async Task DefinirDocumentosExigidos_DocumentoDoPedidoDeIsencaoCitaFatoDoPedido_Aceita()
    {
        (ProcessoSeletivo processo, Guid tipoDocumentoId, string bolsista) = await SemearCenarioAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await PutDocumentosExigidosAsync(
            client, processo.Id, [RegraDoDocumentoCondicionadaA(bolsista, tipoDocumentoId, processo.CronogramaFases.Single().Id, FormulariosDoProcessoDeTeste.PedidoDeIsencao)]);

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent, await resposta.Content.ReadAsStringAsync());
    }

    [Fact(DisplayName = "O documento exigido sem o código do formulário é recusado com 422 e o código do erro diz que o código é obrigatório")]
    public async Task DefinirDocumentosExigidos_SemCodigoDoFormulario_Recusa()
    {
        (ProcessoSeletivo processo, Guid tipoDocumentoId, string bolsista) = await SemearCenarioAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await PutDocumentosExigidosAsync(
            client, processo.Id, [RegraDoDocumentoCondicionadaA(bolsista, tipoDocumentoId, processo.CronogramaFases.Single().Id, formularioCodigo: null)]);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await resposta.Content.ReadAsStringAsync());
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("code").GetString().Should().Be("uniplus.selecao.formulario_processo.codigo_obrigatorio");
    }

    [Fact(DisplayName = "O documento exigido com o código do formulário ausente do corpo é recusado com 422 e o código do erro diz que o código é obrigatório")]
    public async Task DefinirDocumentosExigidos_CodigoDoFormularioAusenteDoCorpo_Recusa()
    {
        (ProcessoSeletivo processo, Guid tipoDocumentoId, string bolsista) = await SemearCenarioAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await PutDocumentosExigidosAsync(
            client, processo.Id, [RegraDoDocumentoComMomento(bolsista, tipoDocumentoId, new { faseId = processo.CronogramaFases.Single().Id })]);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await resposta.Content.ReadAsStringAsync());
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("code").GetString().Should().Be("uniplus.selecao.formulario_processo.codigo_obrigatorio");
    }

    [Fact(DisplayName = "O documento exigido que cita um formulário que o processo não tem é recusado com 422 e o código do erro diz que o formulário não existe")]
    public async Task DefinirDocumentosExigidos_FormularioInexistente_Recusa()
    {
        (ProcessoSeletivo processo, Guid tipoDocumentoId, string bolsista) = await SemearCenarioAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await PutDocumentosExigidosAsync(
            client, processo.Id, [RegraDoDocumentoCondicionadaA(bolsista, tipoDocumentoId, processo.CronogramaFases.Single().Id, "FORMULARIO_QUE_NAO_EXISTE")]);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await resposta.Content.ReadAsStringAsync());
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("code").GetString().Should().Be("uniplus.selecao.formulario_processo.exigencia_em_formulario_inexistente");
    }

    private async Task<(ProcessoSeletivo Processo, Guid TipoDocumentoId, string Bolsista)> SemearCenarioAsync()
    {
        string sufixo = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        string bolsista = $"BOLSISTA_{sufixo}";
        Guid tipoDocumentoId = await SemearCatalogoAsync(bolsista, $"COMPROVANTE_BOLSA_{sufixo}");
        return (await SemearProcessoAsync(bolsista), tipoDocumentoId, bolsista);
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

    private static object RegraDoDocumentoCondicionadaA(string fato, Guid tipoDocumentoId, Guid faseId, string? formularioCodigo) =>
        RegraDoDocumentoComMomento(fato, tipoDocumentoId, new { faseId, formularioCodigo });

    private static object RegraDoDocumentoComMomento(string fato, Guid tipoDocumentoId, object momento) => new
    {
        forma = "DOCUMENTO",
        documento = new
        {
            tipoDocumento = new { id = tipoDocumentoId },
            momento,
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

    /// <summary>O processo com a fase única dividida entre inscrição e isenção, e o fato que só o pedido de isenção coleta.</summary>
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
        processo.DefinirFormularioSemConjuntoBasico(
            FormulariosDoProcessoDeTeste.PedidoDeIsencao, processo.CronogramaFases.Single().Id, obrigatorio: false);
        processo.DefinirFatosColetados(
                FormulariosDoProcessoDeTeste.Codigo(FormulariosDoProcessoDeTeste.PedidoDeIsencao),
                [FatoColetadoDeTeste.Coletando(fato, 0)
                    .ComRotulo("Bolsista")
                    .ComObrigatoriedade(Obrigatoriedade.Sempre)
                    .NaSecao(FormularioDeTeste.Secao)
                    .Construir()])
            .IsSuccess.Should().BeTrue();
    }
}
