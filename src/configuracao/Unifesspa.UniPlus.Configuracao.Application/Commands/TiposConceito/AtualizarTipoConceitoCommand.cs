namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposConceito;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <remarks>
/// O código não integra o comando porque é imutável. A <c>Definicao</c> é a mesma que a resposta devolve, com a
/// <c>Natureza</c> igual a <c>definicao.natureza.codigo</c>, e os valores categóricos enviados substituem em bloco os que o
/// tipo tinha.
/// </remarks>
public sealed record AtualizarTipoConceitoCommand(
    Guid Id,
    DefinicaoDoTipoConceitoInput? Definicao) : ICommand<Result>;
