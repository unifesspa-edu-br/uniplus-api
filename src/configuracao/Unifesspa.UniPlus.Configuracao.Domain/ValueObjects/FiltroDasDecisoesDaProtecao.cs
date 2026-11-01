namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>O recorte das decisões da proteção de dados que a consulta pede; cada critério ausente não restringe.</summary>
/// <param name="Atos">Quais decisões; vazio, as três.</param>
/// <param name="Cadastro">O cadastro do item.</param>
/// <param name="De">A data inicial (inclusive), no fuso institucional (America/Belem).</param>
/// <param name="Ate">A data final (inclusive), no fuso institucional (America/Belem).</param>
/// <param name="Busca">O trecho do nome ou do código do item; sem distinguir maiúsculas nem acentos.</param>
/// <param name="Autor">Quando informado, só as decisões de quem agiu com este emissor e sub.</param>
public sealed record FiltroDasDecisoesDaProtecao(
    IReadOnlyCollection<AtoDaProtecao> Atos,
    CadastroDoCatalogo? Cadastro,
    DateOnly? De,
    DateOnly? Ate,
    string? Busca,
    (string Emissor, string Sub)? Autor);
