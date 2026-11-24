namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Único lugar em que o código de um eixo do fato vira o enum, no cadastro e na alteração. Ausente,
/// vazio ou só espaços é ausente; o código preenchido que não é do vocabulário é desconhecido, e o
/// agregado o recusa no próprio campo. O corte dos espaços das pontas é feito aqui, antes da leitura.
/// </summary>
internal static class TokensDoFato
{
    internal static CodigoInformado<TipoDoDado> Dominio(string? token) => Ler<TipoDoDado>(token);

    internal static CodigoInformado<QuantidadeDeValores> Cardinalidade(string? token) => Ler<QuantidadeDeValores>(token);

    internal static CodigoInformado<FonteValoresFato> Fonte(string? token) => Ler<FonteValoresFato>(token);

    internal static CodigoInformado<FormatoTexto> Formato(string? token) => Ler<FormatoTexto>(token);

    internal static CodigoInformado<ComponenteDeExibicao> Componente(string? token) => Ler<ComponenteDeExibicao>(token);

    internal static CodigoInformado<EscopoFato> Escopo(string? token) => Ler<EscopoFato>(token);

    internal static CodigoInformado<ClassificacaoDeProtecao> Classificacao(string? token) => Ler<ClassificacaoDeProtecao>(token);

    internal static CodigoInformado<HipoteseLegalTratamento> HipoteseLegal(string? token) => Ler<HipoteseLegalTratamento>(token);

    private static CodigoInformado<T> Ler<T>(string? token)
        where T : struct, Enum =>
        CodigoInformado<T>.Ler(token?.Trim());
}
