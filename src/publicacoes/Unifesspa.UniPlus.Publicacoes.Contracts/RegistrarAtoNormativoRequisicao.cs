namespace Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// requisição durável de registro de ato normativo: a mensagem que um
/// domínio (Seleção ao publicar um Edital, Ingresso ao convocar uma chamada)
/// enfileira, na MESMA transação em que publica, para que Publicações registre o
/// ato correspondente.
/// </summary>
/// <remarks>
/// <para>
/// O <see cref="AtoId"/> é decidido pelo DOMÍNIO que publica, não por Publicações.
/// É o que torna a reentrega idempotente: a fila é at-least-once, e um segundo
/// processamento tenta gravar o MESMO id — que a chave primária recusa. Sem isso,
/// uma reentrega criaria um segundo ato, e o segundo ocuparia a vaga de linhagem do
/// objeto (ADR-0107) contra a linhagem do primeiro.
/// </para>
/// <para>
/// É também o que permite ao domínio referenciar o ato ANTES de ele existir
/// fisicamente: <c>VersaoConfiguracao.AtoCriadorId</c> é referência por valor, sem
/// chave estrangeira (ADR-0061) — o modelo já previa que o ato viveria noutro módulo.
/// </para>
/// </remarks>
public sealed record RegistrarAtoNormativoRequisicao(
    Guid AtoId,
    string Orgao,
    string Serie,
    int Ano,
    string? Numero,
    string TipoCodigo,
    DateOnly DataPublicacao,
    string DocumentoHash,
    string Assinante,
    Guid? VersaoInvocadaId,
    string? VersaoInvocadaHash,
    Guid? AtoRetificadoId,
    string? MotivoRetificacao,
    IReadOnlyList<VinculoEntidadeRequisicao> Vinculos,
    AtributosDoTipoAto AtributosDoTipo);

/// <summary>
/// O que o catálogo dizia sobre o tipo do ato NO MOMENTO em que o domínio publicou —
/// copiado por valor, e não relido depois (ADR-0061).
/// </summary>
/// <remarks>
/// O catálogo é editável: um administrador pode mudar <c>congela_configuracao</c> de um
/// tipo entre o 204 da publicação e o consumo da fila. Se o consumidor relesse o catálogo,
/// um edital publicado como congelante poderia ser registrado com um tipo que agora não
/// congela — recriando exatamente a divergência que a conferência prévia existe para
/// impedir, e desta vez sem ninguém para recusá-la.
/// <para>
/// O ato foi publicado sob as regras que valiam então, e é sob elas que se registra.
/// Mudança posterior no cadastro vale para o que vier depois, não reescreve o passado.
/// </para>
/// </remarks>
public sealed record AtributosDoTipoAto(
    bool CongelaConfiguracao,
    bool UnicoPorObjeto,
    bool EfeitoIrreversivel);

/// <summary>
/// Entidade de outro domínio a que o ato se liga (ADR-0105). O par é opaco para
/// Publicações: sem lista de valores permitidos e sem chave estrangeira.
/// </summary>
public sealed record VinculoEntidadeRequisicao(string EntidadeTipo, Guid EntidadeId);
