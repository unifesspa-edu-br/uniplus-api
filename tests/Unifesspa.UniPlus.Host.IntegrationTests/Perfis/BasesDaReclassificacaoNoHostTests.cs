namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;
using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// As bases das permissões nas concessões pelo host real: a descida só deixa trilha, a nova subida suspende de novo,
/// a mudança de escopo suspende a cada mudança, e a troca de versão mantém, encerra ou não traz a base.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class BasesDaReclassificacaoNoHostTests : ReclassificacaoNoHostTestBase
{
    public BasesDaReclassificacaoNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "A descida de classe só grava a trilha e a base conciliada: não suspende, não avisa, e a instância antiga recusa a leitura")]
    public async Task Descida_SoGravaATrilha()
    {
        Cenario perfil = await CenarioAsync([Sensivel], Leandro);

        await ImplantarAsync(Sensivel, ClassificacaoDeProtecao.Interno);
        await ProcessarAvisosAsync();

        (await ReclassificacoesAsync(perfil.Perfil)).Should().Be(1);
        (await BasesDaOrigemAsync(perfil.Perfil, "conciliacao")).Should().Be(1);
        (await AvisosAsync(perfil.Perfil)).Should().Be(0);
        await FluentActions.Awaiting(() => PermissoesAsync(Leandro, perfil.Perfil)).Should().ThrowAsync<CatalogoDesatualizadoException>();
    }

    [Fact(DisplayName = "Subida, liberação, descida e nova subida: a permissão é suspensa de novo")]
    public async Task SubidaLiberacaoDescidaESubida_SuspendeDeNovo()
    {
        Cenario perfil = await CenarioAsync([Sensivel], Leandro);
        await SubirAClasseAsync();
        await LiberarAsync(Marlene, EtapaProtecao, perfil.Perfil, await ReclassificacaoAsync(perfil.Perfil, Sensivel));
        (await PermissoesAsync(Leandro, perfil.Perfil)).Should().Equal(Sensivel);

        await SubirAClasseAsync();

        (await PermissoesAsync(Leandro, perfil.Perfil)).Should().BeEmpty();
    }

    [Theory(DisplayName = "Escopo A, B e A de novo: a permissão fica suspensa a cada mudança, com ou sem liberação no meio")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EscopoAParaBParaA_SuspendeACadaMudanca(bool comLiberacao)
    {
        Cenario perfil = await CenarioAsync([Consultar], Leandro);
        await MudarOEscopoEVoltarAsync();
        if (comLiberacao)
        {
            await LiberarAsync(Isabella, EtapaGestao, perfil.Perfil, await ReclassificacaoAsync(perfil.Perfil, Consultar));
            (await PermissoesAsync(Leandro, perfil.Perfil)).Should().Equal(Consultar);
        }

        await MudarOEscopoEVoltarAsync();

        (await PermissoesAsync(Leandro, perfil.Perfil)).Should().BeEmpty();
    }

    [Fact(DisplayName = "A concessão criada depois da troca numa versão antiga fica suspensa")]
    public async Task ConcessaoNaVersaoAntiga_FicaSuspensa()
    {
        Cenario perfil = await CenarioAsync([Consultar, Sensivel], Leandro);
        await RebaixarRetratoAsync(perfil.Perfil, 1, Sensivel, "interno");

        Guid nova = await ConcederDepoisAsync(perfil, Betina);

        nova.Should().NotBeEmpty();
        (await PermissoesAsync(Betina, perfil.Perfil)).Should().Equal(Consultar);
    }

    [Fact(DisplayName = "A concessão numa versão criada depois da troca não fica suspensa e a migração mantém a base da antiga")]
    public async Task VersaoNova_NaoSuspendeEAMigracaoMantemABase()
    {
        Cenario perfil = await CenarioAsync([Consultar, Sensivel], Leandro);
        await RebaixarRetratoAsync(perfil.Perfil, 1, Consultar, "publico");

        await TrocarComposicaoAsync(perfil.Perfil, Consultar);
        await RebaixarRetratoAsync(perfil.Perfil, 2, Consultar, "interno");
        await ConcederDepoisAsync(perfil, Betina);

        (await PermissoesAsync(Betina, perfil.Perfil)).Should().Equal(Consultar);
        (await PermissoesAsync(Leandro, perfil.Perfil)).Should().BeEmpty();
        (await OrigemDaBaseAsync(perfil.De(Leandro), Consultar)).Should().Be("migracao");
    }

    [Fact(DisplayName = "O código removido e reintroduzido usa o retrato da versão que o reintroduziu, e não a base antiga")]
    public async Task CodigoReintroduzido_UsaORetratoNovo()
    {
        Cenario perfil = await CenarioAsync([Consultar, Sensivel], Leandro);
        await RebaixarRetratoAsync(perfil.Perfil, 1, Consultar, "publico");

        await TrocarComposicaoAsync(perfil.Perfil, Sensivel);
        await TrocarComposicaoAsync(perfil.Perfil, Sensivel, Consultar);
        await MigrarNaJanelaAsync(perfil.Perfil, perfil.De(Leandro));

        (await OrigemDaBaseAsync(perfil.De(Leandro), Consultar)).Should().Be("saida");
        (await PermissoesAsync(Leandro, perfil.Perfil)).Should().BeEquivalentTo(Consultar, Sensivel);
    }

    private async Task MigrarNaJanelaAsync(Guid perfil, Guid concessao)
    {
        using HttpClient gestao = await GestaoAsync();
        using HttpResponseMessage lote = await EnviarAsync(
            gestao, HttpMethod.Post, $"{RotaPerfis}/{perfil}/janela/lote", new { decisoes = new[] { new { concessaoId = concessao, decisao = "migrar" } }, confirmacaoDeExclusao = false });
        lote.StatusCode.Should().Be(HttpStatusCode.OK, await lote.Content.ReadAsStringAsync());
    }

    private async Task MudarOEscopoEVoltarAsync()
    {
        await ImplantarAsync(Consultar, ClassificacaoDeProtecao.Interno, deUnidade: false);
        await ImplantarRealAsync();
    }
}
