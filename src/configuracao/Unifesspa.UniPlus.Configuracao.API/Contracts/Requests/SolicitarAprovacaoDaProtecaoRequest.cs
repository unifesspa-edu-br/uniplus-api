namespace Unifesspa.UniPlus.Configuracao.API.Contracts.Requests;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>Corpo da solicitação: os itens que o administrador envia à proteção de dados; item repetido conta uma vez.</summary>
public sealed record SolicitarAprovacaoDaProtecaoRequest(IReadOnlyList<ItemParaSolicitarRequest?>? Itens)
{
    public IReadOnlyList<ItemParaSolicitarDto?> ParaItens() =>
        [.. (Itens ?? []).Select(static i => i is null ? null : new ItemParaSolicitarDto(i.Cadastro, i.Id))];
}
