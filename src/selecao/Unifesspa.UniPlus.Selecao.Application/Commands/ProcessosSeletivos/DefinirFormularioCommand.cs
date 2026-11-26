namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Define ou substitui o formulário de uma finalidade (UNI-REQ-0144): a fase do cronograma, o título,
/// as páginas e, opcionalmente, a consequência da falta de envio. Editável só em rascunho.
/// </summary>
public sealed record DefinirFormularioCommand(
    Guid ProcessoSeletivoId,
    FinalidadeFormulario Finalidade,
    Guid? FaseId,
    string? Titulo,
    IReadOnlyList<PaginaDoFormularioInput> Paginas,
    [property: AceitaCodigosDe<ConsequenciaDoDescumprimento>] string? ConsequenciaDaFaltaDeEnvio = null) : ICommand<Result>;

/// <summary>Remove o formulário de uma finalidade, com os itens e os termos dele. Só em rascunho.</summary>
public sealed record RemoverFormularioCommand(
    Guid ProcessoSeletivoId,
    FinalidadeFormulario Finalidade) : ICommand<Result>;
