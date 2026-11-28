namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Publica o Edital de abertura do processo, congelando os parâmetros:
/// valida a conformidade estrutural, congela a configuração na versão 1 da
/// <c>VersaoConfiguracao</c> (append-only) e transita a situação para Publicado,
/// tudo na mesma transação. O ator (<c>IUserContext.UserId</c>)
/// nunca é input do command — vem do contexto autenticado.
/// <para>
/// <see cref="Ato"/> carrega o que o DOCUMENTO declara — órgão, série, ano, data
/// documental, assinante e tipo. Publicações registra o ato correspondente a partir
/// desses dados, na mesma transação (ADR-0170).
/// </para>
/// </summary>
/// <param name="PeriodoInscricaoInicio">
/// Início do período, informado apenas quando o certame não coleta inscrição pelo sistema;
/// caso contrário, vem da janela da fase. Anulável para separar "omitido" de
/// instante zerado.
/// </param>
/// <param name="PeriodoInscricaoFim">Fim do período, com a mesma regra do início.</param>
/// <param name="Anexos">
/// Os anexos do edital, na ordem em que ele os apresenta; nulo ou vazio quando não há.
/// </param>
public sealed record PublicarProcessoSeletivoCommand(
    Guid ProcessoSeletivoId,
    string? Numero,
    DateTimeOffset? PeriodoInscricaoInicio,
    DateTimeOffset? PeriodoInscricaoFim,
    Guid? DocumentoEditalId,
    DadosDoAto? Ato,
    IReadOnlyList<AnexoSolicitadoDoEdital>? Anexos = null) : ICommand<Result>;
