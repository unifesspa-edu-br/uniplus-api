namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.CenarioDaGovernancaDaVersao;

/// <summary>
/// As permissões das duas etapas da governança da versão e a recusa de quem não pode decidir a etapa pedida, com a decisão
/// que a trilha recebe em cada caso.
/// </summary>
public sealed class AcessoDasEtapasTests
{
    private static readonly Guid PerfilId = Guid.NewGuid();

    [Theory(DisplayName = "Sem nenhuma das duas permissões, qualquer etapa é recusada por falta de permissão e a trilha recebe as duas negativas")]
    [InlineData(EtapaDaVersao.GestaoDeAcessos)]
    [InlineData(EtapaDaVersao.ProtecaoDeDados)]
    [InlineData(null)]
    public async Task Recusa_SemNenhumaPermissao_SemPermissaoComAsDuasNegativas(EtapaDaVersao? etapa)
    {
        VerificadorDeTeste verificador = new(PerfisDeTeste.Gestor.ContaId, []);

        DomainError? recusa = await RecusaAsync(verificador, [], etapa);

        recusa!.Code.Should().Be(PerfilErrorCodes.SemPermissaoNaEtapa);
        TrilhaDeAcesso.Do(verificador).Select(t => (t.Permissao, t.Resultado)).Should().BeEquivalentTo(
            [(Gestao, ResultadoDoAcesso.Negado), (Protecao, ResultadoDoAcesso.Negado)]);
    }

    [Theory(DisplayName = "Com só a permissão de uma etapa, a etapa dela passa e a outra é recusada como etapa inválida")]
    [InlineData(Gestao, EtapaDaVersao.GestaoDeAcessos, null)]
    [InlineData(Gestao, EtapaDaVersao.ProtecaoDeDados, PerfilErrorCodes.EtapaInvalida)]
    [InlineData(Protecao, EtapaDaVersao.ProtecaoDeDados, null)]
    [InlineData(Protecao, EtapaDaVersao.GestaoDeAcessos, PerfilErrorCodes.EtapaInvalida)]
    public async Task Recusa_ComUmaPermissao_SoPassaNaEtapaDela(string permissao, EtapaDaVersao etapa, string? codigoEsperado)
    {
        VerificadorDeTeste verificador = new(PerfisDeTeste.Gestor.ContaId, [permissao]);

        DomainError? recusa = await RecusaAsync(verificador, [permissao], etapa);

        recusa?.Code.Should().Be(codigoEsperado);
        (recusa is null).Should().Be(codigoEsperado is null);
        TrilhaDeAcesso.Do(verificador).Should().ContainSingle("a trilha recebe a decisão da etapa pedida, não o desfecho das duas");
    }

    [Fact(DisplayName = "Com as duas permissões, qualquer etapa passa")]
    public async Task Recusa_ComAsDuasPermissoes_PassaEmQualquerEtapa()
    {
        VerificadorDeTeste verificador = new(PerfisDeTeste.Gestor.ContaId, [Gestao, Protecao]);

        DomainError? daGestao = await RecusaAsync(verificador, [Gestao, Protecao], EtapaDaVersao.GestaoDeAcessos);
        DomainError? daProtecao = await RecusaAsync(verificador, [Gestao, Protecao], EtapaDaVersao.ProtecaoDeDados);

        (daGestao, daProtecao).Should().Be((null, null));
    }

    [Fact(DisplayName = "Com permissão e sem etapa pedida, a recusa é de etapa inválida e a trilha recebe o desfecho das duas")]
    public async Task Recusa_ComPermissaoSemEtapa_EtapaInvalidaComODesfecho()
    {
        VerificadorDeTeste verificador = new(PerfisDeTeste.Gestor.ContaId, [Gestao]);

        DomainError? recusa = await RecusaAsync(verificador, [Gestao], null);

        recusa!.Code.Should().Be(PerfilErrorCodes.EtapaInvalida);
        TrilhaDeAcesso.Do(verificador).Should().ContainSingle(t => t.Permissao == Gestao).Which.Resultado.Should().Be(ResultadoDoAcesso.Permitido);
    }

    [Theory(DisplayName = "O papel é a gestão de acessos com a permissão dela, mesmo com as duas, e a proteção de dados só com a dela")]
    [InlineData(new[] { Gestao }, EtapaDaVersao.GestaoDeAcessos)]
    [InlineData(new[] { Gestao, Protecao }, EtapaDaVersao.GestaoDeAcessos)]
    [InlineData(new[] { Protecao }, EtapaDaVersao.ProtecaoDeDados)]
    public async Task Papel_PelaPermissaoDaGestao(string[] permitidas, EtapaDaVersao esperado)
    {
        AcessoDasEtapas acesso = (await AcessoDaVersao.ConsultarAsync(
            new VerificadorDeTeste(PerfisDeTeste.Gestor.ContaId, permitidas), PerfilId, CancellationToken.None))!;

        acesso.Papel.Should().Be(esperado);
    }

    [Fact(DisplayName = "Na contenção, quem mantém perfis tem a etapa da gestão e a permissão de aprovar pela gestão não vale")]
    public async Task ConsultarParaContencao_ManterEAGestao()
    {
        AcessoDasEtapas comManter = (await AcessoDaVersao.ConsultarParaContencaoAsync(
            new VerificadorDeTeste(PerfisDeTeste.Gestor.ContaId, [Manter]), PerfilId, CancellationToken.None))!;
        AcessoDasEtapas soComAprovar = (await AcessoDaVersao.ConsultarParaContencaoAsync(
            new VerificadorDeTeste(PerfisDeTeste.Gestor.ContaId, [Gestao]), PerfilId, CancellationToken.None))!;

        (comManter.Gestao, soComAprovar.Gestao).Should().Be((true, false));
    }

    [Fact(DisplayName = "Sem sujeito identificado no token, a consulta devolve nulo")]
    public async Task Consultar_SemSujeito_DevolveNulo()
    {
        IVerificadorDeAcesso verificador = Substitute.For<IVerificadorDeAcesso>();
        verificador.AvaliarAsync(default!, default, default).ReturnsForAnyArgs(Task.FromResult<DecisaoDeAcesso>(SemSujeito.Instancia));

        AcessoDasEtapas? acesso = await AcessoDaVersao.ConsultarAsync(verificador, PerfilId, CancellationToken.None);

        acesso.Should().BeNull();
    }

    private static async Task<DomainError?> RecusaAsync(VerificadorDeTeste verificador, string[] permitidas, EtapaDaVersao? etapa)
    {
        AcessoDasEtapas acesso = (await AcessoDaVersao.ConsultarAsync(verificador, PerfilId, CancellationToken.None))!;

        return await acesso.RecusaDaEtapaAsync(etapa, verificador, CancellationToken.None);
    }
}
