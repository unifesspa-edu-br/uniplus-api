namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Enums;

/// <summary>
/// A cópia congelada da fase canônica de onde uma <see cref="Entities.FaseCronograma"/> veio
/// (ADR-0061): o id de origem sem chave estrangeira, o código e os traços que a fase herda do
/// cadastro. É entrada de fábrica de vida curta, só dados; a fase copia os valores e não guarda o
/// objeto.
/// </summary>
public sealed record OrigemCanonicaDaFase(
    Guid OrigemId,
    string Codigo,
    OrigemDataFase OrigemData,
    bool AgrupaEtapas,
    bool PermiteComplementacao,
    bool ColetaInscricao,
    bool ColetaSolicitacaoIsencao,
    bool OrganizadaEmChamadas)
{
    public string Nome { get; init; } = string.Empty;
}
