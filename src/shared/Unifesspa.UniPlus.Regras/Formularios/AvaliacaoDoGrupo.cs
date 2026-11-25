namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;

internal sealed class AvaliacaoDoGrupo(
    DefinicaoGrupo grupo, string paginaCodigo, bool paginaConcluida, DateOnly? diaDoEnvio,
    IReadOnlyDictionary<string, FatoResolvido> fatos, Ternario visivel, Ternario obrigatorio)
{
    internal AvaliacaoGrupo Avaliar(EntradaAvaliacaoFormulario entrada)
    {
        EstadoFato semResposta = AvaliacaoDaColeta.EstadoSemResposta(obrigatorio, paginaConcluida);
        if (entrada.RespostasDosGrupos is null || !entrada.RespostasDosGrupos.TryGetValue(grupo.Codigo, out IReadOnlyList<OcorrenciaRespondida>? respondidas))
        {
            return Resultado(semResposta, []);
        }

        if (respondidas.GroupBy(static ocorrencia => ocorrencia.Id, StringComparer.Ordinal).FirstOrDefault(static conjunto => conjunto.Count() > 1) is { } repetida)
        {
            throw new ArgumentException($"A identidade '{repetida.Key}' aparece em mais de uma ocorrência do grupo '{grupo.Codigo}'.", nameof(entrada));
        }

        return AvaliarOcorrencias(respondidas, semResposta);
    }

    private AvaliacaoGrupo AvaliarOcorrencias(IReadOnlyList<OcorrenciaRespondida> respondidas, EstadoFato semResposta)
    {
        AvaliacaoOcorrencia[] ocorrencias = [.. respondidas.Select(ocorrencia =>
            new AvaliacaoDaOcorrencia(paginaCodigo, paginaConcluida, diaDoEnvio, fatos, ocorrencia).Avaliar(grupo.Subitens))];
        bool contagemValida = grupo.ContagemValida(ocorrencias.Length, obrigatorio);
        bool candidatoValido = grupo.OcorrenciaDoCandidatoValida(respondidas);
        if (!contagemValida || !candidatoValido)
        {
            return Resultado(semResposta, ocorrencias, contagemValida, candidatoValido);
        }

        return Resultado(DefinicaoGrupo.EstadoDaLista(ocorrencias, obrigatorio), ocorrencias);
    }

    private AvaliacaoGrupo Resultado(EstadoFato estado, IReadOnlyList<AvaliacaoOcorrencia> ocorrencias, bool contagemValida = true, bool candidatoValido = true) =>
        new(grupo.Codigo, paginaCodigo, visivel, obrigatorio, estado, contagemValida, candidatoValido, ocorrencias);
}
