namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Ajusta um termo do modelo de partida, que o teste monta já válido, na obrigatoriedade: o termo devolvido é novo e o de
/// partida não muda.
/// </summary>
internal static class TermoDoModeloDeTeste
{
    public static TermoDoModelo ComObrigatoriedade(this TermoDoModelo termo, Obrigatoriedade obrigatoriedade) =>
        termo with { Regras = new ExibicaoEObrigatoriedade(termo.Regras.Exibicao, obrigatoriedade) };
}
