namespace Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosDaInscricao;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>Republica um lote de pedidos sem resultado além do prazo; devolve quantos.</summary>
public sealed record ReconciliarVerificacaoDosArquivosCommand : ICommand<Result<int>>;
