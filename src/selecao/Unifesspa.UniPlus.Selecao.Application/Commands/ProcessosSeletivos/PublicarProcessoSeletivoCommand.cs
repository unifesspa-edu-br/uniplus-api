namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Publica o Edital de abertura do processo (RN08, Story #759, T4 #785):
/// valida a conformidade estrutural, congela a configuração na versão 1 da
/// <c>VersaoConfiguracao</c> (append-only) e transita o status para Publicado,
/// tudo na mesma transação (CA-01/CA-02). O ator (<c>IUserContext.UserId</c>)
/// nunca é input do command — vem do contexto autenticado.
/// <para>
/// <see cref="Ato"/> carrega o que o DOCUMENTO declara — órgão, série, ano, data
/// documental, assinante e tipo. Publicações registra o ato correspondente a partir
/// desses dados, por mensagem durável (ADR-0108).
/// </para>
/// </summary>
public sealed record PublicarProcessoSeletivoCommand(
    Guid ProcessoSeletivoId,
    string? Numero,
    DateOnly PeriodoInscricaoInicio,
    DateOnly PeriodoInscricaoFim,
    Guid DocumentoEditalId,
    DadosDoAto Ato) : ICommand<Result>;
