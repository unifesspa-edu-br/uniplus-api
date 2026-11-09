namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>A entrada em vigor da versão: troca de vigência sob os índices parciais, resultado repetido e as recusas que dependem do nome e das concessões.</summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class VigenciaDaVersaoDoPerfilNoHostTests : VersoesDoPerfilNoHostTestBase
{
    public VigenciaDaVersaoDoPerfilNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "O resultado repetido não abre outra vigência nem grava de novo")]
    public async Task VeredictoRepetido_NaoAbreOutraVigencia()
    {
        await ResetarAsync();
        using HttpClient gestor = await GestorAsync();
        Envio envio = await CriarPerfilAsync(gestor, Consultar);
        await EnviarEVerificarAsync(gestor, envio);

        DesfechoDoResultadoDaVersao desfecho = await RegistrarDiretoAsync(envio.DocumentoId);

        desfecho.Should().Be(DesfechoDoResultadoDaVersao.Ignorado);
        (await VigenciasAsync(envio.PerfilId)).Should().ContainSingle();
    }

    [Fact(DisplayName = "A troca de vigência encerra a anterior e abre a nova sob os índices parciais")]
    public async Task TrocaDeVigencia_FuncionaSobOsIndicesParciais()
    {
        await ResetarAsync();
        using HttpClient gestor = await GestorAsync();
        Envio primeira = await CriarPerfilAsync(gestor, Consultar);
        await EnviarEVerificarAsync(gestor, primeira);
        Envio segunda = await CriarVersaoAsync(gestor, primeira.PerfilId, Nome(), Consultar, Sensivel);

        await EnviarEVerificarAsync(gestor, segunda);

        JsonElement perfil = await LerAsync(gestor, $"{RotaPerfis}/{primeira.PerfilId}");
        perfil.GetProperty("composicaoEmVigor").GetProperty("numero").GetInt32().Should().Be(2);
        (bool Aberta, Guid VersaoId)[] vigencias = await VigenciasAsync(primeira.PerfilId);
        vigencias.Should().HaveCount(2);
        vigencias.Count(v => v.Aberta).Should().Be(1);
        vigencias.Single(v => v.Aberta).VersaoId.Should().Be(segunda.VersaoId);
    }

    [Fact(DisplayName = "Nome tomado por outro perfil enquanto a versão esperava a aprovação recusa a decisão e a pendente segue")]
    public async Task CorridaDeNome_RecusaAAprovacaoEMantemAPendente()
    {
        await ResetarAsync();
        using HttpClient gestor = await GestorAsync();
        Envio original = await CriarPerfilAsync(gestor, Consultar);
        await EnviarEVerificarAsync(gestor, original);
        string nomeNovo = Nome();
        Envio versao = await CriarVersaoAsync(gestor, original.PerfilId, nomeNovo, Consultar);
        await ConfirmarEnvioAsync(gestor, versao);
        (await EnviarAsync(gestor, HttpMethod.Post, RotaPerfis, Dados(nomeNovo, Consultar))).StatusCode.Should().Be(HttpStatusCode.Created);
        await InjetarResultadoAsync(versao.DocumentoId, VeredictoDaVerificacao.Verificado);
        await AguardarAsync(async () => await SituacaoDoDocumentoAsync(gestor, original.PerfilId) == "verificado", "o documento não foi verificado");

        using HttpClient gestao = await GestaoAsync();
        using HttpResponseMessage recusa = await AprovarBrutoAsync(gestao, versao, EtapaGestao);

        await ConferirRecusaDoPerfilAsync(recusa, HttpStatusCode.Conflict, "nome_duplicado");
        JsonElement perfil = await LerAsync(gestor, $"{RotaPerfis}/{original.PerfilId}");
        perfil.GetProperty("composicaoEmVigor").GetProperty("numero").GetInt32().Should().Be(1);
        perfil.GetProperty("versaoPendente").GetProperty("versaoId").GetGuid().Should().Be(versao.VersaoId);
    }

    [Fact(DisplayName = "Concessão surgida antes da aprovação não impede a vigência da versão que amplia: abre a janela de migração")]
    public async Task ConcessaoAntesDaAprovacao_EntraEmVigorEAbreAJanela()
    {
        await ResetarAsync();
        using HttpClient gestor = await GestorAsync();
        Envio original = await CriarPerfilAsync(gestor, Consultar);
        await EnviarEVerificarAsync(gestor, original);
        Envio ampliada = await CriarVersaoAsync(gestor, original.PerfilId, Nome(), Consultar, Sensivel);
        await ConfirmarEnvioAsync(gestor, ampliada);
        await InjetarResultadoAsync(ampliada.DocumentoId, VeredictoDaVerificacao.Verificado);
        await AguardarAsync(async () => await SituacaoDoDocumentoAsync(gestor, original.PerfilId) == "verificado", "o documento não foi verificado");
        await SemearConcessaoAsync(Leandro, original.PerfilId, EscopoDaConcessao.DaUnidade(await SemearUnidadeAsync()));

        await AprovarAteAVigenciaAsync(gestor, ampliada);

        JsonElement perfil = await LerAsync(gestor, $"{RotaPerfis}/{original.PerfilId}");
        perfil.GetProperty("composicaoEmVigor").GetProperty("numero").GetInt32().Should().Be(2);
        perfil.GetProperty("janela").GetProperty("situacao").GetString().Should().Be("aberta");
    }

    [Fact(DisplayName = "Concessão posterior à entrada em vigor não impede a próxima ampliação: a versão é criada pendente")]
    public async Task ConcessaoDepoisDaVigencia_AceitaANovaAmpliacao()
    {
        await ResetarAsync();
        using HttpClient gestor = await GestorAsync();
        Envio original = await CriarPerfilAsync(gestor, Consultar);
        await EnviarEVerificarAsync(gestor, original);
        await SemearConcessaoAsync(Leandro, original.PerfilId, EscopoDaConcessao.DaUnidade(await SemearUnidadeAsync()));

        using HttpResponseMessage resposta = await EnviarAsync(
            gestor, HttpMethod.Post, $"{RotaPerfis}/{original.PerfilId}/versoes", Dados(Nome(), Consultar, Sensivel));

        resposta.StatusCode.Should().Be(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync());
    }

    [Fact(DisplayName = "A entrada em vigor sem acréscimo leva a concessão e o pedido ativo para a versão nova")]
    public async Task EntradaEmVigorSemAcrescimo_MigraConcessaoEPedido()
    {
        await ResetarAsync();
        using HttpClient gestor = await GestorAsync();
        Envio original = await CriarPerfilAsync(gestor, Consultar, Sensivel);
        await EnviarEVerificarAsync(gestor, original);
        Guid unidade = await SemearUnidadeAsync();
        await SemearConcessaoAsync(Leandro, original.PerfilId, EscopoDaConcessao.DaUnidade(unidade));
        using HttpClient betina = Cliente(Betina);
        Guid pedido = await CriarAsync(betina, original.PerfilId, unidade, null);

        using HttpResponseMessage remocao = await RemoverBrutoAsync(gestor, original.PerfilId, [Sensivel]);
        JsonElement reduzida = await CorpoAsync(remocao, HttpStatusCode.OK);

        (await ConcessoesAsync(Leandro, original.PerfilId)).Should().ContainSingle().Which.VersaoId
            .Should().Be(reduzida.GetProperty("composicaoEmVigor").GetProperty("versaoId").GetGuid());
        JsonElement lido = await LerAsync(betina, $"{Rota}/{pedido}");
        lido.GetProperty("situacao").GetString().Should().Be("aguardando_gestor");
        lido.GetProperty("versao").GetInt32().Should().Be(2);
        lido.GetProperty("trocasDeVersao").EnumerateArray().Single().GetProperty("desfecho").GetString().Should().Be("versao_atualizada");
    }

    [Fact(DisplayName = "A entrada em vigor com acréscimo devolve o pedido ativo e avisa o solicitante")]
    public async Task EntradaEmVigorComAcrescimo_DevolveOPedido()
    {
        await ResetarAsync();
        using HttpClient gestor = await GestorAsync();
        Envio original = await CriarPerfilAsync(gestor, Consultar);
        await EnviarEVerificarAsync(gestor, original);
        Guid unidade = await SemearUnidadeAsync();
        using HttpClient betina = Cliente(Betina);
        Guid pedido = await CriarAsync(betina, original.PerfilId, unidade, null);
        Envio ampliada = await CriarVersaoAsync(gestor, original.PerfilId, Nome(), Consultar, Sensivel);

        await EnviarEVerificarAsync(gestor, ampliada);

        JsonElement lido = await LerAsync(betina, $"{Rota}/{pedido}");
        lido.GetProperty("situacao").GetString().Should().Be("devolvido");
        lido.GetProperty("versao").GetInt32().Should().Be(1);
        lido.GetProperty("trocasDeVersao").EnumerateArray().Single().GetProperty("desfecho").GetString().Should().Be("devolvido");
    }
}
