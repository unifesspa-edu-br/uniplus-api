namespace Unifesspa.UniPlus.Publicacoes.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Publicacoes.Application.Commands.AtosNormativos;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;
using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

/// <summary>
/// Os vínculos do ato que emenda outro: os declarados mais os herdados do ato retificado
/// que o ato não repete, em ordem determinística.
/// </summary>
public sealed class CadeiaDeRetificacaoDoAtoVinculosTests
{
    private static readonly Guid Retificado = Guid.CreateVersion7();
    private static readonly Guid Processo = Guid.CreateVersion7();
    private readonly IAtoNormativoRepository _atos = Substitute.For<IAtoNormativoRepository>();

    [Fact(DisplayName = "O vínculo declarado não se repete com o herdado")]
    public async Task Resolver_DeclaradoIgual_NaoRepeteOHerdado()
    {
        EntidadeVinculada igual = new("OBJETO_A", Processo);

        LinhagemDoAto linhagem = await ResolverAsync(declarados: [igual], herdados: [igual]);

        linhagem.Vinculos.Should().Equal(igual);
    }

    [Fact(DisplayName = "Os vínculos saem em ordem ordinal pelo rótulo e depois pelo identificador")]
    public async Task Resolver_Vinculos_OrdenaPeloRotuloOrdinalEDepoisPeloId()
    {
        Guid menor = Guid.Parse("00000000-0000-0000-0000-000000000001");
        Guid maior = Guid.Parse("00000000-0000-0000-0000-000000000002");

        LinhagemDoAto linhagem = await ResolverAsync(
            declarados: [new("B_OBJETO", menor)],
            herdados: [new("B_OBJETO", maior), new("A_OBJETO", maior)]);

        linhagem.Vinculos.Should().Equal(
            new EntidadeVinculada("A_OBJETO", maior),
            new EntidadeVinculada("B_OBJETO", menor),
            new EntidadeVinculada("B_OBJETO", maior));
    }

    [Fact(DisplayName = "A linhagem traz os ids da cadeia do ato retificado")]
    public async Task Resolver_AtoRetificado_TrazOsIdsDaCadeia()
    {
        LinhagemDoAto linhagem = await ResolverAsync(declarados: [], herdados: []);

        linhagem.IdsDaPropriaCadeia.Should().Equal(Retificado);
    }

    [Fact(DisplayName = "Sem ato retificado a linhagem não lê a cadeia")]
    public async Task Resolver_SemAtoRetificado_LinhagemVazia()
    {
        LinhagemDoAto linhagem = await new CadeiaDeRetificacaoDoAto(_atos)
            .ResolverAsync(null, [], [], CancellationToken.None);

        linhagem.IdsDaPropriaCadeia.Should().BeEmpty();
        await _atos.DidNotReceiveWithAnyArgs().ListarIdsDaCadeiaAsync(default, default);
    }

    [Fact(DisplayName = "Os vínculos herdados são os do ato retificado")]
    public async Task ListarVinculosHerdados_DevolveOsDoRetificado()
    {
        EntidadeVinculada herdado = new("OBJETO_A", Processo);
        _atos.ListarVinculosDoAtoAsync(Retificado, Arg.Any<CancellationToken>()).Returns([herdado]);

        IReadOnlyList<EntidadeVinculada> herdados = await new CadeiaDeRetificacaoDoAto(_atos)
            .ListarVinculosHerdadosAsync(Retificado, CancellationToken.None);

        herdados.Should().Equal(herdado);
    }

    private async Task<LinhagemDoAto> ResolverAsync(
        IReadOnlyList<EntidadeVinculada> declarados, IReadOnlyList<EntidadeVinculada> herdados)
    {
        _atos.ListarIdsDaCadeiaAsync(Retificado, Arg.Any<CancellationToken>()).Returns([Retificado]);

        return await new CadeiaDeRetificacaoDoAto(_atos)
            .ResolverAsync(Retificado, declarados, herdados, CancellationToken.None);
    }
}
