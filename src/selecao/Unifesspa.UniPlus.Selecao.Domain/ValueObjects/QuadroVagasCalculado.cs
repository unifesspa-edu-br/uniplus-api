namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Resultado do cálculo do quadro de vagas pelo ramo federal
/// (<see cref="Services.CalculadoraQuadroVagasLei12711"/>) — a quantidade por
/// modalidade (as 8 sub-reservas federais, a ampla concorrência, e as
/// modalidades de retirada/suplemento informadas) mais os derivados que o
/// envelope congela junto do quadro (ADR-0115).
/// </summary>
/// <remarks>
/// Não é construído via <c>Criar</c>/<see cref="Kernel.Results.Result{T}"/>
/// porque não é input externo — nasce sempre já válido, montado pela
/// calculadora depois que ela mesma recusou as entradas inválidas (retirada
/// negativa, colisão de chave, ampla concorrência negativa).
/// </remarks>
public sealed record QuadroVagasCalculado(
    IReadOnlyDictionary<string, int> Quadro,
    int VrNominal,
    int VrFinal,
    int Estouro,
    bool CapadoEmVo,
    int Ac,
    int RetiradasTotal,
    int SuplementaresTotal,
    int TotalPublicado);
