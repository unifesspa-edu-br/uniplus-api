namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;
using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;

/// <summary>
/// A troca de vigência e a decisão de uma etapa da retomada disputam a trava do perfil, e a composição em vigor no
/// instante da decisão define quantas etapas a suspensão exige. O teste segura a trava, enfileira as duas chamadas nas
/// duas ordens possíveis e confere que cada ordem termina num estado coerente.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class CorridasDaTrocaDeVigenciaComARetomadaNoHostTests : ContencaoDoPerfilNoHostTestBase
{
    public CorridasDaTrocaDeVigenciaComARetomadaNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "A troca para composição protegida vence a decisão da gestão: a suspensão continua aberta à espera da proteção de dados")]
    public async Task TrocaParaProtegida_AntesDaGestao_SuspensaoContinuaAberta()
    {
        (Guid perfilId, Envio acrescimo) = await SuspensoComAcrescimoSensivelEsperandoAProtecaoAsync();
        using HttpClient protecao = await ProtecaoAsync();
        using HttpClient betina = await ClienteComPermissoesAsync(Betina, AprovarFuncional);

        HttpResponseMessage[] respostas = await EmFilaSobATravaAsync(
            perfilId, () => AprovarBrutoAsync(protecao, acrescimo, EtapaProtecao), () => RetomarAsync(betina, perfilId, EtapaGestao));

        respostas.Select(r => r.StatusCode).Should().AllBeEquivalentTo(HttpStatusCode.OK);
        using HttpClient diogo = await GestorAsync();
        (await TiposDaContencaoAsync(diogo, perfilId)).Should().Equal("suspensao", "decisao_da_retomada");
        (await PermissoesDaConcessaoAsync(perfilId)).Should().BeEmpty();
        DescartarTodas(respostas);
    }

    [Fact(DisplayName = "A decisão da gestão vence a troca para composição protegida: a suspensão fecha e a troca já encontra o perfil retomado")]
    public async Task GestaoAntesDaTrocaParaProtegida_SuspensaoFecha()
    {
        (Guid perfilId, Envio acrescimo) = await SuspensoComAcrescimoSensivelEsperandoAProtecaoAsync();
        using HttpClient protecao = await ProtecaoAsync();
        using HttpClient betina = await ClienteComPermissoesAsync(Betina, AprovarFuncional);

        HttpResponseMessage[] respostas = await EmFilaSobATravaAsync(
            perfilId, () => RetomarAsync(betina, perfilId, EtapaGestao), () => AprovarBrutoAsync(protecao, acrescimo, EtapaProtecao));

        respostas.Select(r => r.StatusCode).Should().AllBeEquivalentTo(HttpStatusCode.OK);
        using HttpClient diogo = await GestorAsync();
        (await TiposDaContencaoAsync(diogo, perfilId)).Should().Equal("suspensao", "decisao_da_retomada", "retomada");
        (await PermissoesDaConcessaoAsync(perfilId)).Should().Equal(Consultar);
        DescartarTodas(respostas);
    }

    [Fact(DisplayName = "A troca que tira a proteção vence a decisão da proteção de dados: a troca fecha a suspensão e a decisão recebe não suspenso")]
    public async Task TrocaQueTiraAProtecao_AntesDaProtecao_FechaEARecusaADecisao()
    {
        await PrepararAsync();
        using HttpClient diogo = await GestorAsync();
        using HttpClient marlene = await ProtecaoAsync();
        Guid perfilId = await SuspensoProtegidoComGestaoDecididaAsync(diogo);

        HttpResponseMessage[] respostas = await EmFilaSobATravaAsync(
            perfilId, () => RemoverPermissoesAsync(diogo, perfilId, Consultar), () => RetomarAsync(marlene, perfilId, EtapaProtecao));

        respostas[0].StatusCode.Should().Be(HttpStatusCode.OK);
        await ConferirRecusaDoPerfilAsync(respostas[1], HttpStatusCode.UnprocessableEntity, "nao_suspenso");
        (await TiposDaContencaoAsync(diogo, perfilId)).Should().Equal("suspensao", "decisao_da_retomada", "retomada");
        (await TrilhaAsync(diogo, perfilId)).EnumerateArray().Last().GetProperty("origem").GetString().Should().Be("troca_de_vigencia");
        DescartarTodas(respostas);
    }

    [Fact(DisplayName = "A decisão da proteção de dados vence a troca que tira a proteção: a decisão fecha a suspensão e a troca não encontra suspensão")]
    public async Task ProtecaoAntesDaTrocaQueTiraAProtecao_DecisaoFecha()
    {
        await PrepararAsync();
        using HttpClient diogo = await GestorAsync();
        using HttpClient marlene = await ProtecaoAsync();
        Guid perfilId = await SuspensoProtegidoComGestaoDecididaAsync(diogo);

        HttpResponseMessage[] respostas = await EmFilaSobATravaAsync(
            perfilId, () => RetomarAsync(marlene, perfilId, EtapaProtecao), () => RemoverPermissoesAsync(diogo, perfilId, Consultar));

        respostas.Select(r => r.StatusCode).Should().AllBeEquivalentTo(HttpStatusCode.OK);
        (await TiposDaContencaoAsync(diogo, perfilId)).Should().Equal("suspensao", "decisao_da_retomada", "decisao_da_retomada", "retomada");
        (await TrilhaAsync(diogo, perfilId)).EnumerateArray().Last().GetProperty("origem").GetString().Should().Be("decisao_da_etapa");
        DescartarTodas(respostas);
    }

    private async Task<(Guid PerfilId, Envio Acrescimo)> SuspensoComAcrescimoSensivelEsperandoAProtecaoAsync()
    {
        await PrepararAsync();
        using HttpClient diogo = await GestorAsync();
        Guid perfilId = await PerfilDeUnidadeComConcessaoAsync(diogo, Consultar);
        await SuspenderComSucessoAsync(diogo, perfilId);
        Envio acrescimo = await PendenteVerificadaAsync(diogo, perfilId, Consultar, Sensivel);
        using HttpClient gestao = await GestaoAsync();
        await AprovarAsync(gestao, acrescimo, EtapaGestao);

        return (perfilId, acrescimo);
    }

    private async Task<Guid> SuspensoProtegidoComGestaoDecididaAsync(HttpClient diogo)
    {
        using HttpClient betina = await ClienteComPermissoesAsync(Betina, AprovarFuncional);
        Guid perfilId = await PerfilProtegidoComConcessaoAsync(diogo);
        await SuspenderComSucessoAsync(diogo, perfilId);
        await RetomarComSucessoAsync(betina, perfilId, EtapaGestao);

        return perfilId;
    }
}
