namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

/// <summary>
/// A fase ou a etapa que outra parte do documento cita pela chave: a que a leitura adotou, com o id em memória, a que a leitura
/// recusou ou a que o documento não tem. Quem cita a parte recusada não acumula causa nova, porque a causa é a da parte.
/// </summary>
internal readonly record struct ParteCitadaNoDocumento(Guid? Id, bool RecusadaNaLeitura)
{
    /// <summary>Se a chave não é a de nenhuma parte do documento, adotada ou recusada.</summary>
    internal bool Inexistente => Id is null && !RecusadaNaLeitura;

    internal static ParteCitadaNoDocumento De(string chave, IReadOnlyDictionary<string, Guid> idPelaChave, IReadOnlySet<string> chavesRecusadas)
    {
        ArgumentNullException.ThrowIfNull(chave);
        ArgumentNullException.ThrowIfNull(idPelaChave);
        ArgumentNullException.ThrowIfNull(chavesRecusadas);

        return idPelaChave.TryGetValue(chave, out Guid id)
            ? new(id, false)
            : new(null, chavesRecusadas.Contains(chave));
    }

    internal static ParteCitadaNoDocumento De(string chave, EtapasLidas etapas)
    {
        ArgumentNullException.ThrowIfNull(etapas);

        return De(chave, etapas.IdPelaChave, etapas.ChavesRecusadas);
    }

    internal static ParteCitadaNoDocumento De(string chave, FasesLidas fases)
    {
        ArgumentNullException.ThrowIfNull(fases);

        return De(chave, fases.IdPelaChave, fases.ChavesRecusadas);
    }
}
