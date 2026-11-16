namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>A forma em que o conteúdo do modelo é conferido e gravado.</summary>
internal static class FormaGravadaDoModelo
{
    /// <summary>
    /// A forma em que o conteúdo é conferido e gravado: textos aparados, códigos em NFC, ausente como
    /// vazio — a forma do item recusa o vazio. Mantém as posições, inclusive das restrições, para que
    /// cada recusa aponte o índice que veio.
    /// </summary>
    internal static ConteudoDoModelo Normalizar(ConteudoDoModelo conteudo) => new(
        FormaDoItem.TextoOpcional(conteudo.Titulo),
        [.. (conteudo.Etapas ?? []).Select(static e => e with
        {
            Codigo = Normalizar(e.Codigo),
            Titulo = (e.Titulo ?? string.Empty).Trim(),
            Descricao = FormaDoItem.TextoOpcional(e.Descricao),
            Aviso = FormaDoItem.TextoOpcional(e.Aviso),
        })],
        [.. (conteudo.Itens ?? []).Select(NormalizarCampo)],
        [.. (conteudo.Termos ?? []).Select(static t => t with { Codigo = Normalizar(t.Codigo) })],
        [.. (conteudo.Pressupostos ?? []).Select(static p => Normalizar(p))],
        [.. (conteudo.Grupos ?? []).Select(static g => g with
        {
            Codigo = Normalizar(g.Codigo),
            EtapaCodigo = g.EtapaCodigo is null ? null : Normalizar(g.EtapaCodigo),
            Rotulo = (g.Rotulo ?? string.Empty).Trim(),
            Subitens = [.. (g.Subitens ?? []).Select(NormalizarCampo)],
        })],
        [.. (conteudo.DocumentosExigidos ?? []).Select(static d => d with
        {
            FaseCodigo = Normalizar(d.FaseCodigo),
            TipoDocumentoCodigo = Normalizar(d.TipoDocumentoCodigo),
            PorOpcao = d.PorOpcao is { } porOpcao
                ? new ExigenciaPorOpcao(Normalizar(porOpcao.FatoCodigo), [.. porOpcao.Exceto.Select(static v => Normalizar(v))])
                : null,
        })]);

    internal static ItemDoModelo NormalizarCampo(ItemDoModelo campo) => campo with
    {
        FatoCodigo = Normalizar(campo.FatoCodigo),
        EtapaCodigo = campo.EtapaCodigo is null ? null : Normalizar(campo.EtapaCodigo),
        Rotulo = (campo.Rotulo ?? string.Empty).Trim(),
        Formato = FormaDoItem.TextoOpcional(campo.Formato),
        Ajuda = FormaDoItem.TextoOpcional(campo.Ajuda),
        Restricoes = campo.Restricoes ?? [],
    };

    /// <summary>O código aparado e em NFC; o que não se normaliza fica como veio, para a recusa de texto não gravável.</summary>
    internal static string Normalizar(string? codigo) => TextoNormalizavel.AparadoEmNfc(codigo ?? string.Empty);
}
