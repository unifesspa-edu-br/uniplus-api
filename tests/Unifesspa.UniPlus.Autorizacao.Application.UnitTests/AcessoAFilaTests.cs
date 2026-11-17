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
        VerificadorDeTeste verificador = new(PerfisDeTeste.Gestor.ContaId, [deOutraEtapa]);

        DomainError? recusa = await AcessoAFila.NegadoAsync(verificador, etapa, CancellationToken.None);

        recusa!.Code.Should().Be(PerfilErrorCodes.SemPermissaoNaEtapa);
        TrilhaDeAcesso.Do(verificador).Should().Equal(NaOperacao(permissaoDaEtapa, ResultadoDoAcesso.Negado));
    }

    [Theory(DisplayName = "A fila não recusa quem tem a permissão da etapa e registra a decisão permitida")]
    [InlineData(EtapaDaVersao.GestaoDeAcessos, Gestao)]
    [InlineData(EtapaDaVersao.ProtecaoDeDados, Protecao)]
    public async Task Negado_ComPermissaoDaEtapa_NaoRecusaERegistraAPermitida(EtapaDaVersao etapa, string permissaoDaEtapa)
    {
        VerificadorDeTeste verificador = new(PerfisDeTeste.Gestor.ContaId, [permissaoDaEtapa]);

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

    [Theory(DisplayName = "A etapa ausente ou desconhecida é recusada como etapa inválida, antes de qualquer registro")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("outra")]
    public async Task Autorizar_EtapaInvalida_RecusaSemRegistrar(string? etapa)
    {
        VerificadorDeTeste verificador = new(PerfisDeTeste.Gestor.ContaId, [Gestao, Protecao]);

        Result<EtapaDaVersao> resultado = await AcessoAFila.AutorizarAsync(etapa, verificador, CancellationToken.None);

        resultado.Error!.Code.Should().Be(PerfilErrorCodes.EtapaInvalida);
        verificador.Gravados.Should().BeEmpty();
    }

    [Theory(DisplayName = "Quem tem a permissão da etapa pedida recebe a etapa e a decisão permitida vai para a trilha")]
    [InlineData(EtapaDaVersao.GestaoDeAcessos, Gestao)]
    [InlineData(EtapaDaVersao.ProtecaoDeDados, Protecao)]
    public async Task Autorizar_ComPermissao_DevolveAEtapa(EtapaDaVersao esperada, string permissao)
    {
        VerificadorDeTeste verificador = new(PerfisDeTeste.Gestor.ContaId, [permissao]);

        Result<EtapaDaVersao> resultado = await AcessoAFila.AutorizarAsync(esperada.ToCodigo(), verificador, CancellationToken.None);

        resultado.Value.Should().Be(esperada);
        TrilhaDeAcesso.Do(verificador).Should().Equal(NaOperacao(permissao, ResultadoDoAcesso.Permitido));
    }

    [Fact(DisplayName = "Quem não tem a permissão da etapa pedida recebe a recusa dela, com a negativa na trilha")]
    public async Task Autorizar_SemPermissao_RecusaERegistraANegativa()
    {
        VerificadorDeTeste verificador = new(PerfisDeTeste.Gestor.ContaId, [Protecao]);

        Result<EtapaDaVersao> resultado = await AcessoAFila.AutorizarAsync(
            EtapaDaVersao.GestaoDeAcessos.ToCodigo(), verificador, CancellationToken.None);

        resultado.Error!.Code.Should().Be(PerfilErrorCodes.SemPermissaoNaEtapa);
        TrilhaDeAcesso.Do(verificador).Should().Equal(NaOperacao(Gestao, ResultadoDoAcesso.Negado));
    }

    private static TrilhaDeAcesso NaOperacao(string permissao, ResultadoDoAcesso resultado) =>
        new(PerfisDeTeste.Gestor.ContaId, permissao, ResourceTipos.Operacao, null, resultado);
}
