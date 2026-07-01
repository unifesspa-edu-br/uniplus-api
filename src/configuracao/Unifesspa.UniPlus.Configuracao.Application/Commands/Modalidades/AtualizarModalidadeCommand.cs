namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Modalidades;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Atualiza uma modalidade de concorrência existente. O <c>Codigo</c> e o <c>Id</c>
/// são <b>imutáveis</b> (diferente do TipoDocumento): o comando <b>não</b> aceita
/// código — o handler carrega a entidade e a atualiza sem alterá-lo. Os enums
/// chegam como tokens canônicos UPPER_SNAKE. O ator (<c>updated_by</c>) é carimbado
/// server-side via <c>IUserContext</c>.
/// </summary>
public sealed record AtualizarModalidadeCommand(
    Guid Id,
    string? Descricao = null,
    string? NaturezaLegal = null,
    string? ComposicaoVagas = null,
    string? ComposicaoOrigem = null,
    string? RegraRemanejamento = null,
    string? RemanejamentoDestino = null,
    string? RemanejamentoPar = null,
    string? RemanejamentoFallback = null,
    IReadOnlyList<string>? CriteriosCumulativos = null,
    string? AcaoQuandoIndeferido = null,
    string? BaseLegal = null) : ICommand<Result>;
