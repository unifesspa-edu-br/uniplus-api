namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Um fato do catálogo como a consulta pública o devolve: o fato e os operadores que uma condição de regra
/// pode usar sobre ele.
/// </summary>
/// <param name="Fato">O fato, com a mesma forma que a Seleção lê pelo leitor do catálogo.</param>
/// <param name="OperadoresAdmitidos">
/// Os operadores que uma condição pode usar sobre o fato, na ordem em que a tela os oferece; o primeiro é o
/// padrão da condição nova. Cada rótulo está no sentido da cardinalidade do fato. Vazia quando o fato não é
/// citável em regra (texto, data, endereço) ou quando não é categórico e tem mais de um valor.
/// </param>
public sealed record FatoDoCatalogoDto(
    FatoCandidatoView Fato,
    IReadOnlyList<CodigoComRotulo<Operador>> OperadoresAdmitidos);
