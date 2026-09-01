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
/// <param name="PeriodoInscricaoInicio">
/// Início do período, informado APENAS quando o certame não coleta inscrição pelo sistema
/// (issue #1350) — coletando, ele vem da janela da fase. Anulável para separar "omitido" de
/// instante zerado.
/// </param>
/// <param name="PeriodoInscricaoFim">Fim do período, com a mesma regra do início.</param>
public sealed record PublicarProcessoSeletivoCommand(
    Guid ProcessoSeletivoId,
    string? Numero,
    DateTimeOffset? PeriodoInscricaoInicio,
    DateTimeOffset? PeriodoInscricaoFim,
    Guid DocumentoEditalId,
    DadosDoAto Ato) : ICommand<Result>;
