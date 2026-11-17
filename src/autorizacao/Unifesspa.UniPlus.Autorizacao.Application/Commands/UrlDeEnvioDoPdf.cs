namespace Unifesspa.UniPlus.Autorizacao.Application.Commands;

/// <summary>A URL de PUT em que o cliente sobe o PDF, e o instante até o qual ela vale.</summary>
public sealed record UrlDeEnvioDoPdf(Uri Endereco, DateTimeOffset ExpiraEm);
