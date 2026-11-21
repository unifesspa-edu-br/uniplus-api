namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.CenarioDoRecursoPorVersao;

/// <summary>
/// Os parâmetros do recurso (efeito suspensivo, guarda da vaga, instância superior e prazos) são lidos da configuração congelada
/// na versão que vigora no instante da leitura, e não do cadastro vivo: o consumidor que abre o período de recurso, ou que publica o
/// indeferimento ao candidato, enxerga o que o edital declarava naquele instante. A versão passa a valer no instante gravado no
/// congelamento, inclusive nele: um microssegundo antes vale a anterior, e antes da primeira não vale nenhuma.
/// </summary>
[Collection(VersaoVigentePersistenciaColecao.Nome)]
public sealed class ConfiguracaoDoRecursoNaVersaoVigenteTests
{
    // O banco guarda o instante em microssegundos, e um microssegundo são dez ticks.
    private const long UmMicrossegundoEmTicks = 10;

    private readonly CenarioDoRecursoPorVersao _cenario;

    public ConfiguracaoDoRecursoNaVersaoVigenteTests(ProcessoSeletivoDbFixture fixture)
    {
        _cenario = new CenarioDoRecursoPorVersao(fixture);
    }

    [Fact(DisplayName = "No instante em que a primeira versão passa a valer, a leitura devolve os parâmetros do recurso congelados nela")]
    public async Task Ler_NaVigenciaDaPrimeira_DevolveOsParametrosDaPrimeira()
    {
        Guid processoId = await _cenario.PublicarAberturaEGravarSegundaVersaoAsync(nameof(Ler_NaVigenciaDaPrimeira_DevolveOsParametrosDaPrimeira));

        (int versao, ParametrosRegraPrazoRecurso parametros) = await _cenario.LerOsParametrosAsync(processoId, VigenciaDaPrimeira);

        versao.Should().Be(1);
        parametros.Should().Be(CongeladosNaPrimeira);
    }

    [Fact(DisplayName = "Um microssegundo antes da vigência da segunda versão, a leitura ainda devolve o prazo da 2ª instância congelado na primeira")]
    public async Task Ler_UmMicrossegundoAntesDaVigenciaDaSegunda_DevolveOsParametrosDaPrimeira()
    {
        Guid processoId = await _cenario.PublicarAberturaEGravarSegundaVersaoAsync(nameof(Ler_UmMicrossegundoAntesDaVigenciaDaSegunda_DevolveOsParametrosDaPrimeira));

        (int versao, ParametrosRegraPrazoRecurso parametros) = await _cenario.LerOsParametrosAsync(
            processoId, VigenciaDaSegunda.AddTicks(-UmMicrossegundoEmTicks));

        versao.Should().Be(1);
        parametros.Should().Be(CongeladosNaPrimeira);
        parametros.PrazoDaSegundaInstancia.Should().NotBe(CongeladosNaSegunda.PrazoDaSegundaInstancia);
    }

    [Fact(DisplayName = "Exatamente no instante em que a segunda versão passa a valer, a leitura devolve o prazo da 2ª instância congelado nela")]
    public async Task Ler_ExatamenteNaVigenciaDaSegunda_DevolveOsParametrosDaSegunda()
    {
        Guid processoId = await _cenario.PublicarAberturaEGravarSegundaVersaoAsync(nameof(Ler_ExatamenteNaVigenciaDaSegunda_DevolveOsParametrosDaSegunda));

        (int versao, ParametrosRegraPrazoRecurso parametros) = await _cenario.LerOsParametrosAsync(processoId, VigenciaDaSegunda);

        versao.Should().Be(2);
        parametros.Should().Be(CongeladosNaSegunda);
        parametros.PrazoDaSegundaInstancia.Should().NotBe(CongeladosNaPrimeira.PrazoDaSegundaInstancia);
    }

    [Fact(DisplayName = "Depois da vigência da segunda versão, a leitura continua devolvendo os parâmetros do recurso congelados nela")]
    public async Task Ler_DepoisDaVigenciaDaSegunda_DevolveOsParametrosDaSegunda()
    {
        Guid processoId = await _cenario.PublicarAberturaEGravarSegundaVersaoAsync(nameof(Ler_DepoisDaVigenciaDaSegunda_DevolveOsParametrosDaSegunda));

        (int versao, ParametrosRegraPrazoRecurso parametros) = await _cenario.LerOsParametrosAsync(processoId, VigenciaDaSegunda.AddDays(30));

        versao.Should().Be(2);
        parametros.Should().Be(CongeladosNaSegunda);
    }

    [Fact(DisplayName = "No instante em que a primeira versão passa a valer, a leitura devolve os parâmetros do recurso da etapa congelados nela")]
    public async Task LerDaEtapa_NaVigenciaDaPrimeira_DevolveOsParametrosDaPrimeira()
    {
        Guid processoId = await _cenario.PublicarAberturaEGravarSegundaVersaoAsync(nameof(LerDaEtapa_NaVigenciaDaPrimeira_DevolveOsParametrosDaPrimeira));

        (int versao, ParametrosRegraPrazoRecurso parametros) = await _cenario.LerOsParametrosDaEtapaAsync(processoId, VigenciaDaPrimeira);

        versao.Should().Be(1);
        parametros.Should().Be(CongeladosDaEtapaNaPrimeira);
    }

    [Fact(DisplayName = "Um microssegundo antes da vigência da segunda versão, a leitura ainda devolve os parâmetros do recurso da etapa congelados na primeira")]
    public async Task LerDaEtapa_UmMicrossegundoAntesDaVigenciaDaSegunda_DevolveOsParametrosDaPrimeira()
    {
        Guid processoId = await _cenario.PublicarAberturaEGravarSegundaVersaoAsync(nameof(LerDaEtapa_UmMicrossegundoAntesDaVigenciaDaSegunda_DevolveOsParametrosDaPrimeira));

        (int versao, ParametrosRegraPrazoRecurso parametros) = await _cenario.LerOsParametrosDaEtapaAsync(
            processoId, VigenciaDaSegunda.AddTicks(-UmMicrossegundoEmTicks));

        versao.Should().Be(1);
        parametros.Should().Be(CongeladosDaEtapaNaPrimeira);
        parametros.Should().NotBe(CongeladosDaEtapaNaSegunda);
    }

    [Fact(DisplayName = "Exatamente no instante em que a segunda versão passa a valer, a leitura devolve os parâmetros do recurso da etapa congelados nela")]
    public async Task LerDaEtapa_ExatamenteNaVigenciaDaSegunda_DevolveOsParametrosDaSegunda()
    {
        Guid processoId = await _cenario.PublicarAberturaEGravarSegundaVersaoAsync(nameof(LerDaEtapa_ExatamenteNaVigenciaDaSegunda_DevolveOsParametrosDaSegunda));

        (int versao, ParametrosRegraPrazoRecurso parametros) = await _cenario.LerOsParametrosDaEtapaAsync(processoId, VigenciaDaSegunda);

        versao.Should().Be(2);
        parametros.Should().Be(CongeladosDaEtapaNaSegunda);
        parametros.Should().NotBe(CongeladosDaEtapaNaPrimeira);
    }

    [Fact(DisplayName = "Depois da vigência da segunda versão, a leitura continua devolvendo os parâmetros do recurso da etapa congelados nela")]
    public async Task LerDaEtapa_DepoisDaVigenciaDaSegunda_DevolveOsParametrosDaSegunda()
    {
        Guid processoId = await _cenario.PublicarAberturaEGravarSegundaVersaoAsync(nameof(LerDaEtapa_DepoisDaVigenciaDaSegunda_DevolveOsParametrosDaSegunda));

        (int versao, ParametrosRegraPrazoRecurso parametros) = await _cenario.LerOsParametrosDaEtapaAsync(processoId, VigenciaDaSegunda.AddDays(30));

        versao.Should().Be(2);
        parametros.Should().Be(CongeladosDaEtapaNaSegunda);
    }

    [Fact(DisplayName = "Um microssegundo antes da primeira versão, não há versão vigente e nenhum parâmetro do recurso, da fase ou da etapa, é devolvido")]
    public async Task Ler_AntesDaPrimeiraVersao_NaoTemVersaoVigente()
    {
        Guid processoId = await _cenario.PublicarAberturaEGravarSegundaVersaoAsync(nameof(Ler_AntesDaPrimeiraVersao_NaoTemVersaoVigente));

        Result<ConfiguracaoVigente> lida = await _cenario.LerAsync(processoId, VigenciaDaPrimeira.AddTicks(-UmMicrossegundoEmTicks));

        lida.IsFailure.Should().BeTrue("antes da primeira publicação nenhuma configuração vigora, e a leitura não recorre a versão posterior");
        lida.Error!.Code.Should().Be("Snapshot.VigenteAusente");
    }
}
