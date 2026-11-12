namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Application.Queries.Perfis;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.CenarioDaGovernancaDaVersao;

public sealed class AcessoAFilaTests
{
    [Theory(DisplayName = "A fila recusa quem não tem a permissão da etapa e registra a negativa dela")]
    [InlineData(EtapaDaVersao.GestaoDeAcessos, Gestao, Protecao)]
    [InlineData(EtapaDaVersao.ProtecaoDeDados, Protecao, Gestao)]
    public async Task Negado_SemPermissaoDaEtapa_RecusaERegistraANegativa(EtapaDaVersao etapa, string permissaoDaEtapa, string deOutraEtapa)
    {
        VerificadorDeTeste verificador = new(PerfisDeTeste.Gestor.Sub, [deOutraEtapa]);

        DomainError? recusa = await AcessoAFila.NegadoAsync(verificador, etapa, CancellationToken.None);

        recusa!.Code.Should().Be(PerfilErrorCodes.SemPermissaoNaEtapa);
        TrilhaDeAcesso.Do(verificador).Should().Equal(NaOperacao(permissaoDaEtapa, ResultadoDoAcesso.Negado));
    }

    [Theory(DisplayName = "A fila não recusa quem tem a permissão da etapa e registra a decisão permitida")]
    [InlineData(EtapaDaVersao.GestaoDeAcessos, Gestao)]
    [InlineData(EtapaDaVersao.ProtecaoDeDados, Protecao)]
    public async Task Negado_ComPermissaoDaEtapa_NaoRecusaERegistraAPermitida(EtapaDaVersao etapa, string permissaoDaEtapa)
    {
        VerificadorDeTeste verificador = new(PerfisDeTeste.Gestor.Sub, [permissaoDaEtapa]);

        DomainError? recusa = await AcessoAFila.NegadoAsync(verificador, etapa, CancellationToken.None);

        recusa.Should().BeNull();
        TrilhaDeAcesso.Do(verificador).Should().Equal(NaOperacao(permissaoDaEtapa, ResultadoDoAcesso.Permitido));
    }

    [Theory(DisplayName = "A fila recusa o token sem autor pela identificação e não registra nada")]
    [InlineData(EtapaDaVersao.GestaoDeAcessos)]
    [InlineData(EtapaDaVersao.ProtecaoDeDados)]
    public async Task Negado_SemAutor_RecusaPeloTokenSemRegistrar(EtapaDaVersao etapa)
    {
        IVerificadorDeAcesso verificador = Substitute.For<IVerificadorDeAcesso>();
        verificador.AvaliarAsync(default!, default, default).ReturnsForAnyArgs(Task.FromResult<DecisaoDeAcesso>(SemSujeito.Instancia));

        DomainError? recusa = await AcessoAFila.NegadoAsync(verificador, etapa, CancellationToken.None);

        recusa!.Code.Should().Be(PerfilErrorCodes.AutorNaoIdentificado);
        await verificador.DidNotReceiveWithAnyArgs().RegistrarDesfechoAsync(default!, default, default);
    }

    private static TrilhaDeAcesso NaOperacao(string permissao, ResultadoDoAcesso resultado) =>
        new(PerfisDeTeste.Gestor.Sub, permissao, ResourceTipos.Operacao, null, resultado);
}
