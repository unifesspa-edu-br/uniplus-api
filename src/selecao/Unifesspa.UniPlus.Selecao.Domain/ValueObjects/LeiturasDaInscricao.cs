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
/// O mínimo de uma inscrição enviada para quem opera o processo: identidade, número, situação, versão do formulário, cursos escolhidos, nome e o
/// CPF — que só se decifra para ser mascarado —, sem as demais respostas. O nome e o CPF só são lidos quando a listagem pede;
/// sem o pedido ficam nulos.
/// </summary>
public sealed record ResumoDaInscricaoDoProcesso(
    Guid Id,
    string? NumeroInscricao,
    SituacaoInscricao Situacao,
    Guid VersaoFormularioId,
    Guid? OfertaCursoId,
    Guid? OfertaCursoSegundaOpcaoId,
    string? Nome,
    Cpf? Cpf,
    DateTimeOffset? SubmetidaEm);

/// <summary>
/// A resposta cifrada de um campo identificador, localizada sem decifrar: a identidade da linha.
/// </summary>
/// <param name="RespostaId">A linha da resposta cifrada.</param>
public sealed record RespostaIdentificadora(Guid RespostaId);
