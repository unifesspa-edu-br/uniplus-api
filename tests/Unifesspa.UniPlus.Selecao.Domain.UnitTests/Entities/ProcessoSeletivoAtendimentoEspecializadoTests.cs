namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDoProcessoSeletivoEmRascunho;

/// <summary>
/// Atendimento especializado do <see cref="ProcessoSeletivo"/> (ADR-0067): a oferta de
/// condições, recursos e tipos de deficiência, e o vínculo dela com a raiz.
/// </summary>
public sealed class ProcessoSeletivoAtendimentoEspecializadoTests
{
    [Fact(DisplayName = "Tipo de deficiencia sob condicao PcD é aceito (CA-06, ADR-0067)")]
    public void Atendimento_TipoDeficienciaSobPcd_Aceito()
    {
        Result<AtendimentoEspecializado> result = AtendimentoEspecializado.Criar(
            condicoes: [CondicaoOfertada.Criar(Guid.CreateVersion7(), "PCD", "Pessoa com deficiência")],
            recursos: [RecursoOfertado.Criar(Guid.CreateVersion7(), "LEDOR", "Ledor")],
            tiposDeficiencia: [DeficienciaAtendida.Criar(Guid.CreateVersion7(), "DEFICIENCIA_VISUAL", "Deficiência visual")]);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TiposDeficiencia.Should().HaveCount(1);
    }

    [Fact(DisplayName = "Tipo de deficiencia sem condicao PcD é recusado (CA-06, ADR-0067)")]
    public void Atendimento_TipoDeficiencia_SoSobPcd()
    {
        Result<AtendimentoEspecializado> result = AtendimentoEspecializado.Criar(
            condicoes: [CondicaoOfertada.Criar(Guid.CreateVersion7(), "LACTANTE", "Lactante")],
            recursos: [],
            tiposDeficiencia: [DeficienciaAtendida.Criar(Guid.CreateVersion7(), "DEFICIENCIA_VISUAL", "Deficiência visual")]);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("AtendimentoEspecializado.TipoDeficienciaSemCondicaoPcd");
    }

    [Fact(DisplayName = "Condicao de atendimento duplicada é recusada")]
    public void Atendimento_CondicaoDuplicada_Recusa()
    {
        Guid condicaoId = Guid.CreateVersion7();

        Result<AtendimentoEspecializado> result = AtendimentoEspecializado.Criar(
            condicoes:
            [
                CondicaoOfertada.Criar(condicaoId, "PCD", "Pessoa com deficiência"),
                CondicaoOfertada.Criar(condicaoId, "PCD", "Pessoa com deficiência"),
            ],
            recursos: [],
            tiposDeficiencia: []);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("AtendimentoEspecializado.CondicaoDuplicada");
    }

    [Fact(DisplayName = "Recurso de acessibilidade duplicado é recusado")]
    public void Atendimento_RecursoDuplicado_Recusa()
    {
        Guid recursoId = Guid.CreateVersion7();

        Result<AtendimentoEspecializado> result = AtendimentoEspecializado.Criar(
            condicoes: [],
            recursos: [RecursoOfertado.Criar(recursoId, "LEDOR", "Ledor"), RecursoOfertado.Criar(recursoId, "LEDOR", "Ledor")],
            tiposDeficiencia: []);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("AtendimentoEspecializado.RecursoDuplicado");
    }

    [Fact(DisplayName = "Tipo de deficiencia duplicado é recusado")]
    public void Atendimento_TipoDeficienciaDuplicado_Recusa()
    {
        Guid tipoId = Guid.CreateVersion7();

        Result<AtendimentoEspecializado> result = AtendimentoEspecializado.Criar(
            condicoes: [CondicaoOfertada.Criar(Guid.CreateVersion7(), "PCD", "Pessoa com deficiência")],
            recursos: [],
            tiposDeficiencia:
            [
                DeficienciaAtendida.Criar(tipoId, "DEFICIENCIA_VISUAL", "Deficiência visual"),
                DeficienciaAtendida.Criar(tipoId, "DEFICIENCIA_VISUAL", "Deficiência visual"),
            ]);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("AtendimentoEspecializado.TipoDeficienciaDuplicado");
    }

    [Fact(DisplayName = "DefinirAtendimentoEspecializado vincula a oferta e as filhas à raiz")]
    public void DefinirAtendimentoEspecializado_Vincula()
    {
        ProcessoSeletivo processo = NovoProcesso();
        AtendimentoEspecializado atendimento = AtendimentoEspecializado.Criar(
            condicoes: [CondicaoOfertada.Criar(Guid.CreateVersion7(), "PCD", "Pessoa com deficiência")],
            recursos: [RecursoOfertado.Criar(Guid.CreateVersion7(), "PROVA_AMPLIADA", "Prova ampliada")],
            tiposDeficiencia: []).Value!;

        Result result = processo.DefinirAtendimentoEspecializado(atendimento);

        result.IsSuccess.Should().BeTrue();
        processo.AtendimentoEspecializado.Should().NotBeNull();
        processo.AtendimentoEspecializado!.ProcessoSeletivoId.Should().Be(processo.Id);
        processo.AtendimentoEspecializado.Condicoes.Single().AtendimentoEspecializadoId
            .Should().Be(atendimento.Id);
    }
}
