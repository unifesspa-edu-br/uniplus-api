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

    private static IdentificadoresDaUnidade ComCodigo(string codigo) => new(UnidadeDeTeste.SlugDoCeps.Valor, "CEPS", codigo, null);

    private static Result AtualizarOCodigo(string codigo) =>
        UnidadeDeTeste.Criar().Value!.Atualizar(
            "Centro de Processos Seletivos",
            new MudancaDeIdentificadores(ComCodigo(codigo), UnidadeDeTeste.InicioDaVigencia.AddMonths(1), null),
            CidadeDaUnidade.Nenhuma,
            null,
            null,
            TipoUnidade.Centro,
            false);

    [Theory(DisplayName = "Criar recusa no campo codigo o código fora do formato que o documento de configuração aceita")]
    [InlineData("CEPS/01")]
    [InlineData("CEPS 01")]
    [InlineData(".CEPS")]
    [InlineData("-CEPS")]
    [InlineData("ÇEPS")]
    [InlineData("7d6c1f3e-0a5b-4c2d-9e8f-1a2b3c4d5e6f")]
    public void Criar_ComCodigoForaDoFormato_RecusaNoCampoCodigo(string codigo)
    {
        Result<Unidade> resultado = UnidadeDeTeste.Criar(ComCodigo(codigo));

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(static causa => (causa.Field, causa.Error.Code)).Should().Equal(("codigo", UnidadeErrorCodes.CodigoFormatoInvalido));
    }

    [Theory(DisplayName = "Atualizar recusa no campo codigo o código fora do formato que o documento de configuração aceita")]
    [InlineData("CEPS/01")]
    [InlineData("CEPS 01")]
    [InlineData(".CEPS")]
    [InlineData("-CEPS")]
    [InlineData("ÇEPS")]
    [InlineData("7d6c1f3e-0a5b-4c2d-9e8f-1a2b3c4d5e6f")]
    public void Atualizar_ComCodigoForaDoFormato_RecusaNoCampoCodigo(string codigo)
    {
        Result resultado = AtualizarOCodigo(codigo);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(static causa => (causa.Field, causa.Error.Code)).Should().Equal(("codigo", UnidadeErrorCodes.CodigoFormatoInvalido));
    }

    [Theory(DisplayName = "Criar aceita o código com letras, números, ponto, hífen e sublinhado")]
    [InlineData("CEPS")]
    [InlineData("CEPS.01")]
    [InlineData("CEPS-01")]
    [InlineData("CEPS_01")]
    [InlineData("11.01.04")]
    public void Criar_ComCodigoNoFormato_Aceita(string codigo) =>
        UnidadeDeTeste.Criar(ComCodigo(codigo)).Value!.Codigo.Should().Be(codigo);

    [Theory(DisplayName = "Atualizar aceita o código com letras, números, ponto, hífen e sublinhado")]
    [InlineData("CEPS")]
    [InlineData("CEPS.01")]
    [InlineData("CEPS-01")]
    [InlineData("CEPS_01")]
    [InlineData("11.01.04")]
    public void Atualizar_ComCodigoNoFormato_Aceita(string codigo) =>
        AtualizarOCodigo(codigo).IsSuccess.Should().BeTrue();

    [Fact(DisplayName = "O código acima do tamanho é recusado como tamanho, e não como formato")]
    public void Criar_ComCodigoMuitoLongo_RecusaPeloTamanho() =>
        UnidadeDeTeste.Criar(ComCodigo(new string('A', 51))).Error!.Code.Should().Be(UnidadeErrorCodes.CodigoTamanho);
}
