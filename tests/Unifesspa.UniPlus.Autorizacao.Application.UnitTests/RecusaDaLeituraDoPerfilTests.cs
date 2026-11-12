namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Application.Permissoes;
using Unifesspa.UniPlus.Autorizacao.Application.Queries.Perfis;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.CenarioDaGovernancaDaVersao;

public sealed class RecusaDaLeituraDoPerfilTests
{
    private const string ConsultarVersoes = "autorizacao:perfis:consultar-versoes";

    private static readonly Guid PerfilId = Guid.CreateVersion7();

    [Fact(DisplayName = "Quem aprova pela proteção de dados lê a coleção e a trilha registra a permissão que governou")]
    public async Task Conferir_PelaProtecaoDeDados_PermiteERegistraAPermissaoQueGovernou()
    {
        VerificadorDeTeste verificador = new(PerfisDeTeste.ProtecaoDeDados.Sub, [Protecao]);

        DomainError? recusa = await RecusaDaLeituraDoPerfil.ConferirAsync(verificador, null, CancellationToken.None);

        recusa.Should().BeNull();
        TrilhaDeAcesso.Do(verificador).Should().Equal(
            new TrilhaDeAcesso(PerfisDeTeste.ProtecaoDeDados.Sub, Protecao, ResourceTipos.Operacao, null, ResultadoDoAcesso.Permitido));
    }

    [Fact(DisplayName = "Quem aprova pela gestão de acessos lê o perfil e a trilha registra a permissão sobre ele")]
    public async Task Conferir_PelaGestaoDeAcessos_PermiteERegistraAPermissaoSobreOPerfil()
    {
        VerificadorDeTeste verificador = new(PerfisDeTeste.Gestor.Sub, [Gestao]);

        DomainError? recusa = await RecusaDaLeituraDoPerfil.ConferirAsync(verificador, PerfilId, CancellationToken.None);

        recusa.Should().BeNull();
        TrilhaDeAcesso.Do(verificador).Should().Equal(SobreOPerfil(PerfisDeTeste.Gestor.Sub, Gestao, ResultadoDoAcesso.Permitido));
    }

    [Fact(DisplayName = "Quem mantém perfis lê o perfil sem registro, por ser operação do dia a dia sobre dado não pessoal")]
    public async Task Conferir_PelaManutencao_PermiteSemRegistrar()
    {
        VerificadorDeTeste verificador = new(PerfisDeTeste.Gestor.Sub, [Manter]);

        DomainError? recusa = await RecusaDaLeituraDoPerfil.ConferirAsync(verificador, PerfilId, CancellationToken.None);

        recusa.Should().BeNull();
        TrilhaDeAcesso.Do(verificador).Should().BeEmpty();
    }

    [Fact(DisplayName = "Sem nenhuma das quatro permissões a leitura é recusada e a trilha registra as negativas sobre o perfil")]
    public async Task Conferir_SemPermissao_RecusaERegistraAsNegativas()
    {
        VerificadorDeTeste verificador = new(PerfisDeTeste.Autor.Sub, []);

        DomainError? recusa = await RecusaDaLeituraDoPerfil.ConferirAsync(verificador, PerfilId, CancellationToken.None);

        recusa!.Code.Should().Be(PerfilErrorCodes.LeituraNegada);
        TrilhaDeAcesso.Do(verificador).Should().BeEquivalentTo(
            new[] { Manter, Gestao, Protecao, ConsultarVersoes }.Select(p => SobreOPerfil(PerfisDeTeste.Autor.Sub, p, ResultadoDoAcesso.Negado)));
    }

    private static TrilhaDeAcesso SobreOPerfil(string ator, string permissao, ResultadoDoAcesso resultado) =>
        new(ator, permissao, ResourceTiposAutorizacao.Perfil, PerfilId, resultado);
}
