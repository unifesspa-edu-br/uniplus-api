namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using System.Text.Json;

/// <summary>
/// Story #922 — uma instância declarada pelo candidato de um <see cref="Enums.TipoEntidade"/>
/// repetível (ex.: "membro 2" do núcleo familiar, "PJ 1" vinculada). O runtime de declaração em
/// si — formulário, validação de schema por tipo — é fora de escopo desta Story (mesmo
/// raciocínio de <see cref="ApresentacaoDocumento"/>: este VO só carrega o que o resolvedor
/// precisa, já resolvido).
/// </summary>
/// <param name="EntidadeId">Identidade estável da instância — chave da correlação <c>(exigencia_id, tipoEntidade, entidade_id)</c> com <see cref="ApresentacaoDocumento.EntidadeId"/>.</param>
/// <param name="Atributos">
/// Os "fatos de escopo-entidade" desta instância (ex.: <c>MAIOR_IDADE</c>, <c>SEM_RENDA</c> para
/// <see cref="Enums.TipoEntidade.MembroNucleoFamiliar"/>) — mesclados sobre os fatos do candidato
/// ao resolver gatilhos DENTRO da subárvore repetida (sujeito trocado, mesmo motor da folha
/// irmã). Vazio para <see cref="Enums.TipoEntidade.PessoaJuridicaVinculada"/> (repetição pura,
/// sem atributos). O vocabulário fechado por tipo é validado fora do domínio (Application).
/// </param>
public sealed record InstanciaEntidade(string EntidadeId, IReadOnlyDictionary<string, FatoResolvido> Atributos);
