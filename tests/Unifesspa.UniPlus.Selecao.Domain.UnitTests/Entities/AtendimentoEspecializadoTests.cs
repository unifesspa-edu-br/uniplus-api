namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

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

    [Fact(DisplayName = "Código pcd em minúsculas não permite ofertar tipo de deficiência")]
    public void Criar_CondicaoPcdEmMinusculas_RecusaTipoDeficiencia()
    {
        Result<AtendimentoEspecializado> resultado = AtendimentoEspecializado.Criar(
            [CondicaoOfertada.Criar(Guid.CreateVersion7(), "pcd", "Condição")], [],
            [DeficienciaAtendida.Criar(Guid.CreateVersion7(), "VISUAL", "Visual")]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new FieldError(
            "tipoDeficienciaIds", new DomainError("AtendimentoEspecializado.TipoDeficienciaSemCondicaoPcd",
                "Tipos de deficiência só podem ser ofertados quando a condição PcD está ofertada.")));
    }

    [Fact(DisplayName = "Código pcd em minúsculas acumula recusa com recurso duplicado")]
    public void Criar_CondicaoPcdEmMinusculasERecursoDuplicado_AcumulaRecusas()
    {
        Guid recursoId = Guid.CreateVersion7();
        Result<AtendimentoEspecializado> resultado = AtendimentoEspecializado.Criar(
            [CondicaoOfertada.Criar(Guid.CreateVersion7(), "pcd", "Condição")],
            [RecursoOfertado.Criar(recursoId, "LEDOR", "Ledor"), RecursoOfertado.Criar(recursoId, "LEDOR", "Ledor")],
            [DeficienciaAtendida.Criar(Guid.CreateVersion7(), "VISUAL", "Visual")]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
            [("recursoIds", "AtendimentoEspecializado.RecursoDuplicado"),
             ("tipoDeficienciaIds", "AtendimentoEspecializado.TipoDeficienciaSemCondicaoPcd")], o => o.WithStrictOrdering());
        resultado.Errors.Should().OnlyContain(e => !e.Error.Message.Contains("pcd", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Criar acumula recurso e tipo de deficiência duplicados")]
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
        ], o => o.WithStrictOrdering());
    }

    [Fact(DisplayName = "Criar acumula tipo de deficiência duplicado e condição PcD ausente")]
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
        ], o => o.WithStrictOrdering());
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
