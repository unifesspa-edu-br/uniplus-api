namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using System.ComponentModel;

using Controllers;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Corpo de <see cref="ProcessoSeletivoOfertaEClassificacaoController.DefinirOpcoesDeCurso"/> — omite
/// <c>ProcessoSeletivoId</c> (vem da rota). <c>Numero</c> é anulável e sem <c>[JsonRequired]</c>: ausente ou nulo
/// chega ao domínio e é recusado (422), e não vira um zero que o agregado recusaria como fora de 1 ou 2.
/// </summary>
public sealed record DefinirOpcoesDeCursoRequest(
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422). Aceita 1 ou 2.")]
    int? Numero);
