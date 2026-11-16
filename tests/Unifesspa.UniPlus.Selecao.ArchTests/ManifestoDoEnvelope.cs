namespace Unifesspa.UniPlus.Selecao.ArchTests;

/// <summary>
/// As entradas do manifesto do envelope: a declaração, por tipo do grafo, das propriedades que entram
/// no documento com peso jurídico e das que ficam de fora, cada uma com o motivo.
/// </summary>
internal static class ManifestoDoEnvelope
{
    /// <summary>
    /// O manifesto. Cada propriedade de negócio de cada entidade do grafo aparece aqui —
    /// como <b>congelada</b> (entra no envelope) ou <b>excluída</b> (com o motivo). Compõe os grupos;
    /// um tipo declarado em dois grupos recusa a composição.
    /// </summary>
    internal static readonly Dictionary<Type, (string[] Congeladas, (string Propriedade, string Motivo)[] Excluidas)> Entradas =
        new[] { ManifestoDeEtapasVagasEClassificacao.Entradas, ManifestoDeEditalSnapshotsECronograma.Entradas, ManifestoDeExigenciasFatosEFormulario.Entradas }
            .SelectMany(static grupo => grupo)
            .ToDictionary(static entrada => entrada.Key, static entrada => entrada.Value);
}
