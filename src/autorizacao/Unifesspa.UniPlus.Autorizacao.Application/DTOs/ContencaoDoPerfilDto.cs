namespace Unifesspa.UniPlus.Autorizacao.Application.DTOs;

/// <summary>Corpo da suspensão, da desativação e da reativação do perfil: o motivo é obrigatório, de até 500 caracteres.</summary>
public sealed record MotivoDaContencaoRequest(string? Motivo);

/// <summary>Corpo da decisão de uma etapa da retomada: <c>gestao_de_acessos</c> ou <c>protecao_de_dados</c>, com o motivo obrigatório.</summary>
public sealed record RetomadaDaSuspensaoRequest(string? Etapa, string? Motivo);

/// <summary>O que a decisão de uma etapa deixou: se a suspensão se fechou e as etapas que ainda faltam decidir.</summary>
public sealed record RetomadaDaSuspensaoDto(bool Concluida, IReadOnlyList<string> EtapasPendentes);

/// <summary>A suspensão aberta do perfil, sem o motivo nem as pessoas, que ficam na trilha da contenção.</summary>
/// <param name="Desde">Quando o perfil foi suspenso.</param>
/// <param name="PapelDeQuemSuspendeu">A permissão que governou a suspensão: <c>gestao_de_acessos</c> ou <c>protecao_de_dados</c>.</param>
/// <param name="EtapasExigidasAgora">As etapas que a retomada exige, pelo catálogo atual.</param>
/// <param name="EtapasDecididas">As etapas já decididas nesta suspensão.</param>
public sealed record SuspensaoDoPerfilDto(
    DateTimeOffset Desde,
    string PapelDeQuemSuspendeu,
    IReadOnlyList<string> EtapasExigidasAgora,
    IReadOnlyList<string> EtapasDecididas);

/// <summary>
/// Um registro da trilha da contenção: suspensão, decisão de uma etapa da retomada, retomada, desativação ou
/// reativação, na ordem em que foram gravados.
/// </summary>
/// <param name="Tipo"><c>suspensao</c>, <c>decisao_da_retomada</c>, <c>retomada</c>, <c>desativacao</c> ou <c>reativacao</c>.</param>
/// <param name="ReferenciaId">A suspensão ou a desativação a que o registro pertence.</param>
/// <param name="Ordem">A posição na ordem total da trilha, estritamente crescente.</param>
/// <param name="Ator"><c>humano</c> ou <c>sistema</c>; a retomada é do sistema.</param>
/// <param name="Pessoa">Quem agiu, pelo emissor e <c>sub</c> do token e o nome quando o token trouxe; nulo só no registro do sistema.</param>
/// <param name="Etapa">O papel da suspensão ou a etapa decidida; nulo nos demais.</param>
/// <param name="Origem">O que fechou a suspensão, na retomada.</param>
/// <param name="PerfilProtegido">Se o perfil era protegido no instante.</param>
/// <param name="DecisorTemConcessao">Na decisão de etapa, se quem decidiu tinha concessão do perfil.</param>
public sealed record RegistroDaContencaoDto(
    Guid Id,
    string Tipo,
    Guid ReferenciaId,
    long Ordem,
    DateTimeOffset Instante,
    string Ator,
    IdentidadeDto? Pessoa,
    string? Etapa,
    string? Motivo,
    string? Origem,
    bool PerfilProtegido,
    bool? DecisorTemConcessao);
