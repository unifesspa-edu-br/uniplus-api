namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// O vínculo de uma inscrição com o processo e com a versão fixada, lido sem decifrar CPF nem respostas: serve a quem só
/// precisa saber a que processo e versão a inscrição pertence, e se ela já foi enviada.
/// </summary>
public sealed record VinculoDaInscricao(
    Guid Id,
    Guid ProcessoSeletivoId,
    SituacaoInscricao Situacao,
    Guid VersaoFormularioId,
    Guid? OfertaCursoId,
    Guid? OfertaCursoSegundaOpcaoId);

/// <summary>
/// O mínimo de uma inscrição enviada para quem opera o processo: identidade, número, situação, nome e o CPF — que só se
/// decifra para ser mascarado —, sem as demais respostas.
/// </summary>
public sealed record ResumoDaInscricaoDoProcesso(
    Guid Id,
    string? NumeroInscricao,
    SituacaoInscricao Situacao,
    string? Nome,
    Cpf Cpf,
    DateTimeOffset? SubmetidaEm);
