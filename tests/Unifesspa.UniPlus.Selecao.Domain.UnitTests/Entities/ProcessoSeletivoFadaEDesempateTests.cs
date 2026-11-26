namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDoProcessoSeletivoEmRascunho;

/// <summary>
/// Fator de Atenuação das Desigualdades de Acesso (FADA) e critérios de desempate do <see cref="ProcessoSeletivo"/>:
/// o vínculo com a raiz, a ordem dos critérios e a integridade da referência às etapas.
/// </summary>
public sealed class ProcessoSeletivoFadaEDesempateTests
{
    [Fact(DisplayName = "DefinirFada declara que aplica e depois que não aplica, removendo a configuração (Fator de Atenuação das Desigualdades de Acesso, UNI-REQ-0015)")]
    public void DefinirFada_DefineERemove()
    {
        ProcessoSeletivo processo = NovoProcesso();
        ConfiguracaoDoFada fada = ConfiguracaoDoFada.Criar(ReferenciaRegra.De(RegraDoFada.Multiplicativo.Codigo(), "v1", new string('a', 64)), 1.20m, null, null, new BaseLegalCongelada(Guid.CreateVersion7(), "PORTARIA_UNIFESSPA_NO_2514_2023", "PORTARIA", "Portaria Unifesspa nº 2514/2023", "Institui inclusão regional", [("1504208", "Marabá", "PA")])).Value!;

        processo.DefinirFada(aplica: true, configuracao: fada).IsSuccess.Should().BeTrue();
        processo.Fada.Should().NotBeNull();
        processo.Fada!.ProcessoSeletivoId.Should().Be(processo.Id);

        processo.DefinirFada(aplica: false, configuracao: null).IsSuccess.Should().BeTrue();
        processo.Fada.Should().BeNull();
    }

    [Fact(DisplayName = "DefinirCriteriosDesempate vincula os critérios em ordem à raiz")]
    public void DefinirCriteriosDesempate_Vincula()
    {
        ProcessoSeletivo processo = NovoProcesso();
        CriterioDesempate idoso = CriterioIdoso(1);
        CriterioDesempate maiorIdade = CriterioMaiorIdade(2);

        Result result = processo.DefinirCriteriosDesempate([idoso, maiorIdade]);

        result.IsSuccess.Should().BeTrue();
        processo.CriteriosDesempate.Should().HaveCount(2);
        processo.CriteriosDesempate.Should().OnlyContain(c => c.ProcessoSeletivoId == processo.Id);
    }

    [Fact(DisplayName = "DefinirCriteriosDesempate vazia remove todos os critérios (o rascunho aceita)")]
    public void DefinirCriteriosDesempate_Vazia_RemoveTodos()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirCriteriosDesempate([CriterioMaiorIdade(1)]);

        Result result = processo.DefinirCriteriosDesempate([]);

        result.IsSuccess.Should().BeTrue();
        processo.CriteriosDesempate.Should().BeEmpty();
    }

    [Fact(DisplayName = "DefinirCriteriosDesempate com ordem duplicada é recusado")]
    public void DefinirCriteriosDesempate_OrdemDuplicada_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        CriterioDesempate a = CriterioMaiorIdade(1);
        CriterioDesempate b = CriterioIdoso(1);

        Result result = processo.DefinirCriteriosDesempate([a, b]);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.OrdemDesempateDuplicada");
    }

    [Fact(DisplayName = "DefinirCriteriosDesempate com etapa_ref existente no processo tem sucesso")]
    public void DefinirCriteriosDesempate_EtapaRefExistente_Sucesso()
    {
        ProcessoSeletivo processo = NovoProcesso();
        EtapaProcesso etapa = EtapaDeProva("Redação", CaraterEtapa.Classificatoria, 1, 1m, null);
        processo.DefinirEtapas([etapa]);

        Result result = processo.DefinirCriteriosDesempate([CriterioDeMaiorNotaDaEtapa(etapa.Id)]);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "DefinirCriteriosDesempate com etapa_ref inexistente no processo é recusado")]
    public void DefinirCriteriosDesempate_EtapaRefInexistente_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirEtapas([EtapaDeProva("Redação", CaraterEtapa.Classificatoria, 1, 1m, null)]);

        Result result = processo.DefinirCriteriosDesempate([CriterioDeMaiorNotaDaEtapa(Guid.CreateVersion7())]);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.EtapaRefDesempateInexistente");
    }

    [Fact(DisplayName = "DefinirEtapas recusa remover uma etapa referenciada por critério de desempate")]
    public void DefinirEtapas_RemoverEtapaReferenciadaPorDesempate_Recusa()
    {
        ProcessoSeletivo processo = ProcessoComCriterioNaEtapa(out EtapaProcesso redacao);

        // Troca as etapas por uma completamente nova — a antiga, referenciada pelo desempate, desaparece.
        Result result = processo.DefinirEtapas(
            [EtapaDeProva("Prova Objetiva", CaraterEtapa.Classificatoria, 1, 1m, null)]);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.EtapaReferenciadaPorDesempate");
        processo.Etapas.Should().ContainSingle(e => e.Id == redacao.Id);
    }

    [Fact(DisplayName = "DefinirEtapas permite manter a mesma etapa referenciada por desempate")]
    public void DefinirEtapas_MesmaEtapaReferenciadaPorDesempate_Sucesso()
    {
        ProcessoSeletivo processo = ProcessoComCriterioNaEtapa(out EtapaProcesso redacao);

        // Incluir a MESMA instância (mesmo Id) não deve ser bloqueado: o desempate continua executável.
        Result result = processo.DefinirEtapas(
        [
            redacao,
            EtapaDeProva("Prova Objetiva", CaraterEtapa.Classificatoria, 2, 2m, null),
        ]);

        result.IsSuccess.Should().BeTrue();
    }

    private static ProcessoSeletivo ProcessoComCriterioNaEtapa(out EtapaProcesso redacao)
    {
        ProcessoSeletivo processo = NovoProcesso();
        redacao = EtapaDeProva("Redação", CaraterEtapa.Classificatoria, 1, 1m, null);
        processo.DefinirEtapas([redacao]);
        processo.DefinirCriteriosDesempate([CriterioDeMaiorNotaDaEtapa(redacao.Id)]);
        return processo;
    }

    private static ReferenciaRegra RegraDesempate(string codigo) =>
        ReferenciaRegra.De(codigo, "v1", new string('a', 64));

    private static CriterioDesempate CriterioIdoso(int ordem) =>
        CriterioDesempate.Criar(ordem, RegraDesempate(RegraDeDesempate.Idoso.Codigo()), new ParametrosDesempateIdoso(60)).Value!;

    private static CriterioDesempate CriterioMaiorIdade(int ordem) =>
        CriterioDesempate.Criar(ordem, RegraDesempate(RegraDeDesempate.MaiorIdade.Codigo()), new ParametrosDesempateMaiorIdade()).Value!;

    private static CriterioDesempate CriterioDeMaiorNotaDaEtapa(Guid etapaId) =>
        CriterioDesempate.Criar(1, RegraDesempate(RegraDeDesempate.MaiorNotaNaEtapa.Codigo()), new ParametrosDesempateMaiorNotaEtapa(etapaId)).Value!;
}
