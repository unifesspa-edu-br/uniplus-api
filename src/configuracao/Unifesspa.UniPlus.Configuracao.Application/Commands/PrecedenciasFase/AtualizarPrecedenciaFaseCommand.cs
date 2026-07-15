namespace Unifesspa.UniPlus.Configuracao.Application.Commands.PrecedenciasFase;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Atualiza o único atributo editável de uma aresta de precedência existente:
/// <c>PermiteSobreposicao</c>. Antecessora e sucessora são <b>imutáveis</b> — o
/// comando <b>não</b> aceita códigos. O ator (<c>updated_by</c>) é carimbado
/// server-side via <c>IUserContext</c>.
/// </summary>
public sealed record AtualizarPrecedenciaFaseCommand(
    Guid Id,
    bool PermiteSobreposicao) : ICommand<Result>;
