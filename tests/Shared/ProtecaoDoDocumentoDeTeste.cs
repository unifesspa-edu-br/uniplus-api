namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>A proteção que a exigência copia do tipo de documento, para os testes que não a discutem.</summary>
internal static class ProtecaoDoDocumentoDeTeste
{
    public static ProtecaoDoDocumento Pessoal { get; } = De(ClassificacaoDeProtecao.Pessoal);

    public static ProtecaoDoDocumento De(ClassificacaoDeProtecao classificacao) =>
        new(classificacao, "Comprovação documental do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL");
}
