namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Um fato como o processo o congelou para a consulta administrativa: como mostrá-lo (rótulo, descrição, tipo e
/// componente) e como protegê-lo (classificação e base legal). Nada daqui vem do catálogo vivo.
/// </summary>
/// <param name="Codigo">O código do fato.</param>
/// <param name="Rotulo">O rótulo congelado.</param>
/// <param name="Descricao">O que o dado significa, congelada na publicação.</param>
/// <param name="Forma">Como ler o valor.</param>
/// <param name="ExibicaoDeclarada">A exibição declarada no catálogo.</param>
/// <param name="Classificacao">A classificação congelada; sensível quando não se resolve.</param>
/// <param name="BaseLegal">A hipótese legal congelada, ou nula quando não há.</param>
/// <param name="Secao">A seção do formulário em que o fato aparece, quando há.</param>
/// <param name="Ordem">A posição na ordem de coleta.</param>
/// <param name="Opcoes">Por código, o rótulo de cada opção de um fato de seleção.</param>
/// <param name="EhCpf">Se o texto do fato tem o formato de CPF, que define a máscara do identificador.</param>
internal sealed record FatoDoProcesso(
    string Codigo,
    string Rotulo,
    string Descricao,
    FormaDoValorDoItem Forma,
    ComponenteDeExibicao? ExibicaoDeclarada,
    ClassificacaoDeProtecao Classificacao,
    string? BaseLegal,
    string? Secao,
    int Ordem,
    IReadOnlyDictionary<string, string> Opcoes,
    bool EhCpf)
{
    public bool EhIdentificador => Classificacao == ClassificacaoDeProtecao.Identificador;

    public ExibicaoDoValorDoItem Exibicao => EhCpf ? ExibicaoDoValorDoItem.Cpf
        : EhIdentificador ? ExibicaoDoValorDoItem.Identificador
        : ExibicaoDeclarada switch
        {
            null => ExibicaoDoValorDoItem.Padrao,
            ComponenteDeExibicao.Moeda => ExibicaoDoValorDoItem.Moeda,
            ComponenteDeExibicao.Situacao => ExibicaoDoValorDoItem.Situacao,
            _ => throw new InvalidOperationException("Exibição declarada desconhecida."),
        };
}
