namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Kernel.Results;

internal sealed record RegraQueCita(string Citante, IReadOnlyCollection<string> Citados, string? GrupoProprio)
{
    internal DomainError? CitacaoForaDoGrupo(IReadOnlyDictionary<string, string> grupoDoMembro)
    {
        string? membro = Citados.FirstOrDefault(c => grupoDoMembro.TryGetValue(c, out string? grupo) && grupo != GrupoProprio);
        return membro is null ? null : new DomainError(
            GrafoFormularioErrorCodes.CitaFatoDeMembroForaDoGrupo,
            $"Uma regra {Citante} cita '{membro}', campo do grupo '{grupoDoMembro[membro]}' — fora do grupo ele existe uma vez por "
            + "ocorrência e não tem valor único.");
    }
    internal DomainError? CitacaoInvalidaDoItem(long posicao, DependenciasDoFormulario dependencias) =>
        Citados.Select(citado => RecusaDoItem(citado, dependencias.Conferir(citado, posicao)))
            .FirstOrDefault(static recusa => recusa is not null);

    private DomainError? RecusaDoItem(string citado, RecusaDeCitacao? recusa) => recusa switch
    {
        RecusaDeCitacao.FatoNaoConhecido => new DomainError(GrafoFormularioErrorCodes.CitaFatoNaoConhecido,
            $"Uma regra {Citante} cita '{citado}', que o formulário não coleta nem deriva, nem é conhecido antes dele."),
        RecusaDeCitacao.FatoPosterior => new DomainError(GrafoFormularioErrorCodes.CitaFatoPosterior,
            $"Uma regra {Citante} cita '{citado}', que só fica conhecido nesse campo ou depois dele — "
            + "o campo dependeria de uma resposta ainda não dada."),
        _ => null,
    };

    internal DomainError? CitacaoInvalidaNaPosicao(long posicao, DependenciasDoFormulario dependencias) =>
        Citados.Select(citado => RecusaNaPosicao(citado, dependencias.Conferir(citado, posicao)))
            .FirstOrDefault(static recusa => recusa is not null);

    private DomainError? RecusaNaPosicao(string citado, RecusaDeCitacao? recusa) => recusa switch
    {
        RecusaDeCitacao.FatoNaoConhecido => new DomainError(GrafoFormularioErrorCodes.CitaFatoNaoConhecido,
            $"Uma regra {Citante} cita '{citado}', que o formulário não coleta nem deriva, nem é conhecido antes do grupo."),
        RecusaDeCitacao.FatoPosterior => new DomainError(GrafoFormularioErrorCodes.CitaFatoPosterior,
            $"Uma regra {Citante} cita '{citado}', que só fica conhecido na posição do grupo ou depois dela."),
        _ => null,
    };
}
