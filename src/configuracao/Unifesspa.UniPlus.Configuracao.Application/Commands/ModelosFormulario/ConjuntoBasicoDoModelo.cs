namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;

using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// O conteúdo do modelo de inscrição com o conjunto básico de dados do candidato na seção reservada,
/// mesclado como a gravação e a aplicação do modelo o mesclam. O item básico que o conteúdo já traz
/// sai com a ordem e o rótulo do conjunto básico.
/// </summary>
public static class ConjuntoBasicoDoModelo
{
    /// <param name="conteudo">O conteúdo de um modelo de inscrição.</param>
    /// <param name="formatos">O formato da resposta de cada fato de texto, em token canônico.</param>
    public static ConteudoDoModelo Mesclar(ConteudoDoModelo conteudo, IReadOnlyDictionary<string, string> formatos)
    {
        ArgumentNullException.ThrowIfNull(conteudo);
        ConteudoDoModeloInput entrada = conteudo.ParaEntrada();
        (ConteudoDoModeloInput? mesclado, _) = EscritaDoModelo.ComASecaoDosDadosBasicos(entrada, FinalidadeFormulario.Inscricao, entrada);
        return ConteudoLido.Ler(mesclado, formatos).ParaConteudo();
    }
}
