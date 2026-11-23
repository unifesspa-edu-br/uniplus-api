namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDoProcessoSeletivoEmRascunho;

/// <summary>
/// Cobertura das invariantes do agregado-raiz <see cref="ProcessoSeletivo"/> na fundação:
/// criação em rascunho, etapas pontuadas e divisor da média.
/// </summary>
public sealed class ProcessoSeletivoTests
{
    [Fact(DisplayName = "Criar inicia o processo em rascunho (CA-01)")]
    public void CriarProcesso_IniciaEmRascunho()
    {
        ProcessoSeletivo processo = NovoProcesso();

        processo.Status.Should().Be(StatusProcesso.Rascunho);
        processo.Nome.Should().Be("PS 2026 — SiSU");
        processo.TipoProcesso.Should().Be(TiposDeProcessoDeTeste.SiSU);
        processo.Etapas.Should().BeEmpty();
        processo.AtendimentoEspecializado.Should().BeNull();
    }

    [Fact(DisplayName = "Criar sem tipo lanca ArgumentException")]
    public void Criar_SemTipo_Lanca()
    {
        Action act = () => NovoProcesso("PS 2026", TiposDeProcessoDeTeste.Nenhum);

        act.Should().Throw<ArgumentException>().WithParameterName("tipoProcesso");
    }

    [Fact(DisplayName = "Criar sem origem dos candidatos lanca ArgumentException (Story #851 §3.4)")]
    public void Criar_SemOrigemCandidatos_Lanca()
    {
        Action act = () => NovoProcesso("PS 2026", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.Nenhuma);

        act.Should().Throw<ArgumentException>().WithParameterName("origemCandidatos");
    }

    [Fact(DisplayName = "Criar sem unidade administradora (Guid.Empty) lanca ArgumentException (issue #849, CA-04)")]
    public void Criar_SemUnidadeAdministradoraOrigemId_Lanca()
    {
        Action act = () => NovoProcesso("PS 2026", TiposDeProcessoDeTeste.SiSU, unidadeAdministradoraOrigemId: Guid.Empty);

        act.Should().Throw<ArgumentException>().WithParameterName("unidadeAdministradoraOrigemId");
    }

    [Fact(DisplayName = "Criar congela a identidade da unidade administradora (issue #849, CA-03)")]
    public void CriarProcesso_CongelaUnidadeAdministradora()
    {
        Guid unidadeId = Guid.NewGuid();
        UnidadeAdministradoraSnapshot snapshot = UnidadeAdministradoraCeps();

        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS 2026", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, unidadeId, snapshot, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

        processo.UnidadeAdministradoraOrigemId.Should().Be(unidadeId);
        processo.UnidadeAdministradora.Should().BeSameAs(snapshot);
    }

    [Fact(DisplayName = "Etapa classificatoria ou ambas com peso compõe o divisor da media (CA-02)")]
    public void Etapa_ComponeDivisorDaMedia()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result result = processo.DefinirEtapas(
        [
            EtapaDeProva("Prova Objetiva", CaraterEtapa.Classificatoria, 1, 3m, null),
            EtapaDeProva("Redação", CaraterEtapa.Ambas, 2, 2m, 5m),
            EtapaDeProva("Banca de heteroidentificação", CaraterEtapa.Eliminatoria, 3, null, null),
        ]);

        // Só as etapas que pontuam (classificatória/ambas, com peso) entram no divisor: 3 + 2 = 5.
        result.IsSuccess.Should().BeTrue();
        processo.Declaracao().Avaliacao.CalcularDivisorMedia().Should().Be(5m);
    }

    [Fact(DisplayName = "DefinirEtapas com ordem duplicada é recusado")]
    public void DefinirEtapas_OrdemDuplicada_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result result = processo.DefinirEtapas(
        [
            EtapaDeProva("Prova Objetiva", CaraterEtapa.Classificatoria, 1, 3m, null),
            EtapaDeProva("Redação", CaraterEtapa.Classificatoria, 1, 2m, null),
        ]);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.OrdemEtapaDuplicada");
    }

    [Fact(DisplayName = "DefinirEtapas só com etapa eliminatoria (nenhuma compõe nota) é recusado")]
    public void DefinirEtapas_NenhumaComponeNota_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result result = processo.DefinirEtapas(
        [
            EtapaDeProva("Banca de heteroidentificação", CaraterEtapa.Eliminatoria, 1, null, null),
        ]);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.NenhumaEtapaComponeNota");
    }

    [Fact(DisplayName = "DefinirEtapas com classificatoria sem peso (nenhuma compõe nota) é recusado")]
    public void DefinirEtapas_ClassificatoriaSemPeso_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result result = processo.DefinirEtapas(
        [
            EtapaDeProva("Análise de histórico", CaraterEtapa.Classificatoria, 1, null, null),
        ]);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.NenhumaEtapaComponeNota");
    }

    [Fact(DisplayName = "DefinirEtapas substitui a colecao anterior por inteiro")]
    public void DefinirEtapas_Substitui()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirEtapas([EtapaDeProva("Prova Objetiva", CaraterEtapa.Classificatoria, 1, 3m, null)]);

        processo.DefinirEtapas([EtapaDeProva("Entrevista", CaraterEtapa.Ambas, 1, 1m, null)]);

        processo.Etapas.Should().HaveCount(1);
        processo.Etapas.Single().Nome.Should().Be("Entrevista");
    }
}
