namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;

internal sealed class AvaliacaoDaColeta(
    string paginaCodigo, DateOnly? diaDoEnvio, IReadOnlyDictionary<string, FatoResolvido> fatos)
{
    internal AvaliacaoGrupo AvaliarGrupo(DefinicaoPagina pagina, DefinicaoGrupo grupo, EntradaAvaliacaoFormulario entrada)
    {
        Ternario visivel = AvaliadorFormulario.E(pagina.Exibicao?.Avaliar(fatos) ?? Ternario.Verdadeiro, grupo.Exibicao?.Avaliar(fatos) ?? Ternario.Verdadeiro);
        Ternario obrigatorio = AvaliadorFormulario.ObrigatorioSeVisivel(visivel, grupo.Obrigatoriedade, fatos);
        if (visivel is Ternario.Falso or Ternario.Indeterminado)
        {
            EstadoFato estado = visivel == Ternario.Falso ? EstadoFato.NaoAplicavel : EstadoFato.Indeterminado;
            return new(grupo.Codigo, paginaCodigo, visivel, obrigatorio, estado, true, true, []);
        }

        return new AvaliacaoDoGrupo(grupo, paginaCodigo, entrada.PaginasConcluidas.Contains(paginaCodigo), diaDoEnvio, fatos, visivel, obrigatorio).Avaliar(entrada);
    }

    internal static EstadoFato EstadoSemResposta(Ternario obrigatorio, bool paginaConcluida) =>
        obrigatorio == Ternario.Falso && paginaConcluida ? EstadoFato.NaoInformado : EstadoFato.Indeterminado;
}
