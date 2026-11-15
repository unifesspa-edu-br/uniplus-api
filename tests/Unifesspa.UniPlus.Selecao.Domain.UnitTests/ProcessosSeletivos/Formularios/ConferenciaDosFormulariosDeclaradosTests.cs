namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Formularios;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

/// <summary>
/// O que se confere nos formulários que o processo declara: o código é único no processo e a fase a
/// que o formulário se liga está no cronograma. As recusas se acumulam.
/// </summary>
public sealed class ConferenciaDosFormulariosDeclaradosTests
{
    private static FormularioDeclarado Formulario(string codigo, Guid? faseId, bool obrigatorio = true) =>
        new(CodigoDoFormulario.Criar(codigo).Value!, faseId, obrigatorio);

    [Fact(DisplayName = "Formulários de códigos distintos, ligados a fases do cronograma, não têm recusa")]
    public void CodigosDistintosEFasesDoCronograma_SemRecusa()
    {
        FaseCronograma inscricao = FaseDeTeste.Criar(1, "INSCRICAO");
        FaseCronograma habilitacao = FaseDeTeste.Criar(3, "HABILITACAO");

        IReadOnlyList<FieldError> recusas = ConferenciaDosFormulariosDeclarados.Conferir(
            [Formulario("INSCRICAO", inscricao.Id), Formulario("HABILITACAO", habilitacao.Id), Formulario("HABILITACAO_2", habilitacao.Id)],
            [inscricao, habilitacao]);

        recusas.Should().BeEmpty();
    }

    [Fact(DisplayName = "O código repetido é recusado em cada formulário que o repete, depois do primeiro")]
    public void CodigoRepetido_Recusa()
    {
        FaseCronograma inscricao = FaseDeTeste.Criar(1, "INSCRICAO");

        IReadOnlyList<FieldError> recusas = ConferenciaDosFormulariosDeclarados.Conferir(
            [Formulario("INSCRICAO", inscricao.Id), Formulario("INSCRICAO", inscricao.Id), Formulario("INSCRICAO", inscricao.Id)],
            [inscricao]);

        recusas.Select(static recusa => (recusa.Field, recusa.Error.Code)).Should().Equal(
            ("formularios[1].codigo", FormulariosDeclaradosErrorCodes.CodigoDuplicado),
            ("formularios[2].codigo", FormulariosDeclaradosErrorCodes.CodigoDuplicado));
    }

    [Fact(DisplayName = "A mensagem do código repetido não repete o código")]
    public void CodigoRepetido_MensagemNaoRepeteOCodigo()
    {
        FaseCronograma inscricao = FaseDeTeste.Criar(1, "INSCRICAO");

        IReadOnlyList<FieldError> recusas = ConferenciaDosFormulariosDeclarados.Conferir(
            [Formulario("INSCRICAO", inscricao.Id), Formulario("INSCRICAO", inscricao.Id)], [inscricao]);

        recusas.Should().ContainSingle().Which.Error.Message.Should().NotContain("INSCRICAO");
    }

    [Fact(DisplayName = "A fase fora do cronograma do processo é recusada")]
    public void FaseForaDoCronograma_Recusa()
    {
        FaseCronograma inscricao = FaseDeTeste.Criar(1, "INSCRICAO");

        IReadOnlyList<FieldError> recusas = ConferenciaDosFormulariosDeclarados.Conferir(
            [Formulario("HABILITACAO", Guid.CreateVersion7())], [inscricao]);

        recusas.Should().ContainSingle().Which.Should().Match<FieldError>(recusa =>
            recusa.Field == "formularios[0].faseId" && recusa.Error.Code == FormularioProcessoErrorCodes.FaseForaDoCronograma);
    }

    [Fact(DisplayName = "O formulário sem fase ainda não tem recusa de fase: a publicação é que a exige")]
    public void FormularioSemFase_SemRecusaDeFase()
    {
        ConferenciaDosFormulariosDeclarados.Conferir([Formulario("INSCRICAO", faseId: null)], [FaseDeTeste.Criar(1, "INSCRICAO")]).Should().BeEmpty();
    }

    [Fact(DisplayName = "As recusas de código repetido e de fase fora do cronograma saem juntas")]
    public void Recusas_SeAcumulam()
    {
        FaseCronograma inscricao = FaseDeTeste.Criar(1, "INSCRICAO");

        IReadOnlyList<FieldError> recusas = ConferenciaDosFormulariosDeclarados.Conferir(
            [Formulario("INSCRICAO", inscricao.Id), Formulario("INSCRICAO", Guid.CreateVersion7())], [inscricao]);

        recusas.Select(static recusa => recusa.Error.Code).Should().BeEquivalentTo(
            FormulariosDeclaradosErrorCodes.CodigoDuplicado, FormularioProcessoErrorCodes.FaseForaDoCronograma);
    }
}
