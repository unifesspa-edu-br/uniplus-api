namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O indicador de unidade acadêmica e o início da vigência são obrigatórios: ausentes (ou nulos, que o domínio não
/// distingue), são recusados como "obrigatório", acumulados com as demais recusas, em vez de virarem <c>false</c> ou o
/// ano 1 em silêncio.
/// </summary>
public sealed class UnidadeCamposObrigatoriosTests
{
    private static Result<Unidade> Criar(DateOnly? inicio, bool? unidadeAcademica, DateOnly? fim = null, string? nome = "Centro de Processos Seletivos") =>
        Unidade.Criar(
            nome,
            UnidadeDeTeste.IdentificadoresDoCeps(),
            CidadeDaUnidade.Nenhuma,
            new VigenciaDaUnidade(inicio, fim),
            null,
            TipoUnidade.Centro,
            unidadeAcademica);

    [Fact(DisplayName = "Criar sem o indicador de unidade acadêmica recusa como obrigatório, nomeando só ele")]
    public void Criar_SemUnidadeAcademica_RecusaSoEsseCampo()
    {
        Result<Unidade> resultado = Criar(UnidadeDeTeste.InicioDaVigencia, null);

        FieldError recusa = resultado.Errors.Should().ContainSingle().Subject;
        recusa.Field.Should().Be("unidadeAcademica");
        recusa.Error.Code.Should().Be(CodigosDeValidacao.Obrigatorio);
        recusa.Error.Message.Should().StartWith("Informe ");
    }

    [Fact(DisplayName = "Criar sem o início da vigência recusa como obrigatório, nomeando só ele")]
    public void Criar_SemInicioDaVigencia_RecusaSoEsseCampo()
    {
        Result<Unidade> resultado = Criar(null, false);

        FieldError recusa = resultado.Errors.Should().ContainSingle().Subject;
        recusa.Field.Should().Be("vigenciaInicio");
        recusa.Error.Code.Should().Be(CodigosDeValidacao.Obrigatorio);
    }

    [Fact(DisplayName = "Criar sem o início, com o fim informado, não compara o fim com um início que falta")]
    public void Criar_SemInicioComFim_NaoAcusaFimAnteriorAoInicio()
    {
        Result<Unidade> resultado = Criar(null, false, fim: UnidadeDeTeste.InicioDaVigencia.AddDays(-30));

        resultado.Errors.Should().ContainSingle(e => e.Field == "vigenciaInicio");
    }

    [Fact(DisplayName = "Os dois campos ausentes acumulam com o nome ausente, na ordem em que o domínio confere")]
    public void Criar_TudoAusente_AcumulaNaOrdem()
    {
        Result<Unidade> resultado = Criar(null, null, nome: null);

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            ("unidadeAcademica", CodigosDeValidacao.Obrigatorio),
            ("nome", UnidadeErrorCodes.NomeObrigatorio),
            ("vigenciaInicio", CodigosDeValidacao.Obrigatorio));
    }

    [Fact(DisplayName = "Indicador falso é aceito: falso é resposta, ausente é que falta")]
    public void Criar_ComUnidadeAcademicaFalsa_Sucesso()
    {
        Unidade unidade = Criar(UnidadeDeTeste.InicioDaVigencia, false).Value!;

        unidade.UnidadeAcademica.Should().BeFalse();
        unidade.VigenciaInicio.Should().Be(UnidadeDeTeste.InicioDaVigencia);
    }

    [Fact(DisplayName = "Atualizar sem o indicador recusa como obrigatório e não muda o valor existente")]
    public void Atualizar_SemUnidadeAcademica_RecusaENaoMuta()
    {
        Unidade unidade = Criar(UnidadeDeTeste.InicioDaVigencia, true).Value!;

        Result resultado = unidade.Atualizar(
            "Novo nome",
            new MudancaDeIdentificadores(UnidadeDeTeste.IdentificadoresDoCeps(), UnidadeDeTeste.InicioDaVigencia.AddMonths(1), null),
            CidadeDaUnidade.Nenhuma,
            null,
            null,
            TipoUnidade.Centro,
            null);

        resultado.Errors.Should().ContainSingle(e => e.Field == "unidadeAcademica" && e.Error.Code == CodigosDeValidacao.Obrigatorio);
        unidade.UnidadeAcademica.Should().BeTrue();
        unidade.Nome.Should().Be("Centro de Processos Seletivos");
    }
}
