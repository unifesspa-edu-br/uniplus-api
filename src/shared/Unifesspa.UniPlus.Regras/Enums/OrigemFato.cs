namespace Unifesspa.UniPlus.Regras.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Origem de um dado do catálogo: como o valor dele chega ao sistema (ADR-0111, refinada pela ADR-0116). É ortogonal ao
/// tipo do dado e à quantidade de valores. Serve à regra, aplicada pelo avaliador, de que um dado calculado não pode
/// ser insumo de si mesmo nem de uma condição avaliada antes da etapa que o produz. É o mesmo enum na Configuração,
/// que cadastra o dado, e na Seleção, que decide a coleta pelo catálogo (ADR-0176).
/// </summary>
[DescricaoDoContrato("Como o valor de um dado do catálogo chega ao sistema: calculado, declarado pelo candidato ou recebido de integração.")]
public enum OrigemFato
{
    /// <summary>Calculado pelo sistema ou por uma regra, sem ser coletado: é saída (ex.: faixa etária a partir da data de nascimento).</summary>
    [CodigoDeContrato("CALCULADO", "Calculado", Descricao = "O valor é calculado pelo sistema ou por uma regra; não é coletado, é resultado.")]
    Calculado = 1,

    /// <summary>Resposta ou seleção direta do candidato no cadastro de inscrição (ex.: COR_RACA, SEXO).</summary>
    [CodigoDeContrato("DECLARADO", "Declarado", Descricao = "O valor é a resposta ou a escolha direta do candidato no formulário.")]
    Declarado = 2,

    /// <summary>Recebido de uma integração externa (ex.: SIGAA) — reservado, sem dado semeado ainda.</summary>
    [CodigoDeContrato("INTEGRACAO", "Integração", Descricao = "O valor é recebido de uma integração com outro sistema.")]
    Integracao = 3,
}
