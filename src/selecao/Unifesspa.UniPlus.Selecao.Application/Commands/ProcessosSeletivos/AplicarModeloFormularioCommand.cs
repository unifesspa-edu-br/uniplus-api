namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Aplica ao processo a cópia de um modelo de formulário (UNI-REQ-0144, ADR-0061): o formulário da
/// finalidade do modelo é criado ou substituído por inteiro. Só em rascunho; mudar o modelo depois
/// não muda o processo.
/// </summary>
public sealed record AplicarModeloFormularioCommand(Guid ProcessoSeletivoId, Guid ModeloId, PrecondicaoIfMatch Precondicao)
    : ICommand<Result<AplicacaoDeModeloDto>>;

/// <summary>O corpo da aplicação: o modelo escolhido.</summary>
public sealed record AplicacaoDeModeloInput(Guid ModeloId);

/// <summary>
/// O que a aplicação fez além de copiar: os fatos que vieram de outra finalidade para a inscrição,
/// os que ficaram na inscrição e saíram da cópia, as partes descartadas por mudança no catálogo e as
/// derivações copiadas do catálogo ou mantidas como o processo já as tinha.
/// </summary>
public sealed record AplicacaoDeModeloDto(
    string Finalidade,
    IReadOnlyList<string> FatosTrazidosParaAInscricao,
    IReadOnlyList<string> FatosMantidosNaInscricao,
    IReadOnlyList<ParteDescartadaDto> Descartados,
    IReadOnlyList<string> DerivacoesCopiadas,
    IReadOnlyList<string> DerivacoesMantidas);

/// <summary>
/// Uma parte do modelo que ficou fora da cópia: o item (<c>ITEM</c>, pelo fato), o grupo repetível
/// (<c>GRUPO</c>, pelo código, quando um campo dele saiu) ou o termo (<c>TERMO</c>, pelo código), com o motivo — <c>FATO_DESATIVADO</c>, <c>FATO_NAO_COLETAVEL</c> ou
/// <c>VERSAO_DE_TERMO_REMOVIDA</c>. A correção é editar o modelo.
/// </summary>
public sealed record ParteDescartadaDto(string Parte, string Codigo, string Motivo);

/// <summary>Códigos de recusa da aplicação de modelo de formulário.</summary>
public static class AplicacaoDeModeloErrorCodes
{
    public const string ModeloInexistente = "AplicacaoDeModelo.ModeloInexistente";
    public const string ModeloInativo = "AplicacaoDeModelo.ModeloInativo";
    public const string TipoDeProcessoDiferente = "AplicacaoDeModelo.TipoDeProcessoDiferente";
    public const string PressupostoAusente = "AplicacaoDeModelo.PressupostoAusente";
    public const string DerivadoBooleano = "AplicacaoDeModelo.DerivadoBooleano";
    public const string DerivadoSemRegra = "AplicacaoDeModelo.DerivadoSemRegra";
    public const string SemDistribuicaoDeVagas = "AplicacaoDeModelo.SemDistribuicaoDeVagas";
}
