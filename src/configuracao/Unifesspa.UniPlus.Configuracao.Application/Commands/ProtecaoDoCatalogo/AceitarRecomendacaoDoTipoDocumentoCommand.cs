namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O administrador aceita a classificação que a proteção de dados recomendou ao negar o tipo de documento: o
/// tipo fica aprovado com ela, sem nova solicitação, e a proteção de dados é avisada por e-mail.
/// </summary>
/// <param name="Id">O tipo de documento.</param>
/// <param name="Destinatarios">Os e-mails de quem tem a permissão de aprovar, lidos na borda.</param>
public sealed record AceitarRecomendacaoDoTipoDocumentoCommand(Guid Id, IReadOnlyList<string> Destinatarios) : ICommand<Result>;
