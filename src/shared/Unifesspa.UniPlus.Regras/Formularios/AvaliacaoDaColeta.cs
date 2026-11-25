namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;

internal sealed class AvaliacaoDaColeta(
    string etapaCodigo, DateOnly? diaDoEnvio, IReadOnlyDictionary<string, FatoResolvido> fatos)
{
    internal AvaliacaoGrupo AvaliarGrupo(DefinicaoEtapa etapa, DefinicaoGrupo grupo, EntradaAvaliacaoFormulario entrada)
    {
        Ternario visivel = AvaliadorFormulario.E(etapa.Exibicao?.Avaliar(fatos) ?? Ternario.Verdadeiro, grupo.Exibicao?.Avaliar(fatos) ?? Ternario.Verdadeiro);
        Ternario obrigatorio = AvaliadorFormulario.ObrigatorioSeVisivel(visivel, grupo.Obrigatoriedade, fatos);
        if (visivel is Ternario.Falso or Ternario.Indeterminado)
        {
            EstadoFato estado = visivel == Ternario.Falso ? EstadoFato.NaoAplicavel : EstadoFato.Indeterminado;
            return new(grupo.Codigo, etapaCodigo, visivel, obrigatorio, estado, true, true, []);
        }

        return new AvaliacaoDoGrupo(grupo, etapaCodigo, entrada.EtapasConcluidas.Contains(etapaCodigo), diaDoEnvio, fatos, visivel, obrigatorio).Avaliar(entrada);
    }

    internal static EstadoFato EstadoSemResposta(Ternario obrigatorio, bool etapaConcluida) =>
        obrigatorio == Ternario.Falso && etapaConcluida ? EstadoFato.NaoInformado : EstadoFato.Indeterminado;
}
