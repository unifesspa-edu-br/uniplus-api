namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A edição das etapas recusa a remoção da etapa que a versão publicada já iniciou (UNI-REQ-0364), com a mesma
/// recusa do fechamento da sessão, depois da coerência da própria coleção e antes do que depende das etapas.
/// </summary>
public sealed class DefinirEtapasComEtapaIniciadaTests
{
    private const string CodigoDaRemocao = "FaseCronograma.EtapaIniciadaNaoSaiDoCronograma";

    [Fact(DisplayName = "Remover uma etapa iniciada é recusado, citando a etapa, e as etapas não mudam")]
    public void DefinirEtapas_EtapaIniciadaRemovida_Recusa()
    {
        (ProcessoSeletivo processo, EtapaProcesso prova, EtapaProcesso heteroidentificacao, EtapaProcesso analise) = ProcessoComTresEtapas();

        Result resultado = processo.DefinirEtapas([prova, analise], PrecondicaoIfMatch.Ausente, Iniciadas(heteroidentificacao));

        resultado.Error!.Code.Should().Be(CodigoDaRemocao);
        resultado.Error.Message.Should().Contain("Heteroidentificação");
        processo.Etapas.Should().HaveCount(3);
    }

    [Fact(DisplayName = "Remover uma etapa que ainda não começou é aceito")]
    public void DefinirEtapas_EtapaNaoIniciadaRemovida_Aceita()
    {
        (ProcessoSeletivo processo, EtapaProcesso prova, EtapaProcesso heteroidentificacao, EtapaProcesso analise) = ProcessoComTresEtapas();

        Result resultado = processo.DefinirEtapas([heteroidentificacao, analise], PrecondicaoIfMatch.Ausente, Iniciadas(heteroidentificacao));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.Etapas.Should().NotContain(prova);
    }

    [Fact(DisplayName = "Reenviar a etapa iniciada, mesmo com outra ordem, não é remoção")]
    public void DefinirEtapas_EtapaIniciadaReenviada_Aceita()
    {
        (ProcessoSeletivo processo, EtapaProcesso prova, EtapaProcesso heteroidentificacao, EtapaProcesso analise) = ProcessoComTresEtapas();

        Result resultado = processo.DefinirEtapas([analise, heteroidentificacao, prova], PrecondicaoIfMatch.Ausente, Iniciadas(heteroidentificacao));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Theory(DisplayName = "Sem as etapas iniciadas, qualquer etapa sai, como antes")]
    [InlineData(false)]
    [InlineData(true)]
    public void DefinirEtapas_SemIniciadas_Aceita(bool informandoNenhuma)
    {
        (ProcessoSeletivo processo, EtapaProcesso prova, _, _) = ProcessoComTresEtapas();

        Result resultado = processo.DefinirEtapas([prova], PrecondicaoIfMatch.Ausente, informandoNenhuma ? FasesIniciadas.Nenhuma : null);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.Etapas.Should().ContainSingle();
    }

    [Fact(DisplayName = "A coerência da própria coleção é recusada antes da remoção da etapa iniciada")]
    public void DefinirEtapas_OrdemDuplicadaERemocaoIniciada_RecusaAOrdem()
    {
        (ProcessoSeletivo processo, EtapaProcesso prova, EtapaProcesso heteroidentificacao, _) = ProcessoComTresEtapas();
        EtapaProcesso outraComAMesmaOrdem = Etapa("Redação", prova.Ordem);

        Result resultado = processo.DefinirEtapas([prova, outraComAMesmaOrdem], PrecondicaoIfMatch.Ausente, Iniciadas(heteroidentificacao));

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.OrdemEtapaDuplicada");
    }

    [Fact(DisplayName = "A remoção da etapa iniciada é recusada antes do que depende das etapas")]
    public void DefinirEtapas_FaseInexistenteERemocaoIniciada_RecusaARemocao()
    {
        (ProcessoSeletivo processo, EtapaProcesso prova, EtapaProcesso heteroidentificacao, _) = ProcessoComTresEtapas();
        EtapaProcesso[] semFaseNoCronograma = [Etapa("Redação", 9, faseCodigo: "FASE_FORA_DO_CRONOGRAMA")];

        processo.DefinirEtapas(semFaseNoCronograma, PrecondicaoIfMatch.Ausente).Error!.Code
            .Should().Be("ProcessoSeletivo.EtapaSemFaseNoCronograma");
        processo.DefinirEtapas(semFaseNoCronograma, PrecondicaoIfMatch.Ausente, Iniciadas(heteroidentificacao)).Error!.Code
            .Should().Be(CodigoDaRemocao);
        prova.Id.Should().NotBeEmpty();
    }

    [Fact(DisplayName = "A guarda de mutação é recusada antes da remoção da etapa iniciada")]
    public void DefinirEtapas_ProcessoEncerradoERemocaoIniciada_RecusaAMutacao()
    {
        (ProcessoSeletivo processo, EtapaProcesso prova, EtapaProcesso heteroidentificacao, _) = ProcessoComTresEtapas();
        typeof(ProcessoSeletivo).GetProperty(nameof(ProcessoSeletivo.Status))!.SetValue(processo, StatusProcesso.Encerrado);

        Result resultado = processo.DefinirEtapas([prova], PrecondicaoIfMatch.Ausente, Iniciadas(heteroidentificacao));

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoForaDeEstadoEditavel");
    }

    private static FasesIniciadas Iniciadas(EtapaProcesso etapa) =>
        new([], etapas: [new EtapaIniciada(etapa.Id, etapa.Nome, NaVersaoVigente: true)]);

    private static EtapaProcesso Etapa(string nome, int? ordem, string faseCodigo = "RESULTADO_FINAL") =>
        EtapaProcesso.Criar(
            nome, CaraterEtapa.Classificatoria,
            TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!,
            peso: 1m, ordem: ordem, notaMaxima: 10m, formaDeAvaliacao: FormaDeAvaliacao.Objetiva, faseCodigo: faseCodigo).Value!;

    private static (ProcessoSeletivo Processo, EtapaProcesso Prova, EtapaProcesso Heteroidentificacao, EtapaProcesso Analise) ProcessoComTresEtapas()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Remocao de etapa", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        processo.DefinirCronogramaFases([ProcessoConformeFactory.FaseConforme()], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        EtapaProcesso[] etapas = [Etapa("Prova", 1), Etapa("Heteroidentificação", 2), Etapa("Análise documental", 3)];
        processo.DefinirEtapas(etapas, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        return (processo, etapas[0], etapas[1], etapas[2]);
    }
}
