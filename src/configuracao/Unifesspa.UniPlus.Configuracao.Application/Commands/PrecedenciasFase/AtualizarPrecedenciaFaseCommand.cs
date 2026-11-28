namespace Unifesspa.UniPlus.Configuracao.Application.Commands.PrecedenciasFase;

using System.ComponentModel;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Atualiza o único atributo editável de uma aresta de precedência existente:
/// <c>PermiteSobreposicao</c>. Antecessora e sucessora são <b>imutáveis</b> — o
/// comando <b>não</b> aceita códigos. O ator (<c>updated_by</c>) é carimbado
/// server-side via <c>IUserContext</c>.
/// </summary>
/// <remarks>
/// <c>PermiteSobreposicao</c> é <c>bool?</c> sem valor padrão: a edição substitui o valor, e um
/// <c>false</c> implícito desligaria a sobreposição em silêncio. Ausente ou nulo chega ao domínio e é
/// recusado com 422.
/// </remarks>
public sealed record AtualizarPrecedenciaFaseCommand(
    Guid Id,
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422).")]
    bool? PermiteSobreposicao) : ICommand<Result>;
