namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;

using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
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
        ArgumentNullException.ThrowIfNull(formatos);
        ConteudoDoModeloInput entrada = conteudo.ParaEntrada();
        (ConteudoDoModeloInput? mesclado, _) = ComASecaoDosDadosBasicos(entrada, FinalidadeFormulario.Inscricao, entrada);
        return new LeituraDoConteudo(formatos).Ler(mesclado).ParaConteudo();
    }

    /// <summary>
    /// O conteúdo do modelo como o formulário do processo o terá depois de aplicado: no de inscrição, com o
    /// conjunto básico na seção reservada. A simulação do modelo tem de mostrar o que o candidato verá.
    /// </summary>
    internal static ConteudoDoModelo ComoAplicado(ModeloFormulario modelo, IReadOnlyList<FatoCandidato> fatos)
    {
        ArgumentNullException.ThrowIfNull(modelo);
        return modelo.Finalidade == FinalidadeFormulario.Inscricao
            ? Mesclar(modelo.Conteudo, CatalogoDoModelo.De(fatos).Formatos)
            : modelo.Conteudo;
    }

    internal static (ConteudoDoModeloInput? Entrada, List<FieldError> Erros) ComASecaoDosDadosBasicos(
        ConteudoDoModeloInput? entrada, FinalidadeFormulario? finalidade, ConteudoDoModeloInput? gravado)
    {
        if (entrada is null || finalidade != FinalidadeFormulario.Inscricao)
        {
            return (entrada, []);
        }

        (IReadOnlyList<FatoColetadoInput> itens, IReadOnlyList<GrupoColetadoInput> grupos, List<FieldError> erros) =
            ConjuntoBasicoDaInscricao.MesclarItens(entrada.Itens ?? [], entrada.Grupos ?? [], ConjuntoBasicoDaInscricao.Referencia(gravado?.Itens ?? []));
        (IReadOnlyList<EtapaFormularioInput> etapas, List<FieldError> daSecao) =
            ConjuntoBasicoDaInscricao.MesclarEtapas(entrada.Etapas ?? [], ConjuntoBasicoDaInscricao.SecaoDe(gravado?.Etapas ?? []));
        return (entrada with { Itens = itens, Grupos = grupos, Etapas = etapas }, [.. erros, .. daSecao]);
    }
}
