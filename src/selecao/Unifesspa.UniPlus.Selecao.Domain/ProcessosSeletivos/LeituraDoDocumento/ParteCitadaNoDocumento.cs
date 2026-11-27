namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

/// <summary>
/// A etapa que uma regra de eliminação ou um critério de desempate cita pela chave: a que a leitura adotou, com o id em memória, a que
/// a leitura recusou ou a que o documento não tem. Quem cita a etapa recusada não acumula causa nova, porque a causa é a da etapa.
/// </summary>
internal readonly record struct EtapaCitadaNoDocumento(Guid? Id, bool RecusadaNaLeitura)
{
    /// <summary>Se a chave não é a de nenhuma etapa do documento, adotada ou recusada.</summary>
    internal bool Inexistente => Id is null && !RecusadaNaLeitura;

    internal static EtapaCitadaNoDocumento De(string chave, EtapasLidas etapas)
    {
        ArgumentNullException.ThrowIfNull(chave);
        ArgumentNullException.ThrowIfNull(etapas);

        return etapas.IdPelaChave.TryGetValue(chave, out Guid id)
            ? new(id, false)
            : new(null, etapas.ChavesRecusadas.Contains(chave));
    }
}
