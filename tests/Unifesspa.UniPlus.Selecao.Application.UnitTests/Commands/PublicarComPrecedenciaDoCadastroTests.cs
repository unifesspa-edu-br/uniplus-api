namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A precedência entre fases do cadastro, lida na publicação sob o bloqueio do processo: a relação cadastrada depois da gravação do
/// cronograma recusa a publicação, e o cadastro sem relação não recusa.
/// </summary>
public sealed class PublicarComPrecedenciaDoCadastroTests
{
    private const string PrecedenciaViolada = "ProcessoSeletivo.PrecedenciaFaseViolada";

    private static ProcessoSeletivo ProcessoComSegundaFase()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Precedência");
        FaseCronograma habilitacao = FaseCronograma.Criar(
            new OrigemCanonicaDaFase(Guid.CreateVersion7(), "HABILITACAO", OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(2, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
            []).Value!;
        processo.DefinirCronogramaFases([.. processo.CronogramaFases, habilitacao], []).IsSuccess.Should().BeTrue();

        return processo;
    }

    [Fact(DisplayName = "A precedência cadastrada depois da gravação recusa a publicação das fases em ordem invertida, na fase que deveria vir depois")]
    public async Task PrecedenciaCadastradaDepois_RecusaAPublicacao()
    {
        ProcessoSeletivo processo = ProcessoComSegundaFase();
        string primeira = processo.CronogramaFases.Single(static fase => fase.Ordem == 1).Codigo;
        ServicosDoCongelamentoDeTeste servicos = ServicosDoCongelamentoDeTeste.Para(processo)
            .ComPrecedencias(PrecedenciasDeTeste.Com(("HABILITACAO", primeira, true)));

        (Result resposta, IEnumerable<object> eventos) = await servicos.PublicarAsync(processo.Id);

        FieldError recusa = resposta.Errors.Should().ContainSingle(static e => e.Error.Code == PrecedenciaViolada).Subject;
        recusa.Field.Should().Be("/cronograma/fases/0");
        recusa.Identificadores.Should().Equal(new IdentificadorDoItem("fases", primeira));
        eventos.Should().BeEmpty();
        await servicos.NadaFoiGravadoAsync();
    }

    [Fact(DisplayName = "Sem relação no cadastro a publicação não recusa por precedência")]
    public async Task SemRelacaoNoCadastro_NaoRecusa()
    {
        ProcessoSeletivo processo = ProcessoComSegundaFase();
        ServicosDoCongelamentoDeTeste servicos = ServicosDoCongelamentoDeTeste.Para(processo);

        (Result resposta, _) = await servicos.PublicarAsync(processo.Id);

        resposta.Errors.Select(static e => e.Error.Code).Should().NotContain(PrecedenciaViolada);
    }
}
