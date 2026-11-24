namespace Unifesspa.UniPlus.Kernel.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Escala única de proteção de dados do Uni+ (ADR-0081, ADR-0136), crescente: usada pelo fato do
/// candidato, pelo tipo de documento, pela exigência documental e pela sensibilidade da permissão
/// (ADR-0078). <see cref="Sensivel"/> diz a natureza do dado (LGPD, art. 5º, II); os demais níveis
/// dizem a exposição permitida a um dado que é sempre pessoal quando ligado a um candidato
/// identificado.
/// </summary>
[DescricaoDoContrato("Classificação da proteção exigida para o tratamento de dados.")]
public enum ClassificacaoDeProtecao
{
    /// <summary>Exposição sem restrição de divulgação.</summary>
    [CodigoDeContrato("PUBLICO", "Público", Descricao = "Dado que pode ser divulgado sem restrição.")]
    Publico = 1,

    /// <summary>Exposição restrita ao âmbito institucional.</summary>
    [CodigoDeContrato("INTERNO", "Interno", Descricao = "Dado de acesso restrito à instituição.")]
    Interno = 2,

    /// <summary>Dado pessoal (LGPD, art. 5º, I).</summary>
    [CodigoDeContrato("PESSOAL", "Pessoal", Descricao = "Dado pessoal relacionado a uma pessoa identificada ou identificável.")]
    Pessoal = 3,

    /// <summary>
    /// Dado pessoal que identifica diretamente o titular: número de documento (CPF, RG,
    /// passaporte). Isola um indivíduo entre os demais sem revelar nenhum atributo seu —
    /// por isso cifrado em repouso, diferente de <see cref="Pessoal"/> e <see cref="Sensivel"/>
    /// (ADR-0121, ADR-0136).
    /// </summary>
    [CodigoDeContrato("IDENTIFICADOR", "Identificador", Descricao = "Dado pessoal que identifica diretamente uma pessoa.")]
    Identificador = 4,

    /// <summary>Dado pessoal sensível (LGPD, art. 5º, II), como origem racial ou saúde.</summary>
    [CodigoDeContrato("SENSIVEL", "Sensível", Descricao = "Dado pessoal sensível, como informação sobre saúde.")]
    Sensivel = 5,
}
