namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using NSubstitute;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Resolvedor de unidade responsável de teste: as unidades de <see cref="UnidadesDeTeste"/> estão
/// vivas, qualquer outra não existe mais no cadastro.
/// </summary>
internal static class ResolvedorDeUnidadeDeTeste
{
    public static IResolvedorDaUnidadeResponsavel ComAsUnidadesDeTeste() => Com(UnidadesDeTeste.Ceps, UnidadesDeTeste.Cga);

    /// <summary>Só as unidades informadas estão vivas, com a sigla e o nome informados.</summary>
    public static IResolvedorDaUnidadeResponsavel Com(params UnidadeResponsavelSnapshot[] vivas)
    {
        IResolvedorDaUnidadeResponsavel resolvedor = Substitute.For<IResolvedorDaUnidadeResponsavel>();
        resolvedor.ResolverAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(chamada => Task.FromResult<UnidadeResponsavelSnapshot?>(
                vivas.FirstOrDefault(u => u.OrigemId == chamada.Arg<Guid>())));
        return resolvedor;
    }
}
