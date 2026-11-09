namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;

/// <summary>A remoção de permissões pela API: vale na hora, contém a versão pendente e avisa quem governa o perfil.</summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class RemocaoDePermissoesNoHostTests : AprovacaoDaVersaoNoHostTestBase
{
    private const string AssuntoDaRemocao = "Permissões removidas";
    private const string AssuntoDoCancelamento = "cancelada pela remoção";
    private const string Motivo = "Permissão concedida por engano.";

    public RemocaoDePermissoesNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "A remoção vale na hora: a versão nova entra em vigor sem aprovação e a concessão acompanha")]
    public async Task Remocao_ValeNaHoraELevaAConcessao()
    {
        await ResetarAsync();
        using HttpClient diogo = await GestorAsync();
        Envio vigente = await PerfilVigenteAsync(diogo, Consultar, Sensivel);
        await SemearConcessaoAsync(Leandro, vigente.PerfilId, EscopoDaConcessao.DaUnidade(await SemearUnidadeAsync()));

        using HttpResponseMessage resposta = await RemoverBrutoAsync(diogo, vigente.PerfilId, [Sensivel]);

        JsonElement perfil = await CorpoAsync(resposta, HttpStatusCode.OK);
        perfil.GetProperty("composicaoEmVigor").GetProperty("numero").GetInt32().Should().Be(2);
        perfil.GetProperty("composicaoEmVigor").GetProperty("permissoes").EnumerateArray()
            .Select(p => p.GetProperty("codigo").GetString()).Should().Equal(Consultar);
        Guid novaVersao = perfil.GetProperty("composicaoEmVigor").GetProperty("versaoId").GetGuid();
        (await ConcessoesAsync(Leandro, vigente.PerfilId)).Should().ContainSingle().Which.VersaoId.Should().Be(novaVersao);
        (await VigenciasAsync(vigente.PerfilId)).Count(v => v.Aberta).Should().Be(1);
    }

    [Fact(DisplayName = "A remoção cancela a versão pendente e avisa quem a criou para submeter de novo")]
    public async Task Remocao_CancelaAPendenteEAvisaOCriador()
    {
        await ResetarAsync();
        using HttpClient diogo = await GestorAsync();
        using HttpClient betina = await ClienteComPermissoesAsync(Betina, AprovarFuncional);
        Envio vigente = await PerfilVigenteAsync(diogo, Consultar, Sensivel);
        Envio pendente = await CriarVersaoAsync(betina, vigente.PerfilId, Nome(), Consultar, Sensivel);

        using HttpResponseMessage resposta = await RemoverBrutoAsync(diogo, vigente.PerfilId, [Sensivel]);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        (await SituacaoDaVersaoNoBancoAsync(pendente.VersaoId)).Should().Be("cancelada");
        JsonElement aviso = await AguardarAvisoAsync(Betina, vigente.PerfilId, AssuntoDoCancelamento);
        aviso.GetProperty("Text").GetString().Should().NotContain(Motivo).And.NotContain(Diogo.Nome).And.NotContain(Diogo.Email);
    }

    [Fact(DisplayName = "A versão com o prazo vencido expira sem aviso quando a remoção chega")]
    public async Task Remocao_ExpiraAVencidaSemAviso()
    {
        await ResetarAsync();
        using HttpClient diogo = await GestorAsync();
        using HttpClient betina = await ClienteComPermissoesAsync(Betina, AprovarFuncional);
        using HttpClient isabella = await GestaoAsync();
        Envio vigente = await PerfilVigenteAsync(diogo, Consultar, Sensivel);
        Envio pendente = await CriarVersaoAsync(betina, vigente.PerfilId, Nome(), Consultar, Sensivel);
        await ExecutarSqlAsync("UPDATE autorizacao.versoes_do_perfil SET expira_em = now() WHERE id = {0}", pendente.VersaoId);

        using HttpResponseMessage resposta = await RemoverBrutoAsync(diogo, vigente.PerfilId, [Sensivel]);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        (await SituacaoDaVersaoNoBancoAsync(pendente.VersaoId)).Should().Be("expirada");
        await AguardarAvisoAsync(Isabella, vigente.PerfilId, AssuntoDaRemocao);
        await AguardarAvisoAsync(Betina, vigente.PerfilId, AssuntoDaRemocao);
        (await MensagensAsync(Betina, vigente.PerfilId)).Should().NotContain(m => m.GetProperty("Subject").GetString()!.Contains(AssuntoDoCancelamento, StringComparison.OrdinalIgnoreCase));
    }

    [Fact(DisplayName = "A remoção de permissão protegida avisa os gestores e a proteção de dados, sem motivo, nome nem e-mail")]
    public async Task RemocaoProtegida_AvisaGestoresEProtecaoDeDados()
    {
        await ResetarAsync();
        using HttpClient diogo = await GestorAsync();
        using HttpClient isabella = await GestaoAsync();
        using HttpClient marlene = await ProtecaoAsync();
        Envio vigente = await PerfilVigenteAsync(diogo, Consultar, Sensivel);

        (await RemoverBrutoAsync(diogo, vigente.PerfilId, [Sensivel])).Dispose();

        JsonElement paraAGestao = await AguardarAvisoAsync(Isabella, vigente.PerfilId, AssuntoDaRemocao);
        await AguardarAvisoAsync(Marlene, vigente.PerfilId, AssuntoDaRemocao);
        paraAGestao.GetProperty("Text").GetString().Should().NotContain(Motivo).And.NotContain(Diogo.Nome).And.NotContain(Diogo.Email);
        (await MensagensAsync(Diogo, vigente.PerfilId)).Should().BeEmpty("quem removeu não é avisado");
    }

    [Fact(DisplayName = "A remoção de permissão só interna não avisa a proteção de dados")]
    public async Task RemocaoInterna_NaoAvisaAProtecaoDeDados()
    {
        await ResetarAsync();
        using HttpClient diogo = await GestorAsync();
        using HttpClient isabella = await GestaoAsync();
        using HttpClient marlene = await ProtecaoAsync();
        Envio vigente = await PerfilVigenteAsync(diogo, Consultar, "teste:laudos:consultar");

        (await RemoverBrutoAsync(diogo, vigente.PerfilId, [Consultar])).Dispose();

        await AguardarAvisoAsync(Isabella, vigente.PerfilId, AssuntoDaRemocao);
        (await MensagensAsync(Marlene, vigente.PerfilId)).Should().BeEmpty();
    }

    [Fact(DisplayName = "A segunda remoção também permite ler o documento instituidor, que é o da proposta")]
    public async Task SegundaRemocao_LeODocumentoDaProposta()
    {
        await ResetarAsync();
        using HttpClient diogo = await GestorAsync();
        Envio vigente = await PerfilVigenteAsync(diogo, Consultar, Sensivel, "teste:laudos:consultar");
        (await RemoverBrutoAsync(diogo, vigente.PerfilId, [Sensivel])).Dispose();
        using HttpResponseMessage segunda = await RemoverBrutoAsync(diogo, vigente.PerfilId, ["teste:laudos:consultar"]);
        Guid versaoDaSegunda = (await CorpoAsync(segunda, HttpStatusCode.OK)).GetProperty("composicaoEmVigor").GetProperty("versaoId").GetGuid();

        using HttpResponseMessage documento = await diogo.GetAsync(
            new Uri($"{RotaPerfis}/{vigente.PerfilId}/versoes/{versaoDaSegunda}/documento", UriKind.Relative));

        documento.StatusCode.Should().Be(HttpStatusCode.OK, await documento.Content.ReadAsStringAsync());
    }

    [Fact(DisplayName = "A remoção total é válida e preserva o alcance do perfil")]
    public async Task RemocaoTotal_PreservaOAlcance()
    {
        await ResetarAsync();
        using HttpClient diogo = await GestorAsync();
        Envio vigente = await PerfilVigenteAsync(diogo, Consultar);

        using HttpResponseMessage resposta = await RemoverBrutoAsync(diogo, vigente.PerfilId, [Consultar]);

        JsonElement perfil = await CorpoAsync(resposta, HttpStatusCode.OK);
        perfil.GetProperty("composicaoEmVigor").GetProperty("permissoes").GetArrayLength().Should().Be(0);
        using HttpResponseMessage nova = await EnviarAsync(diogo, HttpMethod.Post, $"{RotaPerfis}/{vigente.PerfilId}/versoes", Dados(Nome(), Consultar));
        nova.StatusCode.Should().Be(HttpStatusCode.Created, await nova.Content.ReadAsStringAsync());
    }

    [Fact(DisplayName = "A remoção em perfil desativado é recusada")]
    public async Task PerfilInativo_Recusa()
    {
        await ResetarAsync();
        using HttpClient diogo = await GestorAsync();
        Envio vigente = await PerfilVigenteAsync(diogo, Consultar);
        (await EnviarAsync(diogo, HttpMethod.Delete, $"{RotaPerfis}/{vigente.PerfilId}/ativacao")).Dispose();

        using HttpResponseMessage resposta = await RemoverBrutoAsync(diogo, vigente.PerfilId, [Consultar]);

        await ConferirRecusaDoPerfilAsync(resposta, HttpStatusCode.UnprocessableEntity, "inativo");
    }

    [Theory(DisplayName = "A remoção inválida é recusada com o código tipado")]
    [InlineData("[]", "Motivo válido.", null, "remocao_invalida")]
    [InlineData("[\"teste:atendimento:analisar\"]", "Motivo válido.", null, "remocao_invalida")]
    [InlineData("[\"teste:inscricoes:operar\"]", "   ", null, "motivo_invalido")]
    [InlineData("[\"teste:inscricoes:operar\"]", "Motivo válido.", "categoria_que_nao_existe", "remocao_invalida")]
    public async Task RemocaoInvalida_Recusada(string permissoesJson, string motivo, string? categoria, string codigo)
    {
        await ResetarAsync();
        using HttpClient diogo = await GestorAsync();
        Envio vigente = await PerfilVigenteAsync(diogo, Consultar);
        string[] permissoes = JsonSerializer.Deserialize<string[]>(permissoesJson)!;

        using HttpResponseMessage resposta = await RemoverBrutoAsync(diogo, vigente.PerfilId, permissoes, motivo, categoria);

        await ConferirRecusaDoPerfilAsync(resposta, HttpStatusCode.UnprocessableEntity, codigo);
    }

    [Fact(DisplayName = "A remoção exige a permissão de manter perfis")]
    public async Task SemPermissao_Recusa()
    {
        await ResetarAsync();
        using HttpClient diogo = await GestorAsync();
        using HttpClient leandro = Cliente(Leandro);
        Envio vigente = await PerfilVigenteAsync(diogo, Consultar);

        using HttpResponseMessage resposta = await RemoverBrutoAsync(leandro, vigente.PerfilId, [Consultar]);

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
