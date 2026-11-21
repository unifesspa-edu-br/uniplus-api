namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Ajusta um grupo do modelo de partida, que o teste monta já válido, em só uma parte das ocorrências ou
/// das regras do grupo: o grupo devolvido é novo e o de partida não muda.
/// </summary>
internal static class GrupoDoModeloDeTeste
{
    public static GrupoDoModelo ComOcorrencias(this GrupoDoModelo grupo, int minimo, int? maximo, bool incluiCandidato = false) =>
        grupo with { Ocorrencias = new OcorrenciasDoGrupo(minimo, maximo, incluiCandidato) };

    public static GrupoDoModelo ComObrigatoriedade(this GrupoDoModelo grupo, Obrigatoriedade obrigatoriedade) =>
        grupo with { Regras = new ExibicaoEObrigatoriedade(grupo.Regras.Exibicao, obrigatoriedade) };
}
