namespace Unifesspa.UniPlus.Geo.Application.Abstractions;

using Unifesspa.UniPlus.Geo.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Porta da API para o ETL de atualização periódica do Geo (Story #674). O ETL é um
/// serviço de Infrastructure (ADR-0092), não um command Wolverine — não há regra de
/// negócio nem evento de domínio, é carga de reference data autoritativo.
/// Esta porta expõe à borda apenas o <strong>disparo</strong> e o
/// <strong>acompanhamento</strong>; a execução pesada roda em segundo plano.
/// </summary>
public interface IGeoImportacaoService
{
    /// <summary>
    /// Registra uma carga da <paramref name="versao"/> (AAAAMM) no estado "em andamento"
    /// e a enfileira para execução em segundo plano. Retorna o Id da execução para
    /// acompanhamento. Falha com erro de conflito quando já há uma carga em andamento
    /// (uma execução por vez), ou de validação quando a versão é inválida.
    /// </summary>
    /// <param name="versao">Release DNE no formato AAAAMM.</param>
    /// <param name="disparadoPor">Subject do administrador, ou <c>seed</c> no boot de dev.</param>
    Task<Result<Guid>> IniciarAsync(string versao, string disparadoPor, CancellationToken cancellationToken);

    /// <summary>
    /// Obtém o estado e o relatório de uma execução pelo Id, ou <see langword="null"/>
    /// quando não existe.
    /// </summary>
    Task<ImportacaoGeoDto?> ObterAsync(Guid id, CancellationToken cancellationToken);
}
