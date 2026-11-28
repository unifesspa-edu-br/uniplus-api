namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Modalidades;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.Commands.Predicados;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Atualiza uma modalidade de concorrência existente. O <c>Codigo</c> e o <c>Id</c>
/// são <b>imutáveis</b> (diferente do TipoDocumento): o comando <b>não</b> aceita
/// código — o handler carrega a entidade e a atualiza sem alterá-lo. Os enums
/// chegam como código de contrato. O ator (<c>updated_by</c>) é carimbado
/// server-side via <c>IUserContext</c>.
/// </summary>
/// <remarks>
/// A operação é <b>substituição integral</b>: campo omitido não é "mantido como está",
/// é reenviado com o default (natureza <c>AMPLA</c>, composição <c>RESTANTE_DAS_VAGAS_OFERTADAS</c>,
/// demais nulos; o nome, obrigatório, precisa ser reenviado mesmo quando não muda): omitir <c>Remanejamento</c> declara que a modalidade não remaneja. Numa
/// modalidade do catálogo legal fixo, onde só nome, descrição e base legal são editáveis, isso significa
/// reenviar a estrutura tal como está — omitir a natureza de uma cota federal para corrigir a descrição resulta em <c>EstruturaProtegidaNaoEditavel</c>
/// (422), não em edição parcial silenciosa.
/// </remarks>
public sealed record AtualizarModalidadeCommand(
    Guid Id,
    string? Nome,
    string? Descricao = null,
    [property: AceitaCodigosDe<NaturezaLegal>] string? NaturezaLegal = null,
    [property: AceitaCodigosDe<ComposicaoVagas>] string? ComposicaoVagas = null,
    string? ComposicaoOrigem = null,
    RemanejamentoDasVagasInformado? Remanejamento = null,
    IReadOnlyList<IReadOnlyList<CondicaoDoPredicadoInput>>? RegraDeAplicabilidade = null,
    [property: AceitaCodigosDe<AcaoQuandoIndeferido>] string? AcaoQuandoIndeferido = null,
    string? BaseLegal = null) : ICommand<Result>
{
    /// <summary>Os campos editáveis como o agregado os recebe, com a regra de aplicabilidade já montada.</summary>
    public DeclaracaoDaModalidade Declaracao(PredicadoDnf? regraDeAplicabilidade) => new(
        Nome, Descricao, NaturezaLegal, ComposicaoVagas, ComposicaoOrigem, Remanejamento?.Regra, Remanejamento?.Destino,
        Remanejamento?.Par, Remanejamento?.Fallback, regraDeAplicabilidade, AcaoQuandoIndeferido, BaseLegal);
}
