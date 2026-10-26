namespace Unifesspa.UniPlus.Selecao.Application.Commands.NotasDoEnem;

using DTOs.NotasDoEnem;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Gera o arquivo de envio ao INEP com os CPFs das inscrições enviadas que escolheram a edição, no
/// leiaute escolhido.
/// </summary>
public sealed record GerarEnvioAoInepCommand(
    Guid ProcessoSeletivoId,
    Guid EtapaProcessoId,
    string Edicao,
    string? LeiauteCodigo,
    string? LeiauteVersao) : ICommand<Result<EnvioAoInepGeradoDto>>;
