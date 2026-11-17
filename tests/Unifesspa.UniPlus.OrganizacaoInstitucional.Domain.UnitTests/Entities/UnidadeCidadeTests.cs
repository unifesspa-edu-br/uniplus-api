namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>Cidade da Unidade: opcional all-or-nothing.</summary>
public sealed class UnidadeCidadeTests
{
    [Fact(DisplayName = "Criar sem cidade tem sucesso — cidade é opcional em Unidade")]
    public void Criar_SemCidade_Sucesso()
    {
        Result<Unidade> resultado = Unidade.Criar(
            "Nome valido",
            UnidadeDeTeste.IdentificadoresDoCeps(),
            CidadeDaUnidade.Nenhuma,
            new VigenciaDaUnidade(UnidadeDeTeste.InicioDaVigencia, null),
            null,
            TipoUnidade.Centro,
            false);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.CidadeCodigoIbge.Should().BeNull();
        resultado.Value.CidadeNome.Should().BeNull();
        resultado.Value.CidadeUf.Should().BeNull();
    }

    [Fact(DisplayName = "Criar com trio de cidade completo tem sucesso e normaliza UF para uppercase")]
    public void Criar_ComCidadeCompleta_Sucesso()
    {
        Result<Unidade> resultado = Unidade.Criar(
            "Nome valido",
            UnidadeDeTeste.IdentificadoresDoCeps(),
            new CidadeDaUnidade("1504208", "Marabá", "pa"),
            new VigenciaDaUnidade(UnidadeDeTeste.InicioDaVigencia, null),
            null,
            TipoUnidade.Centro,
            false);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.CidadeCodigoIbge.Should().Be("1504208");
        resultado.Value.CidadeNome.Should().Be("Marabá");
        resultado.Value.CidadeUf.Should().Be("PA");
    }

    [Theory(DisplayName = "Criar com cidade parcialmente preenchida falha — bicondicional all-or-nothing")]
    [InlineData(null, "Marabá", "PA")]
    [InlineData("1504208", null, "PA")]
    [InlineData("1504208", "Marabá", null)]
    public void Criar_ComCidadeParcial_Falha(string? codigo, string? nome, string? uf)
    {
        Result<Unidade> resultado = Unidade.Criar(
            "Nome valido",
            UnidadeDeTeste.IdentificadoresDoCeps(),
            new CidadeDaUnidade(codigo, nome, uf),
            new VigenciaDaUnidade(UnidadeDeTeste.InicioDaVigencia, null),
            null,
            TipoUnidade.Centro,
            false);

        resultado.IsFailure.Should().BeTrue();
    }

    [Fact(DisplayName = "Criar com código IBGE incoerente com a UF falha")]
    public void Criar_ComCidadeUfIncoerente_Falha()
    {
        Result<Unidade> resultado = Unidade.Criar(
            "Nome valido",
            UnidadeDeTeste.IdentificadoresDoCeps(),
            new CidadeDaUnidade("1504208", "Marabá", "SP"),
            new VigenciaDaUnidade(UnidadeDeTeste.InicioDaVigencia, null),
            null,
            TipoUnidade.Centro,
            false);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CidadeReferenciaErrorCodes.UfIncoerente);
    }

    [Fact(DisplayName = "Atualizar propaga cidade completa")]
    public void Atualizar_ComCidadeCompleta_Sucesso()
    {
        Unidade unidade = UnidadeDeTeste.Criar().Value!;

        Result resultado = UnidadeDeTeste.AtualizarSemMudarIdentificadores(
            unidade, unidade.Nome, new CidadeDaUnidade("1504208", "Marabá", "PA"), unidadeAcademica: false);

        resultado.IsSuccess.Should().BeTrue();
        unidade.CidadeCodigoIbge.Should().Be("1504208");
        unidade.CidadeNome.Should().Be("Marabá");
        unidade.CidadeUf.Should().Be("PA");
    }

    [Fact(DisplayName = "Atualizar removendo cidade (trio nulo) tem sucesso e zera os três campos")]
    public void Atualizar_RemovendoCidade_ZeraOsTresCampos()
    {
        Unidade unidade = UnidadeDeTeste.Criar(cidade: new CidadeDaUnidade("1504208", "Marabá", "PA")).Value!;

        Result resultado = UnidadeDeTeste.AtualizarSemMudarIdentificadores(
            unidade, unidade.Nome, CidadeDaUnidade.Nenhuma, unidadeAcademica: false);

        resultado.IsSuccess.Should().BeTrue();
        unidade.CidadeCodigoIbge.Should().BeNull();
        unidade.CidadeNome.Should().BeNull();
        unidade.CidadeUf.Should().BeNull();
    }
}
