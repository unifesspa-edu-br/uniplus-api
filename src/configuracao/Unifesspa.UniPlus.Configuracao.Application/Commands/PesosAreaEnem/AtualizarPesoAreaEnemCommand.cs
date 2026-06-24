namespace Unifesspa.UniPlus.Configuracao.Application.Commands.PesosAreaEnem;

using System.Text.Json.Serialization;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Atualiza os cinco pesos, o corte de redação e a base legal de uma linha de
/// pesos. A chave de negócio (<c>Resolucao</c> + <c>GrupoCurso</c>) e o <c>Id</c>
/// são imutáveis (CA-04b) — não constam no payload.
/// </summary>
/// <remarks>
/// PUT é substituição completa: os cinco pesos e o <c>CorteRedacao</c> são
/// obrigatórios, não opcionais. Como são <c>decimal</c> não-anuláveis, são marcados
/// <c>[JsonRequired]</c> — sem isso o System.Text.Json constrói o record com
/// <c>0m</c> quando o campo é omitido, e o validator aceita 0, sobrescrevendo
/// silenciosamente o valor configurado (ex.: um corte 450 vira 0 ao editar só a
/// base legal). Com <c>[JsonRequired]</c> a omissão vira 400 na desserialização. O
/// cliente sempre reenvia o estado completo da linha.
/// </remarks>
public sealed record AtualizarPesoAreaEnemCommand(
    Guid Id,
    [property: JsonRequired] decimal PesoRedacao,
    [property: JsonRequired] decimal PesoCienciasNatureza,
    [property: JsonRequired] decimal PesoCienciasHumanas,
    [property: JsonRequired] decimal PesoLinguagens,
    [property: JsonRequired] decimal PesoMatematica,
    [property: JsonRequired] decimal CorteRedacao,
    string BaseLegal) : ICommand<Result>;
