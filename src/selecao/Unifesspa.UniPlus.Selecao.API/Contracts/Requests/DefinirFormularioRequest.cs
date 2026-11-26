namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using System.Text.Json.Serialization;

using Application.Commands.ProcessosSeletivos;

using Controllers;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Corpo de <see cref="FormulariosController.DefinirFormulario"/> — o processo e o código do formulário
/// vêm da rota. Dizer se o formulário é obrigatório é obrigatório no corpo. A lista de páginas é obrigatória: o formulário termina sempre na revisão e aceite. A consequência da
/// falta de envio é opcional, e a ausência dela retira a que o formulário tinha.
/// </summary>
public sealed record DefinirFormularioRequest(
    Guid? FaseId,
    string? Titulo,
    [property: JsonRequired] bool Obrigatorio,
    [property: JsonRequired] IReadOnlyList<PaginaDoFormularioInput> Paginas,
    [property: AceitaCodigosDe<ConsequenciaDoDescumprimento>] string? ConsequenciaDaFaltaDeEnvio = null);

/// <summary>
/// Corpo de <see cref="FormulariosController.DefinirItens"/>. A lista de itens é obrigatória: só a
/// lista vazia explícita remove os itens, nunca um corpo sem ela. Os grupos repetíveis
/// (UNI-REQ-0146) são opcionais: sem eles, os grupos do formulário ficam como estão, e só a lista
/// vazia explícita os remove.
/// </summary>
public sealed record DefinirItensDoFormularioRequest(
    [property: JsonRequired] IReadOnlyList<FatoColetadoInput> Itens,
    IReadOnlyList<GrupoColetadoInput>? Grupos = null);

/// <summary>
/// Corpo de <see cref="FormulariosController.DefinirTermos"/>. A lista é obrigatória: só a lista
/// vazia explícita remove os termos, nunca um corpo sem ela.
/// </summary>
public sealed record DefinirTermosDoFormularioRequest([property: JsonRequired] IReadOnlyList<TermoExigidoInput> Termos);
