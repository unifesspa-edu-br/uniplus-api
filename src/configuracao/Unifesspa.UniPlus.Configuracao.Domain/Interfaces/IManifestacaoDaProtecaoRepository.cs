namespace Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>
/// Registro só acrescentado das manifestações técnicas na proteção do catálogo de tipos de documento e de fatos do
/// candidato (ADR-0163): grava junto da mudança do circuito do item e lê os eventos de um ciclo ou o histórico inteiro
/// do item.
/// </summary>
public interface IManifestacaoDaProtecaoRepository
{
    /// <summary>Acrescenta o evento à transação em curso; é gravado junto da mudança do item.</summary>
    void Adicionar(ManifestacaoDaProtecaoDoCatalogo manifestacao);

    /// <summary>Os eventos do item no ciclo, sem rastreamento, do mais antigo ao mais recente.</summary>
    Task<IReadOnlyList<ManifestacaoDaProtecaoDoCatalogo>> ListarDoCicloAsync(
        ItemDaProtecao item, CicloDaAprovacao ciclo, CancellationToken cancellationToken);

    /// <summary>Todos os eventos do item, de todos os ciclos, sem rastreamento, por ciclo e instante.</summary>
    Task<IReadOnlyList<ManifestacaoDaProtecaoDoCatalogo>> ListarDoItemAsync(ItemDaProtecao item, CancellationToken cancellationToken);
}
