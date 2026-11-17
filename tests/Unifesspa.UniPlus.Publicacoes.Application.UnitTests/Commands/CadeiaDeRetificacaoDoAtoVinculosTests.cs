namespace Unifesspa.UniPlus.Publicacoes.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Publicacoes.Application.Commands.AtosNormativos;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;
using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Os vínculos do ato que emenda outro: os declarados, aparados, mais os herdados do ato retificado
/// que o ato não repete, em ordem determinística.
/// </summary>
public sealed class CadeiaDeRetificacaoDoAtoVinculosTests
{
    private static readonly Guid Retificado = Guid.CreateVersion7();
    private static readonly Guid Processo = Guid.CreateVersion7();
    private readonly IAtoNormativoRepository _atos = Substitute.For<IAtoNormativoRepository>();

    [Fact(DisplayName = "O vínculo declarado com espaços nas pontas não se repete com o herdado")]
    public async Task Resolver_DeclaradoComEspacos_NaoRepeteOHerdado()
    {
        HerdaDoRetificado(new EntidadeVinculada("OBJETO_A", Processo));

        LinhagemDoAto linhagem = await Cadeia().ResolverAsync(
            Retificando([new VinculoEntidadeInput("  OBJETO_A  ", Processo)]), CancellationToken.None);

        linhagem.Vinculos.Should().Equal(new EntidadeVinculada("OBJETO_A", Processo));
    }

    [Fact(DisplayName = "Os vínculos saem em ordem ordinal pelo rótulo e depois pelo identificador")]
    public async Task Resolver_Vinculos_OrdenaPeloRotuloOrdinalEDepoisPeloId()
    {
        Guid menor = Guid.Parse("00000000-0000-0000-0000-000000000001");
        Guid maior = Guid.Parse("00000000-0000-0000-0000-000000000002");
        HerdaDoRetificado(new EntidadeVinculada("B_OBJETO", maior), new EntidadeVinculada("A_OBJETO", maior));

        LinhagemDoAto linhagem = await Cadeia().ResolverAsync(
            Retificando([new VinculoEntidadeInput("B_OBJETO", menor)]), CancellationToken.None);

        linhagem.Vinculos.Should().Equal(
            new EntidadeVinculada("A_OBJETO", maior),
            new EntidadeVinculada("B_OBJETO", menor),
            new EntidadeVinculada("B_OBJETO", maior));
    }

    private static RegistrarAtoNormativoCommand Retificando(IReadOnlyList<VinculoEntidadeInput> vinculos) =>
        new(
            Orgao: "CEPS",
            Serie: "EDITAL",
            Ano: 2026,
            Numero: "13",
            TipoCodigo: "EDITAL_ABERTURA",
            DataDoAto: new DateOnly(2026, 3, 13),
            DocumentoHash: AtoNormativoDeTeste.HashValido,
            Assinante: PersonasDeTeste.NomeKevinCarvalho,
            AtoRetificadoId: Retificado,
            MotivoRetificacao: "corrige o anexo",
            Vinculos: vinculos);

    private CadeiaDeRetificacaoDoAto Cadeia() => new(_atos);

    private void HerdaDoRetificado(params EntidadeVinculada[] herdados)
    {
        _atos.ListarIdsDaCadeiaAsync(Retificado, Arg.Any<CancellationToken>()).Returns([Retificado]);
        _atos.ListarVinculosDoAtoAsync(Retificado, Arg.Any<CancellationToken>()).Returns(herdados);
    }
}
