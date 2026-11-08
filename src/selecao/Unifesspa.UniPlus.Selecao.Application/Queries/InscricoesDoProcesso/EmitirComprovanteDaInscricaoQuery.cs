namespace Unifesspa.UniPlus.Selecao.Application.Queries.InscricoesDoProcesso;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;

/// <summary>
/// Pede, pela unidade que opera o processo, o comprovante número <paramref name="Numero"/> de uma inscrição enviada ou
/// cancelada do processo.
/// </summary>
public sealed record EmitirComprovanteDaInscricaoQuery(Guid ProcessoSeletivoId, Guid InscricaoId, int Numero)
    : IQuery<Result<ComprovanteDaInscricaoDto>>;
