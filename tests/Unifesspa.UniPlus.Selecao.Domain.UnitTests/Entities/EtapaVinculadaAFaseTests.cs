namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A etapa declara a fase a que pertence, e a raiz resolve o vínculo. É o que permite
/// qualquer fase subdividir-se — não só a que o cadastro marcava como agrupadora — e o
/// que faz a habilitação com oito etapas ser exprimível.
/// </summary>
public sealed class EtapaVinculadaAFaseTests
{
    private static TipoEtapaSnapshot Tipo() =>
        TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "ANALISE_DOCUMENTAL", "Análise Documental").Value!;

    private static ProcessoSeletivo Processo() => ProcessoSeletivo.Criar(
        "PS", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

    private static FaseCronograma Fase(int ordem, string codigo) => FaseCronograma.Criar(
        ordem, Guid.CreateVersion7(), codigo, "CEPS", OrigemDataFase.Delegada,
        agrupaEtapas: false, permiteComplementacao: false,
        coletaInscricao: false, coletaSolicitacaoIsencao: false,
        inicio: null, fim: null, produtos: [], faseConcluinteCodigo: null,
        emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null).Value!;

    private static EtapaProcesso Etapa(string nome, string? faseCodigo) => EtapaProcesso.Criar(
        nome, CaraterEtapa.Classificatoria, Tipo(), peso: 1m, notaMinima: null, ordem: null,
        faseCodigo: faseCodigo).Value!;

    [Fact(DisplayName = "Etapa que declara fase presente no cronograma é vinculada ao Id daquela fase")]
    public void EtapaComFaseDeclarada_VinculaAoIdDaFase()
    {
        ProcessoSeletivo processo = Processo();
        FaseCronograma habilitacao = Fase(1, "HABILITACAO");
        processo.DefinirCronogramaFases([habilitacao], [], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirEtapas(
            [Etapa("Envio dos documentos pessoais", "HABILITACAO")], PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue();
        processo.Etapas.Single().FaseCronogramaId.Should().Be(habilitacao.Id);
    }

    [Fact(DisplayName = "Etapa que declara fase fora do cronograma é recusada, nomeando a fase")]
    public void EtapaComFaseAusente_Recusada()
    {
        ProcessoSeletivo processo = Processo();
        processo.DefinirCronogramaFases([Fase(1, "INSCRICAO")], [], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirEtapas(
            [Etapa("Envio dos documentos pessoais", "HABILITACAO")], PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.EtapaSemFaseNoCronograma");
        resultado.Error!.Message.Should().Contain("HABILITACAO");
    }

    [Fact(DisplayName = "Qualquer fase subdivide-se: habilitação com várias etapas é aceita")]
    public void FaseNaoAgrupadora_AceitaVariasEtapas()
    {
        ProcessoSeletivo processo = Processo();
        processo.DefinirCronogramaFases([Fase(1, "HABILITACAO")], [], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirEtapas(
            [
                Etapa("Preenchimento do cadastro acadêmico", "HABILITACAO"),
                Etapa("Envio dos documentos pessoais", "HABILITACAO"),
                Etapa("Envio dos comprovantes da cota de renda", "HABILITACAO"),
            ],
            PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue();
        processo.EtapasDaFase("HABILITACAO").Should().HaveCount(3);
        processo.EtapasDaFase("INSCRICAO").Should().BeEmpty();
    }

    [Fact(DisplayName = "Etapas de fases distintas ficam cada uma na sua")]
    public void EtapasDeFasesDistintas_NaoSeMisturam()
    {
        ProcessoSeletivo processo = Processo();
        processo.DefinirCronogramaFases(
            [Fase(1, "AVALIACAO"), Fase(2, "HABILITACAO")], [], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        processo.DefinirEtapas(
            [Etapa("Prova objetiva", "AVALIACAO"), Etapa("Envio dos documentos pessoais", "HABILITACAO")],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.EtapasDaFase("AVALIACAO").Single().Nome.Should().Be("Prova objetiva");
        processo.EtapasDaFase("HABILITACAO").Single().Nome.Should().Be("Envio dos documentos pessoais");
    }

    [Fact(DisplayName = "Fase agrupadora com etapa declarada nela não é recusada por falta de etapa")]
    public void FaseAgrupadora_ComEtapaDeclarada_Aceita()
    {
        ProcessoSeletivo processo = Processo();
        FaseCronograma avaliacao = FaseCronograma.Criar(
            1, Guid.CreateVersion7(), "AVALIACAO", "CEPS", OrigemDataFase.Delegada,
            agrupaEtapas: true, permiteComplementacao: false,
            coletaInscricao: false, coletaSolicitacaoIsencao: false,
            inicio: null, fim: null, produtos: [], faseConcluinteCodigo: null,
            emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null).Value!;

        // A ordem que o vínculo impõe: a fase entra primeiro, e só então a etapa pode
        // declará-la. É por isso que a guarda eager de "agrupadora sem etapa" saiu da
        // gravação do cronograma — ali ela fecharia um ciclo sem ordem possível.
        processo.DefinirCronogramaFases([avaliacao], [], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirEtapas(
            [Etapa("Prova objetiva", "AVALIACAO")], PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue();
        processo.EtapasDaFase("AVALIACAO").Should().HaveCount(1);
    }

    [Fact(DisplayName = "Etapa sem fase declarada continua aceita — é o formato anterior ao vínculo")]
    public void EtapaSemFaseDeclarada_ContinuaAceita()
    {
        ProcessoSeletivo processo = Processo();
        processo.DefinirCronogramaFases([Fase(1, "AVALIACAO")], [], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirEtapas(
            [Etapa("Prova objetiva", faseCodigo: null)], PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue();
        processo.Etapas.Single().FaseCronogramaId.Should().Be(Guid.Empty);
    }
}
