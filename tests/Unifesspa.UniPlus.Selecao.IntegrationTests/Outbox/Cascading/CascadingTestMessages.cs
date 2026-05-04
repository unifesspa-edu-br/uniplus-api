namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Diagnostics.CodeAnalysis;

using Domain.Enums;
using Domain.ValueObjects;

[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "Mensagens descobertas pela convenção do Wolverine precisam ser públicas.")]
public sealed record PublicarEditalCascadingCommand(NumeroEdital Numero, string Titulo, TipoProcesso Tipo);

[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "Mensagens descobertas pela convenção do Wolverine precisam ser públicas.")]
public sealed record FalharAposSaveChangesCascadingCommand(NumeroEdital Numero, string Titulo, TipoProcesso Tipo);
