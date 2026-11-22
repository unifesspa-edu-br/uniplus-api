namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Um dia não útil do dataset, projetado para resposta HTTP (a
/// abrangência sai como código e rótulo). Dias municipais carregam a cópia do código
/// IBGE, nome e UF; <c>Uf</c> permanece reservada à abrangência estadual.
/// </summary>
public sealed record DiaNaoUtilDto(
    Guid Id,
    CodigoComRotulo<Abrangencia> Abrangencia,
    string? MunicipioIbge,
    string? MunicipioNome,
    string? MunicipioUf,
    string? Uf,
    DateOnly Data,
    string Descricao);
