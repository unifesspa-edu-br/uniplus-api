namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Apoio;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDePerfis;

public sealed class AutoriaDaOperacaoTests
{
    [Fact(DisplayName = "A autoria exigida de quem o token identifica traz a pessoa e se tem concessão do perfil")]
    public async Task ExigirDaVersao_TokenIdentificado_TrazAPessoaEAConcessao()
    {
        AutoriaDaOperacao autoria = Autoria(PerfisDeTeste.Autor, temConcessao: true);

        Result<AutoriaDaVersao> resultado = await autoria.ExigirDaVersaoAsync(Guid.NewGuid(), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Pessoa.Should().Be(PerfisDeTeste.Autor);
        resultado.Value.TemConcessao.Should().BeTrue();
    }

    [Fact(DisplayName = "A autoria exigida sem perfil não tem concessão")]
    public async Task ExigirDaVersao_SemPerfil_NaoTemConcessao()
    {
        AutoriaDaOperacao autoria = Autoria(PerfisDeTeste.Autor, temConcessao: true);

        Result<AutoriaDaVersao> resultado = await autoria.ExigirDaVersaoAsync(null, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.TemConcessao.Should().BeFalse();
    }

    [Fact(DisplayName = "A autoria exigida de token sem identidade recusa por autor não identificado")]
    public async Task ExigirDaVersao_TokenSemIdentidade_RecusaPorAutorNaoIdentificado()
    {
        AutoriaDaOperacao autoria = Autoria(null, temConcessao: true);

        Result<AutoriaDaVersao> resultado = await autoria.ExigirDaVersaoAsync(Guid.NewGuid(), CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(PerfilErrorCodes.AutorNaoIdentificado);
    }

    private static AutoriaDaOperacao Autoria(Identidade? pessoa, bool temConcessao) =>
        new(new Usuario(pessoa), TimeProvider.System, new ConcessoesNaMemoria(temConcessao));
}
