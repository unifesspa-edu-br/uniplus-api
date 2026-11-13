namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A manifestação técnica de quem tem a permissão de se manifestar sobre a proteção de um item com aprovação solicitada
/// (UNI-REQ-0373). A nova manifestação da mesma pessoa no mesmo ciclo substitui a vigente sem apagá-la.
/// </summary>
/// <param name="Cadastro"><c>TIPO_DOCUMENTO</c> ou <c>FATO</c>.</param>
/// <param name="ItemId">Identificador do tipo de documento ou do fato.</param>
/// <param name="Ciclo">O ciclo de aprovação lido no item; se o item foi reiniciado depois, a manifestação responde conflito.</param>
/// <param name="Parecer"><c>ADEQUADA</c>, <c>RECOMENDA_AJUSTE</c> ou <c>INADEQUADA</c>.</param>
/// <param name="Texto">Até 2.000 caracteres; obrigatório em <c>RECOMENDA_AJUSTE</c> e <c>INADEQUADA</c>.</param>
/// <remarks>
/// Os campos são anuláveis para que o ausente seja recusado no próprio campo, junto das demais violações (ADR-0125).
/// </remarks>
public sealed record RegistrarManifestacaoDaProtecaoCommand(
    string? Cadastro,
    Guid ItemId,
    int? Ciclo,
    string? Parecer,
    string? Texto) : ICommand<Result>;
