namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Um fato como o processo o congelou para a consulta administrativa: como mostrá-lo (rótulo, descrição, tipo e
/// componente) e como protegê-lo (classificação e base legal). Nada daqui vem do catálogo vivo.
/// </summary>
/// <param name="Codigo">O código do fato.</param>
/// <param name="Rotulo">O rótulo congelado.</param>
/// <param name="Descricao">O que o dado significa, congelada na publicação.</param>
/// <param name="Tipo">Como ler o valor (<see cref="TiposDeItem"/>).</param>
/// <param name="Componente">Como exibir o valor (<see cref="ComponentesDeItem"/>).</param>
/// <param name="Classificacao">A classificação congelada; <see cref="ClassificacaoDeProtecao.Nenhuma"/> quando não se resolve.</param>
/// <param name="BaseLegal">A hipótese legal congelada, ou nula quando não há.</param>
/// <param name="Secao">A seção do formulário em que o fato aparece, quando há.</param>
/// <param name="Ordem">A posição na ordem de coleta.</param>
/// <param name="Opcoes">Por código, o rótulo de cada opção de um fato de seleção.</param>
/// <param name="EhCpf">Se o texto do fato tem o formato de CPF, que define a máscara do identificador.</param>
internal sealed record FatoDoProcesso(
    string Codigo,
    string Rotulo,
    string Descricao,
    string Tipo,
    string Componente,
    ClassificacaoDeProtecao Classificacao,
    string? BaseLegal,
    string? Secao,
    int Ordem,
    IReadOnlyDictionary<string, string> Opcoes,
    bool EhCpf);
