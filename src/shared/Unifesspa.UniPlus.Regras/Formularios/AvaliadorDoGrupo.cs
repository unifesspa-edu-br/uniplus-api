namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;

internal sealed class AvaliadorDoGrupo
{
    private readonly DefinicaoGrupo _grupo;
    private readonly string _paginaCodigo;
    private readonly bool _paginaConcluida;
    private readonly ContextoDaResposta _contexto;
    private readonly Ternario _visivel;
    private readonly Ternario _obrigatorio;

    internal AvaliadorDoGrupo(DefinicaoGrupo grupo, string paginaCodigo, bool paginaConcluida,
        ContextoDaResposta contexto, Ternario visibilidadeHerdada)
    {
        _grupo = grupo;
        _paginaCodigo = paginaCodigo;
        _paginaConcluida = paginaConcluida;
        _contexto = contexto;
        _visivel = AvaliadorFormulario.E(visibilidadeHerdada, grupo.Exibicao?.Avaliar(contexto.Fatos) ?? Ternario.Verdadeiro);
        _obrigatorio = AvaliadorFormulario.ObrigatorioSeVisivel(_visivel, grupo.Obrigatoriedade, contexto.Fatos);
    }

    internal AvaliacaoGrupo Avaliar(EntradaAvaliacaoFormulario entrada)
    {
        if (_visivel is Ternario.Falso or Ternario.Indeterminado)
        {
            EstadoFato estado = _visivel == Ternario.Falso ? EstadoFato.NaoAplicavel : EstadoFato.Indeterminado;
            return Resultado(estado, []);
        }
        return AvaliarRespostas(entrada);
    }

    private AvaliacaoGrupo AvaliarRespostas(EntradaAvaliacaoFormulario entrada)
    {
        EstadoFato semResposta = RespostaDeCampo.EstadoSemResposta(_obrigatorio, _paginaConcluida);
        if (entrada.RespostasDosGrupos is null || !entrada.RespostasDosGrupos.TryGetValue(_grupo.Codigo, out IReadOnlyList<OcorrenciaRespondida>? respondidas))
        {
            return Resultado(semResposta, []);
        }

        if (respondidas.GroupBy(static ocorrencia => ocorrencia.Id, StringComparer.Ordinal).FirstOrDefault(static conjunto => conjunto.Count() > 1) is { } repetida)
        {
            throw new ArgumentException($"A identidade '{repetida.Key}' aparece em mais de uma ocorrência do grupo '{_grupo.Codigo}'.", nameof(entrada));
        }
        return AvaliarOcorrencias(respondidas, semResposta);
    }

    private AvaliacaoGrupo AvaliarOcorrencias(IReadOnlyList<OcorrenciaRespondida> respondidas, EstadoFato semResposta)
    {
        AvaliacaoOcorrencia[] ocorrencias = [.. respondidas.Select(ocorrencia =>
            new AvaliadorDaOcorrencia(_paginaCodigo, _paginaConcluida, _contexto, ocorrencia).Avaliar(_grupo.Subitens))];
        bool contagemValida = _grupo.ContagemValida(ocorrencias.Length, _obrigatorio);
        bool candidatoValido = _grupo.OcorrenciaDoCandidatoValida(respondidas);
        if (!contagemValida || !candidatoValido)
        {
            return Resultado(semResposta, ocorrencias, contagemValida, candidatoValido);
        }

        return Resultado(EstadoDaLista(ocorrencias, _obrigatorio), ocorrencias);
    }

    private AvaliacaoGrupo Resultado(EstadoFato estado, IReadOnlyList<AvaliacaoOcorrencia> ocorrencias, bool contagemValida = true, bool candidatoValido = true) =>
        new(_grupo.Codigo, _paginaCodigo, _visivel, _obrigatorio, estado, contagemValida, candidatoValido, ocorrencias);

    private static EstadoFato EstadoDaLista(AvaliacaoOcorrencia[] ocorrencias, Ternario obrigatorio) =>
        ocorrencias.Length == 0 ? obrigatorio switch
        {
            Ternario.Falso => EstadoFato.NaoInformado,
            Ternario.Verdadeiro => EstadoFato.Resolvido,
            _ => EstadoFato.Indeterminado,
        } : ocorrencias.All(static ocorrencia => ocorrencia.Estado == EstadoFato.Resolvido)
            ? EstadoFato.Resolvido : EstadoFato.Indeterminado;
}
