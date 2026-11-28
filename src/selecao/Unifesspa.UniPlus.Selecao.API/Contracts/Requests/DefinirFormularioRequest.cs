namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using System.ComponentModel;

using Application.Commands.ProcessosSeletivos;

using Controllers;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Corpo de <see cref="FormulariosController.DefinirFormulario"/> — o processo e o código do formulário
/// vêm da rota. Dizer se o formulário é obrigatório e enviar a lista de páginas são obrigatórios no corpo: o que
/// falta é recusado com o código de campo obrigatório (422), junto das demais recusas. O formulário termina sempre na revisão e aceite. A consequência da
/// falta de envio é opcional, e a ausência dela retira a que o formulário tinha.
/// </summary>
public sealed record DefinirFormularioRequest(
    Guid? FaseId,
    string? Titulo,
    [property: ObrigatorioNoContrato, Description("Obrigatório; ausente ou nulo é recusado (422).")] bool? Obrigatorio,
    [property: ObrigatorioNoContrato, Description("Obrigatório; ausente ou nulo é recusado (422). Envie [] quando não houver nenhuma.")]
    IReadOnlyList<PaginaDoFormularioInput>? Paginas,
    [property: AceitaCodigosDe<ConsequenciaDoDescumprimento>] string? ConsequenciaDaFaltaDeEnvio = null);

/// <summary>
/// Corpo de <see cref="FormulariosController.DefinirItens"/>. A lista de itens é obrigatória: só a
/// lista vazia explícita remove os itens, e o corpo sem ela é recusado com o código de campo obrigatório (422). Os grupos repetíveis
/// (UNI-REQ-0146) são opcionais: sem eles, os grupos do formulário ficam como estão, e só a lista
/// vazia explícita os remove.
/// </summary>
public sealed record DefinirItensDoFormularioRequest(
    [property: ObrigatorioNoContrato, Description("Obrigatório; ausente ou nulo é recusado (422). Envie [] quando não houver nenhum.")]
    IReadOnlyList<FatoColetadoInput>? Itens,
    IReadOnlyList<GrupoColetadoInput>? Grupos = null);

/// <summary>
/// Corpo de <see cref="FormulariosController.DefinirTermos"/>. A lista é obrigatória: só a lista
/// vazia explícita remove os termos, e o corpo sem ela é recusado (422).
/// </summary>
public sealed record DefinirTermosDoFormularioRequest(
    [property: ObrigatorioNoContrato, Description("Obrigatório; ausente ou nulo é recusado (422). Envie [] quando não houver nenhum.")]
    IReadOnlyList<TermoExigidoInput>? Termos);
