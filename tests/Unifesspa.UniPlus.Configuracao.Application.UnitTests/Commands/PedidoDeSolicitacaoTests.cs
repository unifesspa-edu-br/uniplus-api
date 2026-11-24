namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class PedidoDeSolicitacaoTests
{
    [Fact]
    public void Ler_CadastroDoDadoDoCandidato_AceitaERecusaTokenAntigo()
    {
        Guid id = Guid.NewGuid();
        Result<PedidoDeSolicitacao> aceito = PedidoDeSolicitacao.Ler([new("DADO_DO_CANDIDATO", id)]);
        Result<PedidoDeSolicitacao> recusado = PedidoDeSolicitacao.Ler([new("FATO", id)]);

        aceito.Value!.Itens.Should().ContainSingle().Which.Item.Cadastro.Should().Be(CadastroDoCatalogo.Fato);
        recusado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == "itens[0].cadastro" && e.Error.Code == ProtecaoDoCatalogoErrorCodes.CadastroInvalido);
    }
}
