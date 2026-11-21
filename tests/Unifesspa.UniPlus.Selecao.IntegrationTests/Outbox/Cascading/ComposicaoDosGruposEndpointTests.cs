namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;

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

[Collection(CascadingCollection.Name)]
public sealed class ComposicaoDosGruposEndpointTests(CascadingFixture fixture)
{
    [Fact]
    public async Task DefinirDocumentos_GruposComFasesEFormulariosDiferentes_AcumulaRecusas()
    {
        (Guid processo, Guid tipo, Guid fase) = await PrepararAsync();
        object primeiro = Folha(tipo, fase, "INSCRICAO");
        object segundo = Folha(tipo, Guid.CreateVersion7(), "ISENCAO_TAXA");
        object grupo = new { tipo = "E", filhos = new[] { primeiro, segundo } };
        using JsonDocument problema = await EnviarAsync(processo, [grupo, grupo]);

        JsonElement[] erros = [.. problema.RootElement.GetProperty("errors").EnumerateArray()];
        erros.Select(static e => e.GetProperty("field").GetString()).Should().Equal(
            "nos[0].filhos", "nos[0].filhos", "nos[1].filhos", "nos[1].filhos");
        Conferir(erros[0], "grupo_com_fases_diferentes", "No item 1 da lista: Os itens deste grupo pertencem a fases diferentes. Escolha a mesma fase para todos os itens do grupo.");
        Conferir(erros[1], "grupo_com_finalidades_diferentes", "No item 1 da lista: Os documentos deste grupo pertencem a formulários diferentes. Escolha o mesmo formulário para todos os documentos do grupo.");
    }

    [Fact]
    public async Task DefinirDocumentos_ItemRecusadoEGrupoIncoerente_AcumulaCausasIndependentes()
    {
        (Guid processo, Guid tipo, Guid fase) = await PrepararAsync();
        object grupo = new { tipo = "E", filhos = new[] { Folha(tipo, fase, "INSCRICAO"), Folha(tipo, Guid.CreateVersion7(), "ISENCAO_TAXA") } };
        using JsonDocument problema = await EnviarAsync(processo, [CasosDeRecusaDoNo.FolhaSemDocumento(), grupo]);

        JsonElement[] erros = [.. problema.RootElement.GetProperty("errors").EnumerateArray()];
        erros.Select(static e => e.GetProperty("field").GetString()).Should().Equal("nos[0].documento", "nos[1].filhos", "nos[1].filhos");
        erros.Select(static e => e.GetProperty("code").GetString()).Should().Equal(
            "uniplus.selecao.no_exigencia.documento_obrigatorio_em_folha",
            "uniplus.selecao.no_exigencia.grupo_com_fases_diferentes", "uniplus.selecao.no_exigencia.grupo_com_finalidades_diferentes");
    }

    [Fact]
    public async Task DefinirDocumentos_RepeticaoAninhada_ApontaOGrupo()
    {
        (Guid processo, Guid tipo, Guid fase) = await PrepararAsync();
        object interno = new { tipo = "E", repetePorEntidade = "PESSOAS", filhos = new[] { Folha(tipo, fase, "INSCRICAO") } };
        object externo = new { tipo = "E", repetePorEntidade = "PESSOAS", filhos = new[] { interno } };
        using JsonDocument problema = await EnviarAsync(processo, [externo]);

        JsonElement erro = problema.RootElement.GetProperty("errors").EnumerateArray().Single();
        erro.GetProperty("field").GetString().Should().Be("nos[0].repetePorEntidade");
        Conferir(erro, "repeticao_de_entidade_aninhada", "No item 1 da lista: Este grupo repete itens que já se repetem por outro grupo. Retire uma das repetições.");
    }

    private static void Conferir(JsonElement erro, string codigo, string mensagem)
    {
        erro.GetProperty("code").GetString().Should().Be($"uniplus.selecao.no_exigencia.{codigo}");
        erro.GetProperty("message").GetString().Should().Be(mensagem);
    }

    private async Task<(Guid Processo, Guid Tipo, Guid Fase)> PrepararAsync()
    {
        await using AsyncServiceScope escopo = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        TipoDocumento tipo = CriarTipoDeDocumento();
        db.Add(tipo);
        await db.SaveChangesAsync();
        await db.AprovarCatalogoAsync();
        (ProcessoSeletivo processo, DocumentoAPublicar _) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, new OpcoesDoProcessoSeletivoPublicavel
        {
            Nome = $"Composição de documentos {Guid.CreateVersion7()}",
            Complementar = ConfigurarGrupo,
        });
        return (processo.Id, tipo.Id, processo.CronogramaFases.Single().Id);
    }

    private static TipoDocumento CriarTipoDeDocumento() => TipoDocumento.Criar(
        $"COMPROVANTE_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}", "Comprovante", descricao: null, categoria: "IDENTIFICACAO",
        formatosAceitos: null, tamanhoMaximoMb: null, tipoEquivalente: null, classificacaoProtecao: ClassificacaoDeProtecao.Pessoal,
        finalidadeTratamento: "Comprovação documental.", hipoteseLegal: HipoteseLegalTratamento.CumprimentoObrigacaoLegal).Value!;

    private static void ConfigurarGrupo(ProcessoSeletivo processo)
    {
        FatoColetado fato = FatoColetadoDeTeste.Coletando("MEMBRO_TESTE", 0)
            .ComRotulo("Pessoa").ComObrigatoriedade(Obrigatoriedade.Sempre).Construir();
        GrupoColetado grupo = GrupoColetadoDeTeste.Agrupando("PESSOAS", 0)
            .ComRotulo("Pessoas").ComOcorrencias(1, 10).ComObrigatoriedade(Obrigatoriedade.Sempre).ComCampos(fato).Construir();
        processo.DefinirItens([], grupos: [grupo]).IsSuccess.Should().BeTrue();
    }

    private static object Folha(Guid tipo, Guid fase, string finalidade) => new
    {
        tipo = "FOLHA",
        documento = new
        {
            exigidoNaFaseId = fase,
            tipoDocumentoId = tipo,
            finalidade,
            aplicabilidade = "GERAL",
            obrigatorio = true,
            condicoes = Array.Empty<object>(),
            basesLegais = Array.Empty<object>(),
            formatosPermitidos = "QUALQUER",
        },
    };

    private async Task<JsonDocument> EnviarAsync(Guid processo, object[] nos)
    {
        using HttpClient client = fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Put, new Uri($"/api/selecao/processos-seletivos/{processo}/documentos-exigidos", UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        request.Content = JsonContent.Create(nos);
        using HttpResponseMessage resposta = await client.SendAsync(request);
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await resposta.Content.ReadAsStringAsync());
        return JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
    }
}
