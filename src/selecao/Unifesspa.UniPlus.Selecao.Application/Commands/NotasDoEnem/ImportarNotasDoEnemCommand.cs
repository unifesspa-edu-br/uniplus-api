namespace Unifesspa.UniPlus.Selecao.Application.Commands.NotasDoEnem;

using DTOs.NotasDoEnem;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Importa o arquivo de retorno do INEP de uma edição do ENEM. O conteúdo carrega os bytes porque o
/// hash do arquivo inteiro identifica a importação; o comando é despachado em memória, sem passar
/// pelo outbox.
/// </summary>
public sealed record ImportarNotasDoEnemCommand(
    Guid ProcessoSeletivoId,
    Guid EtapaProcessoId,
    string Edicao,
    string? LeiauteCodigo,
    string? LeiauteVersao,
    byte[] Conteudo) : ICommand<Result<ImportacaoDeNotasDto>>;
