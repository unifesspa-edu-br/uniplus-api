namespace Unifesspa.UniPlus.Discentes.Infrastructure.Sincronizacao;

using System.Collections.Generic;

/// <summary>
/// O que a reconciliação diária busca na origem e em que ritmo grava.
/// </summary>
public sealed class SincronizacaoOptions
{
    public const string SectionName = "Discentes:Sincronizacao";

    /// <summary>
    /// Nível de ensino que interessa à réplica. Hoje só graduação.
    /// </summary>
    public string NivelDeEnsino { get; init; } = "G";

    /// <summary>
    /// Quantos anos de ingresso para trás a varredura alcança.
    /// </summary>
    /// <remarks>
    /// É a aplicação da minimização de dados: guardar vínculos antigos e já encerrados não
    /// serve a nenhum uso do sistema. Vínculo ainda em andamento entra pela outra
    /// varredura, sem limite de idade — quem ingressou há mais tempo e continua matriculado
    /// segue sendo aluno.
    /// </remarks>
    public int AnosDeIngressoConsiderados { get; init; } = 10;

    /// <summary>
    /// Situações que caracterizam vínculo em andamento, no vocabulário da origem.
    /// </summary>
    /// <remarks>
    /// Vão numa única consulta: a origem aceita vários valores de uma vez para este filtro.
    /// </remarks>
    public IReadOnlyList<int> SituacoesEmAndamento { get; init; } = [1, 8, 9];

    /// <summary>
    /// Quantos vínculos gravar por transação.
    /// </summary>
    /// <remarks>
    /// Transação curta é o que permite uma execução interrompida deixar a réplica coerente
    /// no que já passou, em vez de tudo ou nada. Lote grande demais segura bloqueios por
    /// mais tempo e aumenta o que se perde quando um lote falha.
    /// </remarks>
    public int TamanhoDoLote { get; init; } = 500;
}
