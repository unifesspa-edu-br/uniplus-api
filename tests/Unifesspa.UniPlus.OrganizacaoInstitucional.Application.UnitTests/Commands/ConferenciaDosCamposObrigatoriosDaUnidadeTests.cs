namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Unidades;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;

/// <summary>
/// A conferência do cadastro da Unidade recusa o indicador de unidade acadêmica e o início da vigência ausentes como
/// "obrigatório", junto das demais recusas do corpo e antes de qualquer consulta ao repositório.
/// </summary>
public sealed class ConferenciaDosCamposObrigatoriosDaUnidadeTests
{
    private readonly CadastroDeUnidadeSimulado _cadastro = new();

    [Theory(DisplayName = "Criar sem um campo obrigatório recusa como obrigatório, nomeando só ele, sem consultar o repositório")]
    [InlineData("unidadeAcademica")]
    [InlineData("vigenciaInicio")]
    public async Task Criar_SemUmCampoObrigatorio_RecusaSoEsseCampo(string campo)
    {
        CriarUnidadeCommand command = campo == "unidadeAcademica"
            ? CadastroDeUnidadeSimulado.Criacao() with { UnidadeAcademica = null }
            : CadastroDeUnidadeSimulado.Criacao() with { VigenciaInicio = null };

        Result<Unidade> resultado = await _cadastro.Conferencia.ConferirNovaAsync(command, CancellationToken.None);

        resultado.Errors.Should().ContainSingle(e => e.Field == campo && e.Error.Code == CodigosDeValidacao.Obrigatorio);
        _cadastro.Unidades.ReceivedCalls().Should().BeEmpty();
    }

    [Fact(DisplayName = "Criar sem os dois campos e com o nome ausente acumula as três recusas, na ordem do domínio")]
    public async Task Criar_SemOsDoisCamposEComNomeAusente_AcumulaNaOrdem()
    {
        CriarUnidadeCommand command = CadastroDeUnidadeSimulado.Criacao() with { Nome = "", UnidadeAcademica = null, VigenciaInicio = null };

        Result<Unidade> resultado = await _cadastro.Conferencia.ConferirNovaAsync(command, CancellationToken.None);

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            ("unidadeAcademica", CodigosDeValidacao.Obrigatorio),
            ("nome", UnidadeErrorCodes.NomeObrigatorio),
            ("vigenciaInicio", CodigosDeValidacao.Obrigatorio));
    }

    [Fact(DisplayName = "Atualizar sem o indicador recusa como obrigatório antes de buscar a Unidade e não a muda")]
    public async Task Atualizar_SemUnidadeAcademica_RecusaSemBuscarAUnidade()
    {
        AtualizarUnidadeCommand command = CadastroDeUnidadeSimulado.AtualizacaoSemMudancaDeIdentificador(Guid.NewGuid())
            with
        { UnidadeAcademica = null };

        Result<Unidade> resultado = await _cadastro.Conferencia.ConferirAtualizacaoAsync(command, CancellationToken.None);

        resultado.Errors.Should().ContainSingle(e => e.Field == "unidadeAcademica" && e.Error.Code == CodigosDeValidacao.Obrigatorio);
        _cadastro.Unidades.ReceivedCalls().Should().BeEmpty();
    }
}
