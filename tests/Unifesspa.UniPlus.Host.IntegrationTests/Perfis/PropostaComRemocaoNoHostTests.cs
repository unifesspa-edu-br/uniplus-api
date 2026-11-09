namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;

/// <summary>
/// A proposta de versão que retira permissões: a remoção vale na hora, o que sobra da proposta fica pendente, e as duas
/// coisas entram juntas ou não entram.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class PropostaComRemocaoNoHostTests : AprovacaoDaVersaoNoHostTestBase
{
    private const string Acrescimo = "teste:laudos:consultar";
    private const string Motivo = "Permissão concedida por engano.";

    public PropostaComRemocaoNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "A proposta que remove e acrescenta aplica a remoção na hora e deixa pendente só o acréscimo")]
    public async Task RemoveEAcrescenta_RemocaoNaHoraEPendenteSoComOAcrescimo()
    {
        await ResetarAsync();
        using HttpClient diogo = await GestorAsync();
        Envio vigente = await PerfilVigenteAsync(diogo, Consultar, Sensivel);

        using HttpResponseMessage resposta = await ProporAsync(diogo, vigente.PerfilId, Motivo, Consultar, Acrescimo);

        Envio pendente = LerEnvio(await CorpoAsync(resposta, HttpStatusCode.Created));
        JsonElement perfil = await PerfilAsync(diogo, vigente.PerfilId);
        perfil.GetProperty("composicaoEmVigor").GetProperty("numero").GetInt32().Should().Be(2);
        perfil.GetProperty("composicaoEmVigor").GetProperty("permissoes").EnumerateArray()
            .Select(p => p.GetProperty("codigo").GetString()).Should().Equal(Consultar);
        (await SituacaoDaVersaoNoBancoAsync(pendente.VersaoId)).Should().Be("pendente");
        (await VigenciasAsync(vigente.PerfilId)).Count(v => v.Aberta).Should().Be(1);
    }

    [Fact(DisplayName = "A proposta só com remoção vale na hora, devolve o perfil e não cria versão pendente")]
    public async Task SoRemocao_ValeNaHoraSemPendente()
    {
        await ResetarAsync();
        using HttpClient diogo = await GestorAsync();
        Envio vigente = await PerfilVigenteAsync(diogo, Consultar, Sensivel);
        string nome = (await PerfilAsync(diogo, vigente.PerfilId)).GetProperty("nome").GetString()!;

        using HttpResponseMessage resposta = await ProporComNomeAsync(diogo, vigente.PerfilId, Motivo, nome, [Consultar]);

        JsonElement perfil = await CorpoAsync(resposta, HttpStatusCode.OK);
        perfil.GetProperty("composicaoEmVigor").GetProperty("numero").GetInt32().Should().Be(2);
        perfil.GetProperty("composicaoEmVigor").GetProperty("permissoes").GetArrayLength().Should().Be(1);
        (!perfil.TryGetProperty("versaoPendente", out JsonElement pendente) || pendente.ValueKind == JsonValueKind.Null).Should().BeTrue();
    }

    [Fact(DisplayName = "A proposta que remove sem motivo é recusada com o código de motivo inválido e nada muda")]
    public async Task RemoveSemMotivo_Recusada()
    {
        await ResetarAsync();
        using HttpClient diogo = await GestorAsync();
        Envio vigente = await PerfilVigenteAsync(diogo, Consultar, Sensivel);

        using HttpResponseMessage resposta = await ProporAsync(diogo, vigente.PerfilId, null, Consultar, Acrescimo);

        await ConferirRecusaDoPerfilAsync(resposta, HttpStatusCode.UnprocessableEntity, "motivo_invalido");
        (await PerfilAsync(diogo, vigente.PerfilId)).GetProperty("composicaoEmVigor").GetProperty("numero").GetInt32().Should().Be(1);
    }

    [Fact(DisplayName = "Se a pendente é recusada a remoção também não acontece")]
    public async Task PendenteRecusada_DesfazARemocao()
    {
        await ResetarAsync();
        using HttpClient diogo = await GestorAsync();
        Envio vigente = await PerfilVigenteAsync(diogo, Consultar, Sensivel);
        await SemearConcessaoAsync(Leandro, vigente.PerfilId, EscopoDaConcessao.DaUnidade(await SemearUnidadeAsync()));

        using HttpResponseMessage resposta = await ProporAsync(diogo, vigente.PerfilId, Motivo, Consultar, Acrescimo);

        await ConferirRecusaDoPerfilAsync(resposta, HttpStatusCode.UnprocessableEntity, "ampliacao_exige_protecao_de_dados");
        JsonElement perfil = await PerfilAsync(diogo, vigente.PerfilId);
        perfil.GetProperty("composicaoEmVigor").GetProperty("numero").GetInt32().Should().Be(1);
        perfil.GetProperty("composicaoEmVigor").GetProperty("permissoes").GetArrayLength().Should().Be(2);
        (await ConcessoesAsync(Leandro, vigente.PerfilId)).Should().ContainSingle().Which.VersaoId.Should().Be(vigente.VersaoId);
    }

    private static Task<HttpResponseMessage> ProporAsync(HttpClient cliente, Guid perfilId, string? motivo, params string[] permissoes) =>
        ProporComNomeAsync(cliente, perfilId, motivo, Nome(), permissoes);

    private static Task<HttpResponseMessage> ProporComNomeAsync(HttpClient cliente, Guid perfilId, string? motivo, string nome, string[] permissoes) =>
        EnviarAsync(cliente, HttpMethod.Post, $"{RotaPerfis}/{perfilId}/versoes", DadosComRemocao(nome, motivo, permissoes));
}
