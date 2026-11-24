namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Regras reconhecidas pelo catálogo.</summary>
public enum RegraDeCalculoDaNota
{
    [CodigoDeContrato("FORMULA_MEDIA_PONDERADA", "Calcula a média ponderada",
        Descricao = "Calcula a média das notas das etapas com os pesos definidos.")]
    MediaPonderada = 1,

    [CodigoDeContrato("CLASSIFICACAO_IMPORTADA", "Usa a classificação importada",
        Descricao = "Usa a classificação recebida do sistema federal, sem cálculo local.")]
    ClassificacaoImportada = 2,
}
