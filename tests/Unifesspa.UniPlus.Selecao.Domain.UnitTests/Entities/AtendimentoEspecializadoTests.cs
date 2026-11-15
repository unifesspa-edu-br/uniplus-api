namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Cobertura de <see cref="AtendimentoEspecializado.Criar"/> (CA-06, Story #758,
/// ADR-0067) — acumulação de duplicatas por dimensão e da invariante "tipo de deficiência só
/// sob condição PcD" (ADR-0125), e de <see cref="AtendimentoEspecializado.ValidarIdsUnicos"/>
/// (checagem pura, sem cadastro resolvido).
/// </summary>
public sealed class AtendimentoEspecializadoTests
{
    [Fact(DisplayName = "Criar com condição PcD e tipo de deficiência tem sucesso")]
    public void Criar_ComPcdETipoDeficiencia_Aceita()
    {
        Result<AtendimentoEspecializado> resultado = AtendimentoEspecializado.Criar(
            [CondicaoOfertada.Criar(Guid.CreateVersion7(), "PCD", "Pessoa com deficiência")],
            [],
            [DeficienciaAtendida.Criar(Guid.CreateVersion7(), "DEFICIENCIA_VISUAL", "Deficiência visual")]);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.TiposDeficiencia.Should().ContainSingle();
    }

    [Fact(DisplayName = "Criar com condição duplicada falha")]
    public void Criar_CondicaoDuplicada_Recusa()
    {
        Guid condicaoId = Guid.CreateVersion7();
        Result<AtendimentoEspecializado> resultado = AtendimentoEspecializado.Criar(
            [CondicaoOfertada.Criar(condicaoId, "LACTANTE", "Lactante"), CondicaoOfertada.Criar(condicaoId, "LACTANTE", "Lactante")],
            [], []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("AtendimentoEspecializado.CondicaoDuplicada");
    }

    [Fact(DisplayName = "Criar com tipo de deficiência sem condição PcD ofertada falha")]
    public void Criar_TipoDeficienciaSemPcd_Recusa()
    {
        Result<AtendimentoEspecializado> resultado = AtendimentoEspecializado.Criar(
            [CondicaoOfertada.Criar(Guid.CreateVersion7(), "LACTANTE", "Lactante")],
            [],
            [DeficienciaAtendida.Criar(Guid.CreateVersion7(), "DEFICIENCIA_VISUAL", "Deficiência visual")]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("AtendimentoEspecializado.TipoDeficienciaSemCondicaoPcd");
    }

    [Fact(DisplayName = "ADR-0125: Criar acumula duplicata de recurso e de tipo de deficiência no mesmo lote")]
    public void Criar_RecursoETipoDeficienciaDuplicados_AcumulaAsDuasViolacoes()
    {
        Guid recursoId = Guid.CreateVersion7();
        Guid tipoId = Guid.CreateVersion7();

        Result<AtendimentoEspecializado> resultado = AtendimentoEspecializado.Criar(
            [CondicaoOfertada.Criar(Guid.CreateVersion7(), "PCD", "Pessoa com deficiência")],
            [RecursoOfertado.Criar(recursoId, "LEDOR", "Ledor"), RecursoOfertado.Criar(recursoId, "LEDOR", "Ledor")],
            [DeficienciaAtendida.Criar(tipoId, "DEFICIENCIA_VISUAL", "Deficiência visual"), DeficienciaAtendida.Criar(tipoId, "DEFICIENCIA_VISUAL", "Deficiência visual")]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
        [
            "AtendimentoEspecializado.RecursoDuplicado",
            "AtendimentoEspecializado.TipoDeficienciaDuplicado",
        ]);
    }

    [Fact(DisplayName = "ADR-0125: Criar acumula duplicata de tipo de deficiência com a invariante PcD ausente, no mesmo lote")]
    public void Criar_TipoDeficienciaDuplicadoSemPcd_AcumulaAsDuasViolacoes()
    {
        Guid tipoId = Guid.CreateVersion7();

        Result<AtendimentoEspecializado> resultado = AtendimentoEspecializado.Criar(
            [],
            [],
            [DeficienciaAtendida.Criar(tipoId, "DEFICIENCIA_VISUAL", "Deficiência visual"), DeficienciaAtendida.Criar(tipoId, "DEFICIENCIA_VISUAL", "Deficiência visual")]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
        [
            "AtendimentoEspecializado.TipoDeficienciaDuplicado",
            "AtendimentoEspecializado.TipoDeficienciaSemCondicaoPcd",
        ]);
    }

    [Fact(DisplayName = "ValidarIdsUnicos sem duplicatas não retorna violações")]
    public void ValidarIdsUnicos_SemDuplicatas_Vazio()
    {
        List<FieldError> erros = AtendimentoEspecializado.ValidarIdsUnicos(
            [Guid.CreateVersion7()], [Guid.CreateVersion7()], [Guid.CreateVersion7()]);

        erros.Should().BeEmpty();
    }

    [Fact(DisplayName = "ValidarIdsUnicos detecta duplicata sem precisar do cadastro resolvido")]
    public void ValidarIdsUnicos_ComCondicaoIdDuplicado_Detecta()
    {
        Guid condicaoId = Guid.CreateVersion7();

        List<FieldError> erros = AtendimentoEspecializado.ValidarIdsUnicos(
            [condicaoId, condicaoId], [], []);

        erros.Select(e => e.Error.Code).Should().BeEquivalentTo(["AtendimentoEspecializado.CondicaoDuplicada"]);
    }
}
