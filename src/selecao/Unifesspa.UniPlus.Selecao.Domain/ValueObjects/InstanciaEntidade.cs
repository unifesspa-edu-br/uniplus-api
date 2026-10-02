namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using System.Text.Json;

using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Uma ocorrência que o candidato respondeu num grupo repetível do formulário (ex.: "membro 2" da
/// composição familiar), instância da exigência repetida por esse grupo (ADR-0138). Montá-la a
/// partir das respostas é da execução da inscrição; este VO só carrega o que o resolvedor precisa,
/// já resolvido (mesmo raciocínio de <see cref="ApresentacaoDocumento"/>).
/// </summary>
/// <param name="EntidadeId">Identidade estável da ocorrência — chave da correlação <c>(exigencia_id, grupo, entidade_id)</c> com <see cref="ApresentacaoDocumento.EntidadeId"/>.</param>
/// <param name="Atributos">
/// Os campos resolvidos da ocorrência (ex.: <c>MAIOR_IDADE</c>, <c>SOB_GUARDA</c>), mesclados
/// sobre os fatos do candidato ao resolver gatilhos dentro da subárvore repetida (sujeito trocado,
/// mesmo motor da folha irmã).
/// </param>
public sealed record InstanciaEntidade(string EntidadeId, IReadOnlyDictionary<string, FatoResolvido> Atributos);
