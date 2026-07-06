namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Cobertura das invariantes do agregado-raiz <see cref="ProcessoSeletivo"/>
/// na fatia F0 (fundação): criação em rascunho, etapas pontuadas + divisor da
/// média, e oferta de atendimento especializado (ADR-0067). Vagas, bônus,
/// desempate e classificação entram nas fatias F2–F4 sobre o rol_de_regras.
/// </summary>
public sealed class ProcessoSeletivoTests
{
    private static ProcessoSeletivo NovoProcesso() =>
        ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU);

    [Fact(DisplayName = "Criar inicia o processo em rascunho (CA-01)")]
    public void CriarProcesso_IniciaEmRascunho()
    {
        ProcessoSeletivo processo = NovoProcesso();

        processo.Status.Should().Be(StatusProcesso.Rascunho);
        processo.Nome.Should().Be("PS 2026 — SiSU");
        processo.Tipo.Should().Be(TipoProcesso.SiSU);
        processo.Etapas.Should().BeEmpty();
        processo.OfertaAtendimento.Should().BeNull();
    }

    [Fact(DisplayName = "Criar sem tipo lanca ArgumentException")]
    public void Criar_SemTipo_Lanca()
    {
        Action act = () => ProcessoSeletivo.Criar("PS 2026", TipoProcesso.Nenhum);

        act.Should().Throw<ArgumentException>().WithParameterName("tipo");
    }

    [Fact(DisplayName = "Etapa classificatoria ou ambas com peso compõe o divisor da media (CA-02)")]
    public void Etapa_ComponeDivisorDaMedia()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result result = processo.DefinirEtapas(
        [
            EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, peso: 3m, ordem: 1),
            EtapaProcesso.Criar("Redação", CaraterEtapa.Ambas, peso: 2m, notaMinima: 5m, ordem: 2),
            EtapaProcesso.Criar("Banca de heteroidentificação", CaraterEtapa.Eliminatoria, ordem: 3),
        ]);

        result.IsSuccess.Should().BeTrue();
        // Só as etapas que pontuam (classificatória/ambas, com peso) entram
        // no divisor: 3 + 2 = 5. A eliminatória pura fica fora.
        processo.CalcularDivisorMedia().Should().Be(5m);
    }

    [Fact(DisplayName = "DefinirEtapas com ordem duplicada é recusado")]
    public void DefinirEtapas_OrdemDuplicada_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result result = processo.DefinirEtapas(
        [
            EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, peso: 3m, ordem: 1),
            EtapaProcesso.Criar("Redação", CaraterEtapa.Classificatoria, peso: 2m, ordem: 1),
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
            EtapaProcesso.Criar("Banca de heteroidentificação", CaraterEtapa.Eliminatoria, ordem: 1),
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
            EtapaProcesso.Criar("Análise de histórico", CaraterEtapa.Classificatoria, peso: null, ordem: 1),
        ]);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.NenhumaEtapaComponeNota");
    }

    [Fact(DisplayName = "DefinirEtapas substitui a colecao anterior por inteiro")]
    public void DefinirEtapas_Substitui()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirEtapas([EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, peso: 3m)]);

        processo.DefinirEtapas([EtapaProcesso.Criar("Entrevista", CaraterEtapa.Ambas, peso: 1m)]);

        processo.Etapas.Should().HaveCount(1);
        processo.Etapas.Single().Nome.Should().Be("Entrevista");
    }

    [Fact(DisplayName = "Tipo de deficiencia sob condicao PcD é aceito (CA-06, ADR-0067)")]
    public void Atendimento_TipoDeficienciaSobPcd_Aceito()
    {
        Result<OfertaAtendimentoEspecializado> result = OfertaAtendimentoEspecializado.Criar(
            condicoes: [OfertaCondicao.Criar(Guid.CreateVersion7(), "PCD", "Pessoa com deficiência")],
            recursos: [OfertaRecurso.Criar(Guid.CreateVersion7(), "Ledor")],
            tiposDeficiencia: [OfertaTipoDeficiencia.Criar(Guid.CreateVersion7(), "Deficiência visual")]);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TiposDeficiencia.Should().HaveCount(1);
    }

    [Fact(DisplayName = "Tipo de deficiencia sem condicao PcD é recusado (CA-06, ADR-0067)")]
    public void Atendimento_TipoDeficiencia_SoSobPcd()
    {
        Result<OfertaAtendimentoEspecializado> result = OfertaAtendimentoEspecializado.Criar(
            condicoes: [OfertaCondicao.Criar(Guid.CreateVersion7(), "LACTANTE", "Lactante")],
            recursos: [],
            tiposDeficiencia: [OfertaTipoDeficiencia.Criar(Guid.CreateVersion7(), "Deficiência visual")]);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("OfertaAtendimento.TipoDeficienciaSemCondicaoPcd");
    }

    [Fact(DisplayName = "Condicao de atendimento duplicada é recusada")]
    public void Atendimento_CondicaoDuplicada_Recusa()
    {
        Guid condicaoId = Guid.CreateVersion7();

        Result<OfertaAtendimentoEspecializado> result = OfertaAtendimentoEspecializado.Criar(
            condicoes:
            [
                OfertaCondicao.Criar(condicaoId, "PCD", "Pessoa com deficiência"),
                OfertaCondicao.Criar(condicaoId, "PCD", "Pessoa com deficiência"),
            ],
            recursos: [],
            tiposDeficiencia: []);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("OfertaAtendimento.CondicaoDuplicada");
    }

    [Fact(DisplayName = "Recurso de acessibilidade duplicado é recusado")]
    public void Atendimento_RecursoDuplicado_Recusa()
    {
        Guid recursoId = Guid.CreateVersion7();

        Result<OfertaAtendimentoEspecializado> result = OfertaAtendimentoEspecializado.Criar(
            condicoes: [],
            recursos: [OfertaRecurso.Criar(recursoId, "Ledor"), OfertaRecurso.Criar(recursoId, "Ledor")],
            tiposDeficiencia: []);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("OfertaAtendimento.RecursoDuplicado");
    }

    [Fact(DisplayName = "Tipo de deficiencia duplicado é recusado")]
    public void Atendimento_TipoDeficienciaDuplicado_Recusa()
    {
        Guid tipoId = Guid.CreateVersion7();

        Result<OfertaAtendimentoEspecializado> result = OfertaAtendimentoEspecializado.Criar(
            condicoes: [OfertaCondicao.Criar(Guid.CreateVersion7(), "PCD", "Pessoa com deficiência")],
            recursos: [],
            tiposDeficiencia:
            [
                OfertaTipoDeficiencia.Criar(tipoId, "Deficiência visual"),
                OfertaTipoDeficiencia.Criar(tipoId, "Deficiência visual"),
            ]);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("OfertaAtendimento.TipoDeficienciaDuplicado");
    }

    [Fact(DisplayName = "DefinirOfertaAtendimento vincula a oferta e as filhas à raiz")]
    public void DefinirOfertaAtendimento_Vincula()
    {
        ProcessoSeletivo processo = NovoProcesso();
        OfertaAtendimentoEspecializado oferta = OfertaAtendimentoEspecializado.Criar(
            condicoes: [OfertaCondicao.Criar(Guid.CreateVersion7(), "PCD", "Pessoa com deficiência")],
            recursos: [OfertaRecurso.Criar(Guid.CreateVersion7(), "Prova ampliada")],
            tiposDeficiencia: []).Value!;

        Result result = processo.DefinirOfertaAtendimento(oferta);

        result.IsSuccess.Should().BeTrue();
        processo.OfertaAtendimento.Should().NotBeNull();
        processo.OfertaAtendimento!.ProcessoSeletivoId.Should().Be(processo.Id);
        processo.OfertaAtendimento.Condicoes.Single().OfertaAtendimentoEspecializadoId
            .Should().Be(oferta.Id);
    }
}
