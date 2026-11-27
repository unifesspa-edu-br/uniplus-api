namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Domain.Entities;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.ApoioDoCronogramaDeFasesPeloEndpoint;

/// <summary>
/// O processo em rascunho, com o tipo de documento e o dado do candidato de que a regra de documento precisa, e os passos
/// pelo HTTP que os testes das escolhas obrigatórias do documento exigido repetem: enviar as regras, ler o processo e conferir a recusa.
/// </summary>
internal sealed record CenarioDoDocumentoExigido(HttpClient Cliente, Guid ProcessoId, Guid TipoDeDocumentoId, Guid FaseId, string Dado)
{
    internal const string Obrigatorio = "uniplus.validacao.obrigatorio";

    private const string Caminho = "regrasDeComprovacao[0].documento";

    internal static async Task<CenarioDoDocumentoExigido> SemearAsync(CascadingFixture ambiente)
    {
        string sufixo = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        string dado = $"DADO_{sufixo}";
        TipoDocumento tipo = CriarTipoDeDocumento(sufixo);
        await using AsyncServiceScope escopo = ambiente.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        db.AddRange(CriarDado(dado), tipo);
        await db.SaveChangesAsync();
        await db.AprovarCatalogoAsync();
        ProcessoSeletivo processo = await SemearProcessoAsync(db, dado);
        return new(ambiente.Factory.CreateClient(), processo.Id, tipo.Id, processo.CronogramaFases.Single().Id, dado);
    }

    private static async Task<ProcessoSeletivo> SemearProcessoAsync(UniPlusDbContext db, string dado)
    {
        (ProcessoSeletivo processo, DocumentoAPublicar _) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, new OpcoesDoProcessoSeletivoPublicavel
        {
            Nome = $"Escolhas do documento exigido {Guid.CreateVersion7()}",
            ColetaSolicitacaoIsencao = true,
            Complementar = p => ColetarDado(p, dado),
        });
        return processo;
    }

    private static FatoCandidato CriarDado(string codigo) => CriacaoDoFato.CriarDoAdministrador(
        new DescritivoDoFato(codigo, "Dado do teste", "Descrição do dado."),
        new DefinicaoDoFato(TipoDoDado.SimOuNao, QuantidadeDeValores.ValorUnico, null, null, "RESULTADO_FINAL", EscopoFato.Candidato,
            ClassificacaoDeProtecao.Pessoal, "Comprovação documental", HipoteseLegalTratamento.CumprimentoObrigacaoLegal)).Value!;

    private static TipoDocumento CriarTipoDeDocumento(string sufixo) => TipoDocumento.Criar(new DeclaracaoDoTipoDocumento(
        $"COMPROVANTE_{sufixo}", "Comprovante", Descricao: null, Categoria: "IDENTIFICACAO", FormatosAceitos: null, TamanhoMaximoMb: null,
        TipoEquivalente: null, ClassificacaoProtecao: ClassificacaoDeProtecao.Pessoal, FinalidadeTratamento: "Comprovação documental.",
        HipoteseLegal: HipoteseLegalTratamento.CumprimentoObrigacaoLegal)).Value!;

    internal static string CampoDoIndeferimento(string campo) => $"{Caminho}.indeferimento.{campo}";

    internal const string CampoDoReenvioDoGrupo = "regrasDeComprovacao[0].indeferimento.abrePrazoParaReenvio";

    internal static string CampoDaCondicao(string campo) => $"{Caminho}.aplicabilidade.condicoes[0].{campo}";

    /// <summary>A regra de documento completa: sem bloco de grupo, indeferimento com as duas escolhas e uma condição sobre o dado do cenário.</summary>
    internal JsonObject Regra() => new()
    {
        ["forma"] = "DOCUMENTO",
        ["documento"] = new JsonObject
        {
            ["tipoDocumento"] = new JsonObject { ["id"] = TipoDeDocumentoId },
            ["momento"] = new JsonObject { ["faseId"] = FaseId, ["formularioCodigo"] = FormulariosDoProcessoDeTeste.PedidoDeIsencao },
            ["aplicabilidade"] = new JsonObject
            {
                ["tipo"] = "CONDICIONAL",
                ["condicoes"] = new JsonArray(new JsonObject { ["clausula"] = 0, ["fato"] = Dado, ["operador"] = "IGUAL", ["valor"] = "true" }),
            },
            ["indeferimento"] = new JsonObject { ["obrigatorio"] = true, ["abrePrazoParaReenvio"] = false, ["basesLegais"] = new JsonArray() },
            ["requisitos"] = new JsonObject { ["formatosPermitidos"] = "QUALQUER" },
        },
    };

    /// <summary>O grupo de todos os itens, com a regra de documento do cenário como único item e o indeferimento dado.</summary>
    internal JsonObject Grupo(JsonObject? indeferimento) => new()
    {
        ["forma"] = "TODOS_OS_ITENS",
        ["indeferimento"] = indeferimento,
        ["itens"] = new JsonArray(Regra()),
    };

    internal async Task<JsonArray> LerRegrasAsync() => (await LerProcessoComoJsonAsync(Cliente, ProcessoId))["regrasDeComprovacao"]!.AsArray();

    internal static JsonObject DocumentoDe(JsonObject regra) => regra["documento"]!.AsObject();

    internal async Task<HttpResponseMessage> EnviarAsync(JsonObject regra)
    {
        using HttpRequestMessage pedido = new(HttpMethod.Put, new Uri($"/api/selecao/processos-seletivos/{ProcessoId}/documentos-exigidos", UriKind.Relative))
        {
            Content = JsonContent.Create(new JsonArray(regra)),
        };
        Autenticar(pedido);
        pedido.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        return await Cliente.SendAsync(pedido);
    }

    internal async Task<JsonArray> LerDocumentosExigidosAsync() => (await LerProcessoComoJsonAsync(Cliente, ProcessoId))["documentosExigidos"]!.AsArray();

    internal async Task ExigirAceiteAsync(JsonObject regra)
    {
        using HttpResponseMessage resposta = await EnviarAsync(regra);
        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent, await resposta.Content.ReadAsStringAsync());
    }

    /// <summary>Exige a recusa 422 com cada código e campo esperados, na ordem, e que nada tenha sido gravado.</summary>
    internal async Task ExigirRecusaSemGravarAsync(JsonObject regra, params (string Campo, string Codigo)[] esperados)
    {
        using HttpResponseMessage resposta = await EnviarAsync(regra);
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await resposta.Content.ReadAsStringAsync());
        ErroDoProblema[] erros = await LerErrosDoProblemaAsync(resposta);
        erros.Select(static e => (Campo: e.Campo!, Codigo: e.Codigo!)).Should().Equal(esperados);
        (await LerDocumentosExigidosAsync()).Should().BeEmpty();
    }

    private static void ColetarDado(ProcessoSeletivo processo, string dado)
    {
        processo.DefinirFormularioSemConjuntoBasico(FormulariosDoProcessoDeTeste.PedidoDeIsencao, processo.CronogramaFases.Single().Id, obrigatorio: false);
        processo.DefinirFatosColetados(
                FormulariosDoProcessoDeTeste.Codigo(FormulariosDoProcessoDeTeste.PedidoDeIsencao),
                [FatoColetadoDeTeste.Coletando(dado, 0).ComRotulo("Dado do teste").ComObrigatoriedade(Obrigatoriedade.Sempre).NaSecao(FormularioDeTeste.Secao).Construir()])
            .IsSuccess.Should().BeTrue();
    }
}
