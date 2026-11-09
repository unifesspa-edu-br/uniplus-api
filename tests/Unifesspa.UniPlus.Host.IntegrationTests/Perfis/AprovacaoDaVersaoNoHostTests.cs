namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>A aprovação da versão do perfil pela API: quem decide, em que ordem, e o que cada recusa deixa gravado.</summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class AprovacaoDaVersaoNoHostTests : AprovacaoDaVersaoNoHostTestBase
{
    public AprovacaoDaVersaoNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "Quem criou a versão não a aprova")]
    public async Task QuemCriou_NaoAprova()
    {
        await ResetarAsync();
        using HttpClient diogo = await GestorAsync();
        using HttpClient betina = await ClienteComPermissoesAsync(Betina, AprovarFuncional);
        Envio vigente = await PerfilVigenteAsync(diogo, Consultar);
        Envio versao = await PendenteVerificadaAsync(betina, vigente.PerfilId, Consultar);

        using HttpResponseMessage recusa = await AprovarBrutoAsync(betina, versao, EtapaGestao);

        await ConferirRecusaDoPerfilAsync(recusa, HttpStatusCode.UnprocessableEntity, "decisor_e_autor");
        (await DecisoesAsync(versao.VersaoId)).Should().BeEmpty();
    }

    private const string Catalogo = "autorizacao:permissoes:consultar-catalogo";

    [Fact(DisplayName = "Gestor com concessão do perfil aprova a versão criada por outro, e a trilha registra as concessões")]
    public async Task GestorComConcessao_AprovaEATrilhaRegistra()
    {
        await ResetarAsync();
        using HttpClient diogo = await GestorAsync();
        using HttpClient betina = await ClienteComPermissoesAsync(Betina, AprovarFuncional);
        Envio vigente = await PerfilVigenteAsync(diogo, Catalogo);
        await SemearConcessaoAsync(Diogo, vigente.PerfilId, EscopoDaConcessao.DaInstituicao());
        await SemearConcessaoAsync(Betina, vigente.PerfilId, EscopoDaConcessao.DaInstituicao());
        Envio versao = await CriarVersaoAsync(betina, vigente.PerfilId, Nome(), Catalogo);
        await ConfirmarEnvioAsync(betina, versao);
        await InjetarResultadoAsync(versao.DocumentoId, VeredictoDaVerificacao.Verificado);
        await AguardarAsync(async () => await SituacaoDoDocumentoAsync(betina, vigente.PerfilId) == "verificado", "o documento não foi verificado");
        (await PerfilAsync(diogo, vigente.PerfilId)).GetProperty("versaoPendente").GetProperty("criadorTemConcessao").GetBoolean().Should().BeTrue();

        await AprovarAsync(diogo, versao, EtapaGestao);

        (await TemVigenteComVersaoAsync(diogo, vigente.PerfilId, versao.VersaoId)).Should().BeTrue();
        (await DecisoesAsync(versao.VersaoId)).Should().ContainSingle().Which.TemConcessao.Should().BeTrue();
    }

    [Fact(DisplayName = "A mesma pessoa não aprova como gestão e pela proteção de dados")]
    public async Task MesmaPessoa_NaoAprovaNasDuasEtapas()
    {
        await ResetarAsync();
        using HttpClient diogo = await GestorAsync();
        using HttpClient isabella = await GestoraComAmbasAsync();
        Envio vigente = await PerfilVigenteAsync(diogo, Consultar);
        Envio versao = await PendenteVerificadaAsync(diogo, vigente.PerfilId, Consultar, Sensivel);
        await AprovarAsync(isabella, versao, EtapaGestao);

        using HttpResponseMessage recusa = await AprovarBrutoAsync(isabella, versao, EtapaProtecao);

        await ConferirRecusaDoPerfilAsync(recusa, HttpStatusCode.UnprocessableEntity, "mesmo_decisor_nas_duas_etapas");
    }

    [Fact(DisplayName = "A proteção de dados não aprova antes da gestão")]
    public async Task ProtecaoAntesDaGestao_Recusada()
    {
        await ResetarAsync();
        using HttpClient diogo = await GestorAsync();
        using HttpClient marlene = await ProtecaoAsync();
        Envio vigente = await PerfilVigenteAsync(diogo, Consultar);
        Envio versao = await PendenteVerificadaAsync(diogo, vigente.PerfilId, Consultar, Sensivel);

        using HttpResponseMessage recusa = await AprovarBrutoAsync(marlene, versao, EtapaProtecao);

        await ConferirRecusaDoPerfilAsync(recusa, HttpStatusCode.UnprocessableEntity, "etapa_invalida");
        (await DecisoesAsync(versao.VersaoId)).Should().BeEmpty();
    }

    [Fact(DisplayName = "Acréscimo protegido passa pelas três pessoas, e a trilha guarda a identidade de cada uma")]
    public async Task CicloCompleto_TresPessoasNaTrilha()
    {
        await ResetarAsync();
        using HttpClient diogo = await GestorAsync();
        using HttpClient isabella = await GestaoAsync();
        using HttpClient marlene = await ProtecaoAsync();
        Envio vigente = await PerfilVigenteAsync(diogo, Consultar);
        Envio versao = await PendenteVerificadaAsync(diogo, vigente.PerfilId, Consultar, Sensivel);

        JsonElement aposAGestao = await AprovarAsync(isabella, versao, EtapaGestao);
        JsonElement pendente = aposAGestao.GetProperty("versaoPendente");
        pendente.GetProperty("etapaAtual").GetString().Should().Be(EtapaProtecao);
        pendente.GetProperty("decisoes").GetArrayLength().Should().Be(1);
        await AprovarAsync(marlene, versao, EtapaProtecao);

        (await TemVigenteComVersaoAsync(diogo, vigente.PerfilId, versao.VersaoId)).Should().BeTrue();
        DecisaoGravada[] trilha = await DecisoesAsync(versao.VersaoId);
        trilha.Select(d => d.Sub).Should().Equal(Isabella.Sub.ToString(), Marlene.Sub.ToString());
        trilha.Select(d => d.Resultado).Should().OnlyContain(r => r == "Aprovado");
    }

    [Fact(DisplayName = "A versão sem acréscimo protegido exige uma aprovação, a da gestão")]
    public async Task SemAcrescimoProtegido_UmaAprovacao()
    {
        await ResetarAsync();
        using HttpClient diogo = await GestorAsync();
        Envio vigente = await PerfilVigenteAsync(diogo, Consultar);
        Envio versao = await PendenteVerificadaAsync(diogo, vigente.PerfilId, Consultar);
        using HttpClient isabella = await GestaoAsync();

        JsonElement resposta = await AprovarAsync(isabella, versao, EtapaGestao);

        resposta.GetProperty("versaoPendente").ValueKind.Should().Be(JsonValueKind.Null);
        (await DecisoesAsync(versao.VersaoId)).Should().ContainSingle();
    }

    [Fact(DisplayName = "Aprovar antes de o documento ser verificado é recusado")]
    public async Task AntesDoDocumentoVerificado_Recusada()
    {
        await ResetarAsync();
        using HttpClient diogo = await GestorAsync();
        using HttpClient isabella = await GestaoAsync();
        Envio vigente = await PerfilVigenteAsync(diogo, Consultar);
        Envio versao = await CriarVersaoAsync(diogo, vigente.PerfilId, Nome(), Consultar);
        await ConfirmarEnvioAsync(diogo, versao);

        using HttpResponseMessage recusa = await AprovarBrutoAsync(isabella, versao, EtapaGestao);

        await ConferirRecusaDoPerfilAsync(recusa, HttpStatusCode.UnprocessableEntity, "documento_nao_verificado");
    }

    [Fact(DisplayName = "A recusa de uma etapa encerra a versão sem expor o motivo no perfil")]
    public async Task Recusa_EncerraASemExporOMotivo()
    {
        await ResetarAsync();
        using HttpClient diogo = await GestorAsync();
        using HttpClient isabella = await GestaoAsync();
        Envio vigente = await PerfilVigenteAsync(diogo, Consultar);
        Envio versao = await PendenteVerificadaAsync(diogo, vigente.PerfilId, Consultar, Sensivel);

        using HttpResponseMessage recusa = await RecusarBrutoAsync(isabella, versao, EtapaGestao, "O protocolo não cobre a permissão.");

        JsonElement perfil = await CorpoAsync(recusa, HttpStatusCode.OK);
        perfil.GetProperty("versaoPendente").ValueKind.Should().Be(JsonValueKind.Null);
        perfil.GetProperty("ultimaVersaoEncerradaSemVigor").GetProperty("situacao").GetString().Should().Be("recusada");
        perfil.GetRawText().Should().NotContain("O protocolo não cobre");
        (await TemVigenteComVersaoAsync(diogo, vigente.PerfilId, vigente.VersaoId)).Should().BeTrue();
    }

    [Fact(DisplayName = "A recusa sem motivo é recusada")]
    public async Task RecusaSemMotivo_Recusada()
    {
        await ResetarAsync();
        using HttpClient diogo = await GestorAsync();
        using HttpClient isabella = await GestaoAsync();
        Envio vigente = await PerfilVigenteAsync(diogo, Consultar);
        Envio versao = await PendenteVerificadaAsync(diogo, vigente.PerfilId, Consultar);

        using HttpResponseMessage recusa = await RecusarBrutoAsync(isabella, versao, EtapaGestao, "   ");

        await ConferirRecusaDoPerfilAsync(recusa, HttpStatusCode.UnprocessableEntity, "motivo_invalido");
    }
}
