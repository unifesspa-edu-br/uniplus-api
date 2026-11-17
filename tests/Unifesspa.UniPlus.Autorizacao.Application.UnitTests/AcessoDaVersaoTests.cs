namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Apoio;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Application.Permissoes;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.CenarioDaGovernancaDaVersao;

public sealed class AcessoDaVersaoTests
{
    private static readonly Guid PerfilId = Guid.NewGuid();
    private static readonly DomainError SemPermissao = MigracaoErros.SemPermissaoNaEtapa();

    [Fact(DisplayName = "Conferir a etapa sem autor identificado recusa pelo autor e não registra nada")]
    public async Task ConferirEtapa_SemAutor_RecusaPeloAutorSemRegistrar()
    {
        IVerificadorDeAcesso verificador = Substitute.For<IVerificadorDeAcesso>();
        verificador.AvaliarAsync(default!, default, default).ReturnsForAnyArgs(Task.FromResult<DecisaoDeAcesso>(SemSujeito.Instancia));

        DomainError? recusa = await Conferir(verificador, EtapaDaVersao.GestaoDeAcessos);

        recusa!.Code.Should().Be(PerfilErrorCodes.AutorNaoIdentificado);
        await verificador.DidNotReceiveWithAnyArgs().RegistrarDesfechoAsync(default!, default, default);
    }

    [Theory(DisplayName = "Conferir a etapa de quem a decide não recusa e registra a decisão permitida da etapa")]
    [InlineData(EtapaDaVersao.GestaoDeAcessos, Gestao)]
    [InlineData(EtapaDaVersao.ProtecaoDeDados, Protecao)]
    public async Task ConferirEtapa_ComPermissaoDaEtapa_NaoRecusaERegistraAPermitida(EtapaDaVersao etapa, string permissao)
    {
        VerificadorDeTeste verificador = new(PerfisDeTeste.Gestor.ContaId, [permissao]);

        DomainError? recusa = await Conferir(verificador, etapa);

        recusa.Should().BeNull();
        TrilhaDeAcesso.Do(verificador).Should().ContainSingle(entrada => entrada.Permissao == permissao).Which.Resultado.Should().Be(ResultadoDoAcesso.Permitido);
    }

    [Theory(DisplayName = "Conferir a etapa sem a permissão dela devolve a recusa dada e registra a negada da etapa")]
    [InlineData(EtapaDaVersao.GestaoDeAcessos, Gestao, Protecao)]
    [InlineData(EtapaDaVersao.ProtecaoDeDados, Protecao, Gestao)]
    public async Task ConferirEtapa_SemPermissaoDaEtapa_DevolveARecusaERegistraANegada(EtapaDaVersao etapa, string daEtapa, string daOutra)
    {
        VerificadorDeTeste verificador = new(PerfisDeTeste.Gestor.ContaId, [daOutra]);

        DomainError? recusa = await Conferir(verificador, etapa);

        recusa.Should().BeSameAs(SemPermissao);
        TrilhaDeAcesso.Do(verificador).Should().ContainSingle(entrada => entrada.Permissao == daEtapa).Which.Resultado.Should().Be(ResultadoDoAcesso.Negado);
    }

    [Fact(DisplayName = "Conferir a etapa registra a decisão sobre o recurso do tipo dado")]
    public async Task ConferirEtapa_TipoDoRecurso_RegistraSobreORecursoDoTipo()
    {
        VerificadorDeTeste verificador = new(PerfisDeTeste.Gestor.ContaId, [Gestao]);

        await AcessoDaVersao.ConferirEtapaAsync(
            verificador, PerfilId, ResourceTiposAutorizacao.Reclassificacao, EtapaDaVersao.GestaoDeAcessos, SemPermissao, CancellationToken.None);

        TrilhaDeAcesso trilha = TrilhaDeAcesso.Do(verificador).Single(entrada => entrada.Permissao == Gestao);
        trilha.ResourceTipo.Should().Be(ResourceTiposAutorizacao.Reclassificacao);
        trilha.ResourceId.Should().Be(PerfilId);
    }

    private static Task<DomainError?> Conferir(IVerificadorDeAcesso verificador, EtapaDaVersao etapa) =>
        AcessoDaVersao.ConferirEtapaAsync(verificador, PerfilId, ResourceTiposAutorizacao.Perfil, etapa, SemPermissao, CancellationToken.None);
}
