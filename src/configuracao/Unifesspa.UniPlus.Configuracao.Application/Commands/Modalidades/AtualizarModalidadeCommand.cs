namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Modalidades;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.Commands.Predicados;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Atualiza uma modalidade de concorrência existente. O <c>Codigo</c> e o <c>Id</c>
/// são <b>imutáveis</b> (diferente do TipoDocumento): o comando <b>não</b> aceita
/// código — o handler carrega a entidade e a atualiza sem alterá-lo. Os enums
/// chegam como tokens canônicos UPPER_SNAKE. O ator (<c>updated_by</c>) é carimbado
/// server-side via <c>IUserContext</c>.
/// </summary>
/// <remarks>
/// A operação é <b>substituição integral</b>: campo omitido não é "mantido como está",
/// é reenviado com o default (natureza <c>AMPLA</c>, composição <c>RESTANTE_DAS_VAGAS_OFERTADAS</c>,
/// demais nulos). Numa modalidade do catálogo legal fixo, onde só descrição e base legal
/// são editáveis, isso significa reenviar a estrutura tal como está — omitir a natureza de
/// uma cota federal para corrigir a descrição resulta em <c>EstruturaProtegidaNaoEditavel</c>
/// (422), não em edição parcial silenciosa.
/// </remarks>
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
    IReadOnlyList<IReadOnlyList<CondicaoDoPredicadoInput>>? RegraDeAplicabilidade = null,
    string? AcaoQuandoIndeferido = null,
    string? BaseLegal = null) : ICommand<Result>
{
    /// <summary>Os campos editáveis como o agregado os recebe, com a regra de aplicabilidade já montada.</summary>
    public DeclaracaoDaModalidade Declaracao(PredicadoDnf? regraDeAplicabilidade) => new(
        Descricao, NaturezaLegal, ComposicaoVagas, ComposicaoOrigem, RegraRemanejamento, RemanejamentoDestino,
        RemanejamentoPar, RemanejamentoFallback, regraDeAplicabilidade, AcaoQuandoIndeferido, BaseLegal);
}
