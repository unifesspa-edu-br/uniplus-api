namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class RemocaoDeEtapaIniciadaTests
{
    private const string Codigo = "FaseCronograma.EtapaIniciadaNaoSaiDoCronograma";

    [Fact(DisplayName = "Remover a fase de uma etapa iniciada, que a poda levaria junto, é recusado")]
    public void RemoverFase_ComEtapaIniciada_Recusa()
    {
        FaseCronograma habilitacao = ProcessoConformeFactory.FaseConforme();
        FaseCronograma classificacao = FaseNoCronograma(2, "CLASSIFICACAO");
        EtapaProcesso etapa = Etapa("Prova", CaraterEtapa.Classificatoria, 1m, "CLASSIFICACAO");
        ProcessoSeletivo processo = ProcessoComEtapas([habilitacao, classificacao], etapa);

        Result resultado = processo.DefinirCronogramaFases([habilitacao], [], PrecondicaoIfMatch.Curinga, Iniciadas(etapa));

        resultado.Error!.Code.Should().Be(Codigo);
        processo.CronogramaFases.Should().HaveCount(2);
        processo.DefinirCronogramaFases([habilitacao], [], PrecondicaoIfMatch.Curinga, FasesIniciadas.Nenhuma).IsSuccess.Should().BeTrue();
    }

    private static FasesIniciadas Iniciadas(EtapaProcesso etapa) =>
        new([], etapas: [new EtapaIniciada(etapa.Id, etapa.Nome, NaVersaoVigente: true)]);

    private static FaseCronograma FaseNoCronograma(int ordem, string codigo) =>
        FaseCronograma.Criar(
            new OrigemCanonicaDaFase(Guid.CreateVersion7(), codigo, OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(
                ordem, Inicio: new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 2, 28, 0, 0, 0, TimeSpan.Zero), FaseConcluinteCodigo: null,
                UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
            bancasRequeridas: []).Value!;

    private static EtapaProcesso Etapa(string nome, CaraterEtapa carater, decimal? peso, string faseCodigo = "RESULTADO_FINAL") =>
        EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(Nome: nome, Carater: carater, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: null, FaseCodigo: faseCodigo),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: peso, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;

    private static ProcessoSeletivo ProcessoComEtapas(params EtapaProcesso[] etapas) =>
        ProcessoComEtapas([ProcessoConformeFactory.FaseConforme()], etapas);

    private static ProcessoSeletivo ProcessoComEtapas(FaseCronograma[] fases, params EtapaProcesso[] etapas)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Remocao de etapa", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        processo.DefinirCronogramaFases(fases, [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        Result definidas = processo.DefinirEtapas(etapas, PrecondicaoIfMatch.Ausente);
        definidas.IsSuccess.Should().BeTrue(definidas.Error?.Message);
        return processo;
    }
}
