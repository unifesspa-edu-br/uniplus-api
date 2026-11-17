namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.UnitTests.Queries;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Consultas;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Queries.Unidades;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;

public sealed class FiltroDaListagemDeUnidadesTests
{
    private const int Centro = (int)TipoUnidade.Centro;
    private const int TipoInexistente = 999;

    private static readonly string BuscaNoLimite = new('a', BuscaPedida.ComprimentoMaximo);
    private static readonly string BuscaAcimaDoLimite = new('a', BuscaPedida.ComprimentoMaximo + 1);

    [Fact(DisplayName = "A busca no limite de 200 caracteres e os tipos válidos viram o filtro, com o termo como veio")]
    public void BuscaNoLimiteETiposValidos_ViramOFiltro()
    {
        Result<FiltroListagemUnidades> resultado = FiltroDaListagemDeUnidades.Resolver(BuscaNoLimite, [Centro]);

        resultado.Value!.Termo.Should().Be(BuscaNoLimite);
        resultado.Value.Tipos.Should().Equal(TipoUnidade.Centro);
    }

    [Fact(DisplayName = "A busca acima de 200 caracteres é recusada com o código de busca muito longa")]
    public void BuscaAcimaDoLimite_EhRecusada()
    {
        Result<FiltroListagemUnidades> resultado = FiltroDaListagemDeUnidades.Resolver(BuscaAcimaDoLimite, [Centro]);

        resultado.Error!.Code.Should().Be(ConsultaErrorCodes.BuscaMuitoLonga);
    }

    [Fact(DisplayName = "Com a busca longa e o tipo inválido juntos, vale a recusa da busca")]
    public void BuscaLongaETipoInvalido_ValeARecusaDaBusca()
    {
        Result<FiltroListagemUnidades> resultado = FiltroDaListagemDeUnidades.Resolver(BuscaAcimaDoLimite, [TipoInexistente]);

        resultado.Error!.Code.Should().Be(ConsultaErrorCodes.BuscaMuitoLonga);
    }

    [Fact(DisplayName = "Com a busca válida e o tipo inválido, vale a recusa do tipo")]
    public void BuscaValidaETipoInvalido_ValeARecusaDoTipo()
    {
        Result<FiltroListagemUnidades> resultado = FiltroDaListagemDeUnidades.Resolver("ceps", [TipoInexistente]);

        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.FiltroDeTipoInvalido);
    }

    [Theory(DisplayName = "A busca ausente ou em branco não filtra por texto")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BuscaAusenteOuEmBranco_NaoFiltraPorTexto(string? busca)
    {
        Result<FiltroListagemUnidades> resultado = FiltroDaListagemDeUnidades.Resolver(busca, null);

        resultado.Value!.Termo.Should().BeNull();
        resultado.Value.Tipos.Should().BeEmpty();
    }

    [Fact(DisplayName = "A busca só de espaços acima de 200 caracteres é recusada: o comprimento é o bruto")]
    public void BuscaSoDeEspacosAcimaDoLimite_EhRecusada()
    {
        Result<FiltroListagemUnidades> resultado = FiltroDaListagemDeUnidades.Resolver(new string(' ', BuscaPedida.ComprimentoMaximo + 1), null);

        resultado.Error!.Code.Should().Be(ConsultaErrorCodes.BuscaMuitoLonga);
    }
}
