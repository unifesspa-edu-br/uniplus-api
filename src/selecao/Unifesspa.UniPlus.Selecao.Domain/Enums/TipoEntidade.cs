namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Catálogo fechado de 2 tipos de entidade repetível (Story #922) — cada instância que o
/// candidato declarar desse tipo multiplica a avaliação da subárvore marcada
/// <see cref="Entities.NoExigencia.RepetePorEntidade"/>. Ampliar o catálogo (tipo novo, atributo
/// novo, modelo genérico) é decisão firme fora de escopo — exige nova change.
/// </summary>
public enum TipoEntidade
{
    Nenhuma = 0,

    /// <summary>Membro do núcleo familiar — atributos de escopo-entidade fechados: maiorIdade, semRenda, sobGuarda.</summary>
    MembroNucleoFamiliar = 1,

    /// <summary>Pessoa jurídica vinculada — repetição pura, sem atributos (ex.: extratos + IRPJ por PJ).</summary>
    PessoaJuridicaVinculada = 2,
}
