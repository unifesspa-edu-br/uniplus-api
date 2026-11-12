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

    [Fact(DisplayName = "Remover uma etapa iniciada é recusado, citando a etapa, e o conjunto não muda")]
    public void Remover_EtapaIniciada_Recusa()
    {
        (EtapaProcesso prova, EtapaProcesso heteroidentificacao, EtapaProcesso analise) = TresEtapas();
        ProcessoSeletivo processo = ProcessoComEtapas(prova, heteroidentificacao, analise);

        Result resultado = processo.DefinirEtapas([prova, analise], PrecondicaoIfMatch.Curinga, Iniciadas(heteroidentificacao));

        resultado.Error!.Code.Should().Be(Codigo);
        resultado.Error.Message.Should().Contain("Heteroidentificação");
        processo.Etapas.Should().HaveCount(3);
    }

    [Fact(DisplayName = "Remover uma etapa que ainda não começou é aceito")]
    public void Remover_EtapaNaoIniciada_Aceita()
    {
        (EtapaProcesso prova, EtapaProcesso heteroidentificacao, EtapaProcesso analise) = TresEtapas();
        ProcessoSeletivo processo = ProcessoComEtapas(prova, heteroidentificacao, analise);

        Result resultado = processo.DefinirEtapas([prova, heteroidentificacao], PrecondicaoIfMatch.Curinga, FasesIniciadas.Nenhuma);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.Etapas.Should().HaveCount(2);
    }

    [Fact(DisplayName = "A etapa irmã de uma etapa iniciada sai, porque a abertura de uma etapa não inicia as outras da fase")]
    public void Remover_IrmaDeEtapaIniciada_Aceita()
    {
        (EtapaProcesso prova, EtapaProcesso heteroidentificacao, EtapaProcesso analise) = TresEtapas();
        ProcessoSeletivo processo = ProcessoComEtapas(prova, heteroidentificacao, analise);

        Result resultado = processo.DefinirEtapas([prova, heteroidentificacao], PrecondicaoIfMatch.Curinga, Iniciadas(heteroidentificacao));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Remover e recolocar a etapa iniciada na mesma retificação não é remoção")]
    public void Recolocar_EtapaIniciada_Aceita()
    {
        (EtapaProcesso prova, EtapaProcesso heteroidentificacao, EtapaProcesso analise) = TresEtapas();
        FasesIniciadas iniciadas = Iniciadas(heteroidentificacao);

        iniciadas.RecusaDaRemocaoDaEtapa([prova, analise])!.Code.Should().Be(Codigo);
        iniciadas.RecusaDaRemocaoDaEtapa([prova, analise, heteroidentificacao]).Should().BeNull();
    }

    [Fact(DisplayName = "A etapa iniciada que a versão vigente já não traz não é removida pela retificação")]
    public void Remover_EtapaIniciadaForaDaVersaoVigente_NaoRecusa()
    {
        (EtapaProcesso prova, EtapaProcesso heteroidentificacao, _) = TresEtapas();
        FasesIniciadas iniciadas = new([], etapas: [new EtapaIniciada(heteroidentificacao.Id, heteroidentificacao.Nome, NaVersaoVigente: false)]);

        iniciadas.RecusaDaRemocaoDaEtapa([prova]).Should().BeNull();
        iniciadas.ContemEtapa(heteroidentificacao.Id).Should().BeTrue();
        FasesIniciadas.Nenhuma.ContemEtapa(heteroidentificacao.Id).Should().BeFalse();
    }

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

    private static (EtapaProcesso Prova, EtapaProcesso Heteroidentificacao, EtapaProcesso Analise) TresEtapas() =>
        (Etapa("Prova", CaraterEtapa.Classificatoria, 1m), Etapa("Heteroidentificação", CaraterEtapa.Eliminatoria, null),
            Etapa("Análise documental", CaraterEtapa.Eliminatoria, null));

    private static FaseCronograma FaseNoCronograma(int ordem, string codigo) =>
        FaseCronograma.Criar(
            ordem, Guid.CreateVersion7(), codigo, OrigemDataFase.NoEdital, agrupaEtapas: true, permiteComplementacao: false,
            coletaInscricao: false, coletaSolicitacaoIsencao: false,
            inicio: new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero), fim: new DateTimeOffset(2026, 2, 28, 0, 0, 0, TimeSpan.Zero),
            produtos: [], faseConcluinteCodigo: null, emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null,
            unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;

    private static EtapaProcesso Etapa(string nome, CaraterEtapa carater, decimal? peso, string faseCodigo = "RESULTADO_FINAL") =>
        EtapaProcesso.Criar(
            nome, carater,
            TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!,
            peso: peso, notaMaxima: 10m, formaDeAvaliacao: FormaDeAvaliacao.Objetiva, faseCodigo: faseCodigo).Value!;

    private static ProcessoSeletivo ProcessoComEtapas(params EtapaProcesso[] etapas) =>
        ProcessoComEtapas([ProcessoConformeFactory.FaseConforme()], etapas);

    private static ProcessoSeletivo ProcessoComEtapas(FaseCronograma[] fases, params EtapaProcesso[] etapas)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Remocao de etapa", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        processo.DefinirCronogramaFases(fases, [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        Result definidas = processo.DefinirEtapas(etapas, PrecondicaoIfMatch.Ausente);
        definidas.IsSuccess.Should().BeTrue(definidas.Error?.Message);
        return processo;
    }
}
