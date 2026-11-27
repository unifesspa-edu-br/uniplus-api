namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A fase organizada em chamadas sem formulário obrigatório recusa a publicação, uma recusa por fase, cada uma apontando a fase
/// no documento; o formulário obrigatório na fase tira a recusa.
/// </summary>
public sealed class ChamadasSemFormularioObrigatorioNosHandlersTests
{
    private static FaseCronograma FaseEmChamadas(int ordem, string codigo, string chave) => FaseCronograma.Criar(
        ChaveLocal.Criar(chave).Value!,
        OrigensDoTipoDeFaseDeTeste.Da(
            Guid.CreateVersion7(), codigo, OrigemDataFase.NaExecucao, agrupaEtapas: false, permiteComplementacao: false,
            coletaInscricao: false, coletaSolicitacaoIsencao: false).ComChamadas(true),
        new DecisoesDeCronogramaDaFase(ordem, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
        []).Value!;

    private static ProcessoSeletivo ProcessoComDuasFasesEmChamadas(ProcessoSeletivo processo)
    {
        int proxima = processo.CronogramaFases.Max(static fase => fase.Ordem) + 1;
        processo.DefinirCronogramaFases(
            [.. processo.CronogramaFases, FaseEmChamadas(proxima, "HABILITACAO", "habilitacao"), FaseEmChamadas(proxima + 1, "AVALIACAO", "avaliacao")], [])
            .IsSuccess.Should().BeTrue();
        return processo;
    }

    private static FieldError[] DasChamadas(Result resposta) =>
        [.. resposta.Errors.Where(static e => e.Error.Code == FormularioProcessoErrorCodes.ChamadasSemFormularioObrigatorio)];

    [Fact(DisplayName = "A publicação recusa cada fase organizada em chamadas sem formulário obrigatório, na ordem do cronograma")]
    public async Task Publicar_RecusaUmaPorFase()
    {
        ProcessoSeletivo processo = ProcessoComDuasFasesEmChamadas(ProcessoSeletivoConformeBuilder.Criar("PS Chamadas"));
        ServicosDoCongelamentoDeTeste servicos = ServicosDoCongelamentoDeTeste.Para(processo);

        (Result resposta, IEnumerable<object> eventos) = await servicos.PublicarAsync(processo.Id);

        DasChamadas(resposta).Select(static e => e.Field).Should().Equal("/cronograma/fases/1", "/cronograma/fases/2");
        eventos.Should().BeEmpty();
        await servicos.NadaFoiGravadoAsync();
    }

    [Fact(DisplayName = "A publicação só recusa a fase que continua sem formulário obrigatório")]
    public async Task Publicar_ComObrigatorioNaPrimeira_RecusaSoASegunda()
    {
        ProcessoSeletivo processo = ProcessoComDuasFasesEmChamadas(ProcessoSeletivoConformeBuilder.Criar("PS Chamadas"));
        FormularioDeTeste.GarantirFormulario(processo, PapelDoFormularioDeTeste.Habilitacao);
        ServicosDoCongelamentoDeTeste servicos = ServicosDoCongelamentoDeTeste.Para(processo);

        (Result resposta, _) = await servicos.PublicarAsync(processo.Id);

        DasChamadas(resposta).Should().ContainSingle();
    }
}
