namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static CenarioDeFasesDoProcessoPublicado;

/// <summary>
/// O ato cuja chave gravada não declara um lugar reconhecível, num Postgres real: a leitura das fases iniciadas não falha
/// e o ato não inicia nada, em qualquer instante consultado, antes ou depois da divulgação dele.
/// </summary>
public sealed class AtoComChaveNaoReconhecidaTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private readonly CenarioDeFasesDoProcessoPublicado _cenario;

    public AtoComChaveNaoReconhecidaTests(ProcessoSeletivoDbFixture fixture) =>
        _cenario = new CenarioDeFasesDoProcessoPublicado(fixture);

    [Theory(DisplayName = "O ato com chave que não declara lugar não inicia nada, nem falha a consulta, antes ou depois da divulgação")]
    [InlineData("texto sem lugar", false)]
    [InlineData("texto sem lugar", true)]
    [InlineData("fase:não-é-uma-identidade|RESULTADO|-", false)]
    [InlineData("fase:não-é-uma-identidade|RESULTADO|-", true)]
    [InlineData("etapa:|RESULTADO|-", false)]
    [InlineData("etapa:|RESULTADO|-", true)]
    public async Task Iniciadas_AtoComChaveNaoReconhecida_NaoIniciaNada(string chave, bool consultaDepoisDaDivulgacao)
    {
        ProcessoComAto processo = await ProcessoComAtoAsync("PS ato com chave sem lugar");
        await _cenario.SubstituirAChaveDosAtosAsync(processo.ProcessoId, chave);

        FasesIniciadas iniciadas = await IniciadasAsync(processo, consultaDepoisDaDivulgacao);

        NadaComecou(iniciadas, processo);
    }

    [Theory(DisplayName = "O ato com a identidade da fase fora da forma gravada não inicia a fase, antes ou depois da divulgação")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Iniciadas_AtoComIdentidadeEmMaiusculas_NaoIniciaAFase(bool consultaDepoisDaDivulgacao)
    {
        ProcessoComAto processo = await ProcessoComAtoAsync("PS ato com identidade em maiusculas");
        string identidadeEmMaiusculas = processo.Fase.FaseCanonicaOrigemId.ToString("D").ToUpperInvariant();
        await _cenario.SubstituirAChaveDosAtosAsync(processo.ProcessoId, $"fase:{identidadeEmMaiusculas}|RESULTADO|-");

        FasesIniciadas iniciadas = await IniciadasAsync(processo, consultaDepoisDaDivulgacao);

        NadaComecou(iniciadas, processo);
    }

    [Fact(DisplayName = "O ato com a chave que a própria identidade escreve continua iniciando a fase produtora")]
    public async Task Iniciadas_AtoComChaveReconhecida_IniciaAFase()
    {
        ProcessoComAto processo = await ProcessoComAtoAsync("PS ato com chave reconhecida");

        FasesIniciadas iniciadas = await IniciadasAsync(processo, consultaDepoisDaDivulgacao: true);

        iniciadas.AvaliacaoComecou(LugarDaDecisao.DaFase(processo.Fase.FaseCanonicaOrigemId)).Should().BeTrue();
    }

    private sealed record ProcessoComAto(Guid ProcessoId, FaseCronograma Fase, EtapaProcesso Etapa, DateTimeOffset InstanteAntesDoAto);

    private async Task<ProcessoComAto> ProcessoComAtoAsync(string nome)
    {
        (Guid processoId, FaseCronograma fase, EtapaProcesso etapa) = await _cenario.PublicadoAsync(nome, Abertura);
        DateTimeOffset antesDoAto = DateTimeOffset.UtcNow;
        await _cenario.SemearAtoAsync(processoId, IdentidadeDoItemDoEdital.DaFase(fase, fase.Produtos.Single()));

        return new ProcessoComAto(processoId, fase, etapa, antesDoAto);
    }

    private Task<FasesIniciadas> IniciadasAsync(ProcessoComAto processo, bool consultaDepoisDaDivulgacao) =>
        _cenario.IniciadasAsync(processo.ProcessoId, consultaDepoisDaDivulgacao ? DateTimeOffset.UtcNow : processo.InstanteAntesDoAto);

    private static void NadaComecou(FasesIniciadas iniciadas, ProcessoComAto processo)
    {
        iniciadas.Contem(processo.Fase.FaseCanonicaOrigemId).Should().BeFalse();
        iniciadas.AvaliacaoComecou(LugarDaDecisao.DaFase(processo.Fase.FaseCanonicaOrigemId)).Should().BeFalse();
        iniciadas.ContemEtapa(processo.Etapa.Id).Should().BeFalse();
    }
}
