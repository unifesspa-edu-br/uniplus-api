namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// DTO read-only de <c>TipoDeficiencia</c> para consumo cross-módulo via
/// <see cref="ITipoDeficienciaReader"/> (ADR-0056, ADR-0116). Expõe o tipo vivo
/// (id + nome + descrição + classificação de permanência) que outros bounded
/// contexts leem antes de congelar por valor a identidade (snapshot-copy,
/// ADR-0061).
/// </summary>
/// <param name="Id">Identificador único (Guid v7 — ADR-0032).</param>
/// <param name="Nome">Rótulo legível do tipo de deficiência (chave natural).</param>
/// <param name="Descricao">
/// Descrição do tipo — obrigatória (ADR-0116): serve também como a descrição por
/// valor do fato <c>TIPO_DEFICIENCIA</c> que orienta a escolha do candidato.
/// </param>
/// <param name="Permanente">
/// <see langword="null"/> = ainda não classificado pelo CEPS; <see langword="false"/>
/// = classificado como não-permanente; <see langword="true"/> = permanente.
/// </param>
public sealed record TipoDeficienciaView(
    Guid Id,
    string Nome,
    string Descricao,
    bool? Permanente);
