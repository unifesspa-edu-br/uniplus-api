namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Âncora de <see cref="ValueObjects.IdadeMaximaEmissao"/> (Story #554, PR #900, issue #893)
/// — de onde se conta a idade máxima de emissão do DOCUMENTO. Diferente de
/// <see cref="ReferenciaTipo"/> (PR #896, PR #898 — âncora a idade do CANDIDATO e explicitamente
/// exclui submissão), <see cref="DataSubmissao"/> é válida aqui: o documento pode precisar
/// ser recente em relação ao ato de enviar, não a uma data fixa do certame.
/// </summary>
public enum ReferenciaTipoIdadeEmissao
{
    Nenhuma = 0,

    /// <summary>Fim da fase que coleta inscrição (<c>FaseCronograma.ColetaInscricao</c>).</summary>
    FimInscricao = 1,

    /// <summary>Início de uma fase específica do cronograma — exige <c>ReferenciaFaseId</c>.</summary>
    InicioFase = 2,

    /// <summary>Fim de uma fase específica do cronograma — exige <c>ReferenciaFaseId</c>.</summary>
    FimFase = 3,

    /// <summary>Data fixa declarada pelo administrador — exige <c>Data</c>.</summary>
    DataEspecifica = 4,

    /// <summary>Instante em que o candidato submete o documento — resolvida no runtime de coleta, fora de escopo desta Story.</summary>
    DataSubmissao = 5,
}
