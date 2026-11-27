namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A precedência entre fases do cadastro, lida na publicação sob o bloqueio do processo: a relação cadastrada depois da gravação do
/// cronograma recusa a publicação, e o cadastro sem relação não recusa.
/// </summary>
public sealed class PublicarComPrecedenciaDoCadastroTests
{
    private static ProcessoSeletivo ProcessoComSegundaFaseNoMesmoMomento()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Precedência");
        FaseCronograma habilitacao = FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            OrigensDoTipoDeFaseDeTeste.Da(Guid.CreateVersion7(), "HABILITACAO", OrigemDataFase.NaExecucao, agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(2, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps) { NoMesmoMomentoDaAnterior = true },
            new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
            []).Value!;
        processo.DefinirCronogramaFases([.. processo.CronogramaFases, habilitacao], []).IsSuccess.Should().BeTrue();

        return processo;
    }

    [Fact(DisplayName = "A precedência cadastrada depois da gravação recusa a publicação das fases ligadas no mesmo momento, na fase marcada")]
    public async Task PrecedenciaCadastradaDepois_RecusaAPublicacao()
    {
        ProcessoSeletivo processo = ProcessoComSegundaFaseNoMesmoMomento();
        string primeira = processo.CronogramaFases.Single(static fase => fase.Ordem == 1).Codigo;
        ServicosDoCongelamentoDeTeste servicos = ServicosDoCongelamentoDeTeste.Para(processo)
            .ComPrecedencias(PrecedenciasDeTeste.Com((primeira, "HABILITACAO", true)));

        (Result resposta, IEnumerable<object> eventos) = await servicos.PublicarAsync(processo.Id);

        FieldError recusa = resposta.Errors.Should().ContainSingle(static e => e.Error.Code == ProcessoSeletivoErrorCodes.FasesComPrecedenciaNoMesmoMomento).Subject;
        recusa.Field.Should().Be("/cronograma/fases/1/noMesmoMomentoDaAnterior");
        recusa.Identificadores.Should().Equal(new IdentificadorDoItem("fases", "HABILITACAO"));
        eventos.Should().BeEmpty();
        await servicos.NadaFoiGravadoAsync();
    }

    [Fact(DisplayName = "Sem relação no cadastro a publicação não recusa por precedência")]
    public async Task SemRelacaoNoCadastro_NaoRecusa()
    {
        ProcessoSeletivo processo = ProcessoComSegundaFaseNoMesmoMomento();
        ServicosDoCongelamentoDeTeste servicos = ServicosDoCongelamentoDeTeste.Para(processo);

        (Result resposta, _) = await servicos.PublicarAsync(processo.Id);

        resposta.Errors.Select(static e => e.Error.Code).Should().NotContain(ProcessoSeletivoErrorCodes.FasesComPrecedenciaNoMesmoMomento);
    }
}
