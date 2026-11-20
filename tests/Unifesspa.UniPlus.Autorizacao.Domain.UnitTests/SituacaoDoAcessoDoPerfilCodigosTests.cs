namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class SituacaoDoAcessoDoPerfilCodigosTests
{
    [Fact(DisplayName = "Cada situação do acesso do perfil tem o código que o contrato publica")]
    public void Codigo_DeCadaSituacao_EOQueOContratoPublica() =>
        Enum.GetValues<SituacaoDoAcessoDoPerfil>().ToDictionary(static v => v, static v => v.Codigo())
            .Should().Equal(new Dictionary<SituacaoDoAcessoDoPerfil, string>
            {
                [SituacaoDoAcessoDoPerfil.Suspenso] = "suspenso",
                [SituacaoDoAcessoDoPerfil.EmVigor] = "em_vigor",
                [SituacaoDoAcessoDoPerfil.SemVersaoEmVigor] = "sem_versao_em_vigor",
            });

    [Fact(DisplayName = "Toda situação do acesso do perfil tem rótulo")]
    public void Rotulo_DeCadaSituacao_NaoEstaVazio() =>
        Enum.GetValues<SituacaoDoAcessoDoPerfil>().Should().OnlyContain(static v => !string.IsNullOrWhiteSpace(v.Rotulo()));

    [Fact(DisplayName = "Os códigos de contrato dos enums do domínio da autorização passam na conferência da subida")]
    public void ConferirEnumsDe_DominioDaAutorizacao_Passa() =>
        FluentActions.Invoking(() => EnumsDeContrato.ConferirEnumsDe([typeof(SituacaoDoAcessoDoPerfil).Assembly])).Should().NotThrow();
}
