namespace Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// O ato normativo está registrado. Emitido pelo consumo da requisição de registro, na mesma
/// transação que grava o ato.
/// </summary>
/// <remarks>
/// <para>
/// É o desfecho que faltava para o domínio que publicou. Ele decide o identificador do ato dentro
/// da sua própria transação, mas a existência do ato no registro central só se confirma depois — e
/// é dessa confirmação que depende a publicidade do que foi publicado.
/// </para>
/// <para>
/// <b>Só o sucesso é respondido.</b> A recusa de mérito é terminal e já tem o seu lugar: a fila
/// morta, onde alguém reconcilia. Responder a recusa daria ao emissor um caminho para reagir
/// automaticamente a um estado que precisa de decisão humana.
/// </para>
/// <para>
/// A reentrega é esperada, e por isso o consumo do lado de quem publicou tem de ser idempotente: o
/// mesmo ato pode ser anunciado mais de uma vez sem que nada mude.
/// </para>
/// <para>
/// O ano e o tipo viajam com o anúncio porque compõem o endereço do documento que o emissor publica
/// no acervo (ADR-0132): são atributos do ato registrado, que não muda depois de registrado, e o
/// emissor não precisa consultar o registro central para conhecê-los.
/// </para>
/// </remarks>
/// <param name="AtoId">Identificador do ato, o mesmo que o domínio decidiu ao publicar.</param>
/// <param name="Ano">Ano do ato, o da numeração oficial.</param>
/// <param name="TipoCodigo">Código do tipo do ato no catálogo de Publicações.</param>
public sealed record AtoNormativoRegistrado(Guid AtoId, int Ano, string TipoCodigo);
