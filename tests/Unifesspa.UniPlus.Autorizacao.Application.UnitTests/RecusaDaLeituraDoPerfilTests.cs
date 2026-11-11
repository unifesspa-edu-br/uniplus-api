namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Application.Queries.Perfis;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.CenarioDaGovernancaDaVersao;

public sealed class RecusaDaLeituraDoPerfilTests
{
    [Fact(DisplayName = "Quem aprova pela proteção de dados lê a coleção e a trilha registra a permissão que governou")]
    public async Task Conferir_PelaProtecaoDeDados_PermiteERegistraAPermissaoQueGovernou()
    {
        VerificadorDeTeste verificador = new(PerfisDeTeste.ProtecaoDeDados.Sub, [Protecao]);

        DomainError? recusa = await RecusaDaLeituraDoPerfil.ConferirAsync(verificador, null, CancellationToken.None);

        recusa.Should().BeNull();
        verificador.Gravados.Should().ContainSingle().Which
            .Should().OnlyContain(r => r.Resultado == ResultadoDoAcesso.Permitido && r.Permissao == Protecao);
    }

    [Fact(DisplayName = "Quem aprova pela gestão de acessos lê o perfil e a trilha registra a permissão que governou")]
    public async Task Conferir_PelaGestaoDeAcessos_PermiteERegistraAPermissaoQueGovernou()
    {
        VerificadorDeTeste verificador = new(PerfisDeTeste.Gestora.Sub, [Gestao]);

        DomainError? recusa = await RecusaDaLeituraDoPerfil.ConferirAsync(verificador, Guid.NewGuid(), CancellationToken.None);

        recusa.Should().BeNull();
        verificador.Gravados.Should().ContainSingle().Which
            .Should().OnlyContain(r => r.Resultado == ResultadoDoAcesso.Permitido && r.Permissao == Gestao);
    }

    [Fact(DisplayName = "Quem mantém perfis lê o perfil e a trilha registra a permissão de manter")]
    public async Task Conferir_PelaManutencao_PermiteERegistraAPermissaoQueGovernou()
    {
        VerificadorDeTeste verificador = new(PerfisDeTeste.Gestora.Sub, [Manter]);

        DomainError? recusa = await RecusaDaLeituraDoPerfil.ConferirAsync(verificador, Guid.NewGuid(), CancellationToken.None);

        recusa.Should().BeNull();
        verificador.Gravados.Should().ContainSingle().Which
            .Should().OnlyContain(r => r.Resultado == ResultadoDoAcesso.Permitido && r.Permissao == Manter);
    }

    [Fact(DisplayName = "Sem nenhuma das três permissões a leitura é recusada e a trilha registra as negativas")]
    public async Task Conferir_SemPermissao_RecusaERegistraAsNegativas()
    {
        VerificadorDeTeste verificador = new(PerfisDeTeste.Autor.Sub, []);

        DomainError? recusa = await RecusaDaLeituraDoPerfil.ConferirAsync(verificador, Guid.NewGuid(), CancellationToken.None);

        recusa!.Code.Should().Be(PerfilErrorCodes.LeituraNegada);
        verificador.Gravados.Should().ContainSingle().Which
            .Should().HaveCount(3).And.OnlyContain(r => r.Resultado == ResultadoDoAcesso.Negado);
    }
}
