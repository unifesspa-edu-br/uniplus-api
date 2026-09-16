namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Joga fora o rascunho da publicação do operador corrente.
/// </summary>
/// <remarks>
/// Existe como comando próprio, e não como gravação de conteúdo vazio, porque as duas coisas
/// dizem coisas diferentes: "apaguei os campos e quero guardar assim" é um rascunho vazio
/// legítimo; "desisti deste rascunho" é a ausência dele. Sem o descarte explícito, o operador
/// não teria como retirar do servidor o nome que digitou.
/// </remarks>
public sealed record DescartarRascunhoDaPublicacaoCommand(Guid ProcessoSeletivoId) : ICommand<Result>;
