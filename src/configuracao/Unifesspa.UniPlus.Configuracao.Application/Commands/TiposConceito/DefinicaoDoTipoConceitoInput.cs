namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposConceito;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// O que a gravação aceita de um tipo de conceito além do código, que é imutável: a mesma <c>definicao</c> que a
/// leitura devolve, com a natureza só pelo código. Os textos são <c>string?</c>, não <c>string</c> (ADR-0125): o model
/// binding do <c>[ApiController]</c> interceptaria um campo ausente com um 400 genérico antes de o domínio rodar.
/// O <c>Numerico</c> só vale para a natureza <c>NUMERICO</c>, e os <c>Valores</c>, para a <c>POR_OPCOES</c>; os valores
/// categóricos enviados substituem em bloco os que o tipo tinha.
/// </summary>
/// <param name="Nome">Nome do tipo de conceito.</param>
/// <param name="Descricao">Descrição do tipo de conceito, opcional.</param>
/// <param name="Natureza">O <c>codigo</c> da natureza, como o vocabulário <c>naturezas-conceito</c> o publica.</param>
/// <param name="Numerico">A escala do tipo numérico.</param>
/// <param name="Valores">Os valores do tipo categórico, em ordem.</param>
public sealed record DefinicaoDoTipoConceitoInput(
    string? Nome,
    string? Descricao,
    [AceitaCodigosDe<FormaDoConceito>] string? Natureza,
    EscalaNumericaCommand? Numerico,
    IReadOnlyList<ValorCategoricoCommand>? Valores);
