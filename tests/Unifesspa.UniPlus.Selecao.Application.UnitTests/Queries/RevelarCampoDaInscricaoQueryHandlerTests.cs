namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Application.Queries.CampoDaInscricao;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>A revelação devolve o CPF no formato de CPF e trata o valor decifrado vazio como campo não encontrado.</summary>
public sealed class RevelarCampoDaInscricaoQueryHandlerTests
{
    [Fact(DisplayName = "O fato de formato CPF é revelado formatado")]
    public async Task Revela_Cpf_Formatado()
    {
        Result<CampoReveladoDto> resultado = await Revelar(PersonasDeTeste.FernandaDrumond.Valor);

        resultado.Value!.Valor.Should().Be(PersonasDeTeste.FernandaDrumond.Formatado);
    }

    [Theory(DisplayName = "O valor decifrado nulo ou vazio responde campo não encontrado, e não erro do servidor")]
    [InlineData(null)]
    [InlineData("")]
    public async Task ValorVazio_NaoEncontrado(string? decifrado)
    {
        Result<CampoReveladoDto> resultado = await Revelar(decifrado);

        resultado.IsFailure.Should().BeTrue();
    }

    private static async Task<Result<CampoReveladoDto>> Revelar(string? decifrado)
    {
        InscricaoAdministrativaDeTeste cenario = new InscricaoAdministrativaDeTeste(PersonasDeTeste.FernandaDrumond)
            .Fato("CPF", "IDENTIFICADOR", TipoRenderizacao.Texto, null, formato: "CPF");
        InscricaoAdministrativaDeTeste.Mundo mundo = cenario.Montar();
        mundo.Inscricoes.LocalizarIdentificadorAsync(cenario.InscricaoId, "CPF", null, null, Arg.Any<CancellationToken>())
            .Returns(new RespostaIdentificadora(Guid.CreateVersion7()));
        mundo.Inscricoes.LerIdentificadorAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(decifrado);
        DecisoesDeAcessoDeTeste persona = new("diogo.souza", PermissaoSelecao.DadosIdentificadoresRevelar);
        IUserContext usuario = Substitute.For<IUserContext>();

        return await RevelarCampoDaInscricaoQueryHandler.Handle(
            new RevelarCampoDaInscricaoQuery(cenario.ProcessoId, cenario.InscricaoId, "CPF", null),
            persona.Verificador, mundo.Processos, mundo.Inscricoes, mundo.Codecs, usuario, NullLogger.Instance, CancellationToken.None);
    }
}
