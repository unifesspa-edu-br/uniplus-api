namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using Xunit;

using static CenarioDeFasesDoProcessoPublicado;

/// <summary>
/// O instante da janela que a versão vigente guarda fora da forma canônica do envelope não passa por "sem janela":
/// quem lê as fases iniciadas ou a unidade da fase falha, e a fase não aparece como não iniciada.
/// </summary>
public sealed class InstanteForaDaFormaNaVersaoVigenteTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private readonly ProcessoSeletivoDbFixture _fixture;
    private readonly CenarioDeFasesDoProcessoPublicado _cenario;
    private readonly ApoioDaUnidadeDaFaseNaVersaoVigente _apoio;

    public InstanteForaDaFormaNaVersaoVigenteTests(ProcessoSeletivoDbFixture fixture)
    {
        _fixture = fixture;
        _cenario = new CenarioDeFasesDoProcessoPublicado(fixture);
        _apoio = new ApoioDaUnidadeDaFaseNaVersaoVigente(fixture);
    }

    [Fact(DisplayName = "O início da etapa sem fração de segundo faz a leitura das fases iniciadas falhar")]
    public async Task Iniciadas_InicioDaEtapaSemFracao_Falha()
    {
        (Guid processoId, _, _) = await _cenario.PublicadoAsync("PS etapa na forma antiga", Abertura);
        await _apoio.SucederNoEnvelopeAsync(
            processoId, DateTimeOffset.UtcNow, envelope => envelope["etapas"]!.AsArray()[0]!["inicio"] = "2020-01-01T00:00:00Z");

        Func<Task> leitura = () => _cenario.IniciadasAsync(processoId, DateTimeOffset.UtcNow);

        await leitura.Should().ThrowAsync<PayloadForaDoPerfilCanonicoException>();
    }

    [Fact(DisplayName = "O fim da fase com sete casas de fração faz a leitura das fases iniciadas falhar")]
    public async Task Iniciadas_FimDaFaseComSeteCasas_Falha()
    {
        (Guid processoId, _, _) = await _cenario.PublicadoAsync("PS fase com sete casas", Abertura);
        await _apoio.SucederAsync(processoId, DateTimeOffset.UtcNow, fase => fase["fim"] = "2020-01-31T00:00:00.1234567Z");

        Func<Task> leitura = () => _cenario.IniciadasAsync(processoId, DateTimeOffset.UtcNow);

        await leitura.Should().ThrowAsync<PayloadForaDoPerfilCanonicoException>();
    }

    [Fact(DisplayName = "O início da etapa na forma antiga faz a leitura da unidade da etapa falhar")]
    public async Task DaEtapa_InicioNaFormaAntiga_Falha()
    {
        (Guid processoId, _, EtapaProcesso etapa) = await _cenario.PublicadoAsync("PS unidade da etapa na forma antiga");
        await _apoio.SucederNoEnvelopeAsync(
            processoId, DateTimeOffset.UtcNow, envelope => envelope["etapas"]!.AsArray()[0]!["inicio"] = "2020-01-01T00:00:00+00:00");
        await using UniPlusDbContext context = _fixture.CreateDbContext();

        Func<Task> leitura = () => new UnidadeDaFaseNaVersaoVigente(context, TimeProvider.System).DaEtapaAsync(processoId, etapa.Id);

        await leitura.Should().ThrowAsync<PayloadForaDoPerfilCanonicoException>();
    }

    [Fact(DisplayName = "O início nulo da etapa continua sendo a etapa sem janela, que não começou")]
    public async Task Iniciadas_InicioNuloDaEtapa_SemJanela()
    {
        (Guid processoId, _, EtapaProcesso etapa) = await _cenario.PublicadoAsync("PS etapa sem janela", Abertura);
        await _apoio.SucederNoEnvelopeAsync(
            processoId, DateTimeOffset.UtcNow, envelope => envelope["etapas"]!.AsArray()[0]!["inicio"] = null);

        FasesIniciadas iniciadas = await _cenario.IniciadasAsync(processoId, DateTimeOffset.UtcNow);

        iniciadas.ContemEtapa(etapa.Id).Should().BeFalse();
    }
}
