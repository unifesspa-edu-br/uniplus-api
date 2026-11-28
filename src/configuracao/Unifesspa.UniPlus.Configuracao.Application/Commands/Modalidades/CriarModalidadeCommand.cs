namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Modalidades;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.Commands.Predicados;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Cria uma modalidade de concorrência (UNI-REQ-0011): código (chave natural
/// imutável), nome obrigatório, descrição opcional, e os campos de cota, em que os enums chegam como código de contrato
/// (<c>NaturezaLegal</c>, <c>ComposicaoVagas</c>, a regra do bloco <c>Remanejamento</c> e
/// <c>AcaoQuandoIndeferido</c>). Quando ausentes, <c>NaturezaLegal</c> assume AMPLA e <c>ComposicaoVagas</c> assume
/// RESTANTE_DAS_VAGAS_OFERTADAS; sem o bloco <c>Remanejamento</c>, a modalidade não remaneja.
/// A <c>RegraDeAplicabilidade</c> é o OU de cláusulas, cada uma o E de condições, que diz quem
/// concorre à modalidade (ADR-0144); a lista vazia é a regra incondicional, e a ausente é recusada.
/// O ator de auditoria (<c>created_by</c>) é carimbado server-side via
/// <c>IUserContext</c>, não no payload.
/// </summary>
/// <remarks>
/// <c>Codigo</c> e <c>Nome</c> são <c>string?</c>, não <c>string</c> (ADR-0125): sem validator
/// FluentValidation garantindo não-nulo a montante, o model binding automático do
/// <c>[ApiController]</c> interceptaria um campo ausente/nulo com um 400 genérico
/// do ASP.NET, antes de o domínio rodar. Sem valor default (diferente dos demais
/// campos), para que o schema OpenAPI continue listando-os como obrigatórios —
/// mesmo padrão de Curso, TipoDeficiencia e Campus.
/// </remarks>
public sealed record CriarModalidadeCommand(
    string? Codigo,
    string? Nome,
    string? Descricao = null,
    [property: AceitaCodigosDe<NaturezaLegal>] string? NaturezaLegal = null,
    [property: AceitaCodigosDe<ComposicaoVagas>] string? ComposicaoVagas = null,
    string? ComposicaoOrigem = null,
    RemanejamentoDasVagasInformado? Remanejamento = null,
    IReadOnlyList<IReadOnlyList<CondicaoDoPredicadoInput>>? RegraDeAplicabilidade = null,
    [property: AceitaCodigosDe<AcaoQuandoIndeferido>] string? AcaoQuandoIndeferido = null,
    string? BaseLegal = null) : ICommand<Result<Guid>>
{
    /// <summary>Os campos editáveis como o agregado os recebe, com a regra de aplicabilidade já montada.</summary>
    public DeclaracaoDaModalidade Declaracao(PredicadoDnf? regraDeAplicabilidade) => new(
        Nome, Descricao, NaturezaLegal, ComposicaoVagas, ComposicaoOrigem, Remanejamento?.Regra, Remanejamento?.Destino,
        Remanejamento?.Par, Remanejamento?.Fallback, regraDeAplicabilidade, AcaoQuandoIndeferido, BaseLegal);
}
