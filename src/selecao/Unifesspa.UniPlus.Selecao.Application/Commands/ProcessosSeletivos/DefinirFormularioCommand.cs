namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Define ou substitui o formulário do código informado (UNI-REQ-0144): a fase do cronograma, o título, se é
/// obrigatório, as páginas (os dois ausentes são recusados) e, opcionalmente, a consequência da falta de envio. Editável só em rascunho.
/// </summary>
public sealed record DefinirFormularioCommand(
    Guid ProcessoSeletivoId,
    string FormularioCodigo,
    Guid? FaseId,
    string? Titulo,
    bool? Obrigatorio,
    IReadOnlyList<PaginaDoFormularioInput>? Paginas,
    [property: AceitaCodigosDe<ConsequenciaDoDescumprimento>] string? ConsequenciaDaFaltaDeEnvio = null) : ICommand<Result>;

/// <summary>Remove o formulário do código informado, com os itens e os termos dele. Só em rascunho.</summary>
public sealed record RemoverFormularioCommand(
    Guid ProcessoSeletivoId,
    string FormularioCodigo) : ICommand<Result>;
