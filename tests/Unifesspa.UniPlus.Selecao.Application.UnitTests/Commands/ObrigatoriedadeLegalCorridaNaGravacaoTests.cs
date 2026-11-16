namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.CenarioDaObrigatoriedadeLegal;

/// <summary>
/// A corrida entre a consulta de unicidade e a gravação só o banco enxerga. Os endpoints têm chave de
/// idempotência, então o handler descarta o rastreamento antes de devolver a recusa: sem o descarte, o
/// <c>SaveChanges</c> automático do Wolverine repete a mesma violação fora do <c>catch</c>, como 500 em vez de 409.
/// </summary>
public sealed class ObrigatoriedadeLegalCorridaNaGravacaoTests
{
    private const string ConstraintDoCodigo = "ux_obrigatoriedades_legais_regra_codigo_ativos";
    private const string ConstraintDoHash = "ux_obrigatoriedades_legais_hash_ativos";

    private readonly CenarioDaObrigatoriedadeLegal _cenario = new();

    [Theory(DisplayName = "Criar descarta o rastreamento e recusa pela constraint violada")]
    [InlineData(ConstraintDoCodigo, "ObrigatoriedadeLegal.RegraCodigoDuplicada")]
    [InlineData(ConstraintDoHash, "ObrigatoriedadeLegal.HashColisao")]
    public async Task Criar_ViolacaoDeUnicidade_DescartaERecusa(string constraint, string codigoEsperado)
    {
        _cenario.UnitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(ViolacaoDeUnicidade(constraint));

        Result<Guid> resultado = await _cenario.CriarAsync(ComandoDeCriar());

        resultado.Error!.Code.Should().Be(codigoEsperado);
        _cenario.UnitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Theory(DisplayName = "Atualizar descarta o rastreamento e recusa pela constraint violada")]
    [InlineData(ConstraintDoCodigo, "ObrigatoriedadeLegal.RegraCodigoDuplicada")]
    [InlineData(ConstraintDoHash, "ObrigatoriedadeLegal.HashColisao")]
    public async Task Atualizar_ViolacaoDeUnicidade_DescartaERecusa(string constraint, string codigoEsperado)
    {
        ObrigatoriedadeLegal regra = RegraGravada();
        _cenario.ComRegraGravada(regra);
        _cenario.UnitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(ViolacaoDeUnicidade(constraint));

        Result resultado = await _cenario.AtualizarAsync(ComandoDeAtualizar(regra.Id));

        resultado.Error!.Code.Should().Be(codigoEsperado);
        _cenario.UnitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "Violação de outra constraint não é engolida nem descarta o rastreamento")]
    public async Task Criar_OutraConstraint_Propaga()
    {
        _cenario.UnitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(ViolacaoDeUnicidade("outro_indice"));

        Func<Task> criar = () => _cenario.CriarAsync(ComandoDeCriar());

        await criar.Should().ThrowAsync<Exception>();
        _cenario.UnitOfWork.DidNotReceive().DescartarAlteracoesNaoSalvas();
    }
}
