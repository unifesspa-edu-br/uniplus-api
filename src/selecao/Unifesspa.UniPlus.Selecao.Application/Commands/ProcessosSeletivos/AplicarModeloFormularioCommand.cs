namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.ComponentModel;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Aplica ao processo a cópia de um modelo no formulário do código informado (UNI-REQ-0144, ADR-0061): o
/// formulário, que o processo já tem, é substituído por inteiro. Só em rascunho; mudar o modelo depois não muda o processo.
/// </summary>
public sealed record AplicarModeloFormularioCommand(Guid ProcessoSeletivoId, Guid ModeloId, string? FormularioCodigo)
    : ICommand<Result<AplicacaoDeModeloDto>>;

/// <summary>O corpo da aplicação: o modelo escolhido e o código do formulário que recebe a cópia.</summary>
public sealed record AplicacaoDeModeloInput(
    Guid ModeloId,
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422).")]
    string? FormularioCodigo);

/// <summary>
/// O que a aplicação fez além de copiar: o código do formulário que recebeu a cópia, os fatos que vieram de
/// outro formulário para a inscrição, os que ficaram na inscrição e saíram da cópia, as partes descartadas por mudança no catálogo e as
/// derivações copiadas do catálogo ou mantidas como o processo já as tinha.
/// </summary>
public sealed record AplicacaoDeModeloDto(
    string FormularioCodigo,
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
    public const string DerivadoSemRegra = "AplicacaoDeModelo.DerivadoSemRegra";
    public const string SemDistribuicaoDeVagas = "AplicacaoDeModelo.SemDistribuicaoDeVagas";
    public const string SemCotaDaLeiOfertada = "AplicacaoDeModelo.SemCotaDaLeiOfertada";
    public const string FaseDoDocumentoInexistente = "AplicacaoDeModelo.FaseDoDocumentoInexistente";
    public const string TipoDeDocumentoInexistente = "AplicacaoDeModelo.TipoDeDocumentoInexistente";
}
