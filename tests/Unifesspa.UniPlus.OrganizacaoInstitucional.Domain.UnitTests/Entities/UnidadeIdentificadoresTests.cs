namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class UnidadeIdentificadoresTests
{
    [Fact(DisplayName = "Criar com sigla vazia retorna SiglaObrigatoria")]
    public void Criar_ComSiglaVazia_DeveRetornarSiglaObrigatoria()
    {
        Result<Unidade> resultado = Unidade.Criar(
            "Nome valido",
            new IdentificadoresDaUnidade(UnidadeDeTeste.SlugDoCeps.Valor, "  ", "0001", null),
            CidadeDaUnidade.Nenhuma,
            new VigenciaDaUnidade(UnidadeDeTeste.InicioDaVigencia, null),
            null,
            TipoUnidade.Centro,
            false);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.SiglaObrigatoria);
    }

    [Fact(DisplayName = "Criar com sigla > 50 chars retorna SiglaTamanho")]
    public void Criar_ComSiglaMuitoLonga_DeveRetornarSiglaTamanho()
    {
        Result<Unidade> resultado = Unidade.Criar(
            "Nome valido",
            new IdentificadoresDaUnidade(UnidadeDeTeste.SlugDoCeps.Valor, new string('A', 51), "0001", null),
            CidadeDaUnidade.Nenhuma,
            new VigenciaDaUnidade(UnidadeDeTeste.InicioDaVigencia, null),
            null,
            TipoUnidade.Centro,
            false);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.SiglaTamanho);
    }

    [Fact(DisplayName = "Criar com código vazio retorna CodigoObrigatorio")]
    public void Criar_ComCodigoVazio_DeveRetornarCodigoObrigatorio()
    {
        Result<Unidade> resultado = Unidade.Criar(
            "Nome valido",
            new IdentificadoresDaUnidade(UnidadeDeTeste.SlugDoCeps.Valor, "CEPS", "  ", null),
            CidadeDaUnidade.Nenhuma,
            new VigenciaDaUnidade(UnidadeDeTeste.InicioDaVigencia, null),
            null,
            TipoUnidade.Centro,
            false);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.CodigoObrigatorio);
    }

    [Fact(DisplayName = "Criar com alias > 100 chars retorna AliasTamanho")]
    public void Criar_ComAliasMuitoLongo_DeveRetornarAliasTamanho()
    {
        Result<Unidade> resultado = Unidade.Criar(
            "Nome valido",
            UnidadeDeTeste.IdentificadoresDoCeps(alias: new string('a', 101)),
            CidadeDaUnidade.Nenhuma,
            new VigenciaDaUnidade(UnidadeDeTeste.InicioDaVigencia, null),
            null,
            TipoUnidade.Centro,
            false);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.AliasTamanho);
    }

    [Fact(DisplayName = "Sigla é normalizada para uppercase no Criar")]
    public void Criar_SiglaEhNormalizadaParaUppercase()
    {
        Result<Unidade> resultado = Unidade.Criar(
            "Nome valido",
            new IdentificadoresDaUnidade(UnidadeDeTeste.SlugDoCeps.Valor, "ceps", "0001", null),
            CidadeDaUnidade.Nenhuma,
            new VigenciaDaUnidade(UnidadeDeTeste.InicioDaVigencia, null),
            null,
            TipoUnidade.Centro,
            false);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Sigla.Should().Be("CEPS");
    }
}
