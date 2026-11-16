namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.CenarioDaObrigatoriedadeLegal;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.LeitoresDeCadastroParaObrigatoriedadeLegal;

/// <summary>
/// A atualização confere as referências do predicado com o mesmo rigor da criação: aceitar numa o que a outra
/// recusa seria deixar a regra inválida entrar pela porta dos fundos, e uma regra que referencia cadastro
/// inexistente é aprovada por vacuidade na hora de publicar.
/// </summary>
public sealed class AtualizarObrigatoriedadeLegalCommandHandlerTests
{
    private const string ModalidadeAusente = "LB_PPl";
    private const string TipoDocumentoAusente = "LAUDO_INEXISTENTE";

    private readonly CenarioDaObrigatoriedadeLegal _cenario = new();
    private readonly ObrigatoriedadeLegal _regra = RegraGravada();

    public AtualizarObrigatoriedadeLegalCommandHandlerTests() => _cenario.ComRegraGravada(_regra);

    [Fact(DisplayName = "Modalidade inexistente no predicado recusa e preserva o predicado anterior")]
    public async Task Handle_ModalidadeInexistente_RecusaEPreservaPredicado()
    {
        PredicadoObrigatoriedade anterior = _regra.Predicado;
        _cenario.Modalidades = ModalidadeReaderSem(ModalidadeAusente);

        Result resultado = await _cenario.AtualizarAsync(
            ComandoDeAtualizar(_regra.Id, new DocumentoObrigatorioParaModalidade(ModalidadeAusente, "LAUDO_MEDICO")));

        resultado.Error!.Code.Should().Be("ObrigatoriedadeLegal.ModalidadeNaoEncontrada");
        _regra.Predicado.Should().Be(anterior, "a recusa acontece antes de Atualizar");
        await _cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Tipo de documento inexistente é recusado com o erro do seu próprio campo")]
    public async Task Handle_TipoDocumentoInexistente_RecusaComErroProprio()
    {
        _cenario.TiposDeDocumento = TipoDocumentoReaderSem(TipoDocumentoAusente);

        Result resultado = await _cenario.AtualizarAsync(
            ComandoDeAtualizar(_regra.Id, new DocumentoObrigatorioParaModalidade("LB_PPI", TipoDocumentoAusente)));

        resultado.Error!.Code.Should().Be("ObrigatoriedadeLegal.TipoDocumentoNaoEncontrado");
        await _cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Códigos com espaço supérfluo são gravados normalizados na atualização")]
    public async Task Handle_CodigosComEspaco_AtualizaNormalizado()
    {
        Result resultado = await _cenario.AtualizarAsync(
            ComandoDeAtualizar(_regra.Id, new DocumentoObrigatorioParaModalidade(" LB_PPI ", " LAUDO_MEDICO ")));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        _regra.Predicado.Should().Be(new DocumentoObrigatorioParaModalidade("LB_PPI", "LAUDO_MEDICO"));
    }

    [Fact(DisplayName = "Código de regra de outra regra ativa é recusado como duplicado, excluída a própria")]
    public async Task Handle_RegraCodigoDeOutraRegra_Recusa()
    {
        _cenario.Repositorio.ExisteRegraCodigoAtivoAsync("REGRA_GRAVADA", _regra.Id, Arg.Any<CancellationToken>()).Returns(true);

        Result resultado = await _cenario.AtualizarAsync(ComandoDeAtualizar(_regra.Id));

        resultado.Error!.Code.Should().Be("ObrigatoriedadeLegal.RegraCodigoDuplicada");
        _cenario.Repositorio.DidNotReceive().Atualizar(Arg.Any<ObrigatoriedadeLegal>());
    }
}
