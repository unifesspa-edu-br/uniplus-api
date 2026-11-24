namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>A fase canônica viva do cadastro como a origem que a fase do cronograma congela: o id, o código e os traços que ela herda.</summary>
internal static class OrigemCanonicaDaFaseDoCadastro
{
    internal static OrigemCanonicaDaFase De(FaseCanonicaView canonica) => new(
        canonica.Id,
        canonica.Codigo,
        CatalogoDe<OrigemDataFase>.Obter(canonica.OrigemData),
        canonica.AgrupaEtapas,
        canonica.PermiteComplementacao,
        canonica.ColetaInscricao,
        canonica.ColetaSolicitacaoIsencao)
    { Nome = canonica.Nome };
}
