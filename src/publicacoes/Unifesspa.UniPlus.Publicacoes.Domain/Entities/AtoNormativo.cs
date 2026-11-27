namespace Unifesspa.UniPlus.Publicacoes.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

/// <summary>
/// Ato publicado — o registro central e append-only de que algo foi publicado
/// (ADR-0105). Guarda a essência documental do ato (órgão, série, ano, número,
/// tipo, data do ato, hash do documento, assinante e cadeia de retificação) e, por
/// valor, os atributos de consequência do catálogo e a versão do objeto a que o ato
/// se refere. É a <b>prova</b> do publicado: o passado documental não se muta.
/// </summary>
/// <remarks>
/// <para>
/// Implementa <see cref="IForensicEntity"/> (ADR-0063): append-only, não herda
/// <see cref="Kernel.Domain.Entities.EntityBase"/> e não carrega soft-delete.
/// Só recebe <c>INSERT</c> — <c>UPDATE</c>/<c>DELETE</c> são bloqueados no banco
/// por trigger (a factory não expõe mutadores; o trigger fecha a brecha de um
/// <c>UPDATE</c>/<c>DELETE</c> cru fora do agregado).
/// </para>
/// <para>
/// <b>Número declarado, não gerado.</b> O sistema não opina sobre a política de
/// numeração de cada órgão: <see cref="Numero"/> é opcional e não tem unicidade
/// imposta — dois atos com o mesmo <c>(orgao, serie, ano, numero)</c> são aceitos.
/// A colisão de número gera um <i>aviso</i> consultável (computado na leitura),
/// jamais recusa.
/// </para>
/// <para>
/// <b>Data do ato ≠ instante da divulgação.</b> <see cref="DataDoAto"/> é o
/// que o documento declara e nunca ordena vigência; a ordem pertence à versão de
/// configuração, no relógio do sistema. <see cref="PublicadoEm"/> é o instante
/// da divulgação, lido uma vez pelo sistema e recebido aqui — Publicações o guarda e
/// nunca o define —, distinto da data que o documento declara.
/// </para>
/// <para>
/// <b>Cópia por valor.</b> <see cref="CongelaConfiguracao"/> e
/// <see cref="EfeitoIrreversivel"/> são copiados do <c>TipoAtoPublicado</c>
/// vigente no registro — nunca consultados de
/// volta para decidir fluxo (ADR-0103). Editar o catálogo depois não reescreve
/// ato nenhum.
/// </para>
/// <para>
/// <b>Retificação é relação, não tipo</b> (ADR-0103). Um ato pode retificar outro
/// pelo par <see cref="AtoRetificadoId"/> + <see cref="MotivoRetificacao"/>,
/// simétrico (um existe se e somente se o outro existe). A cadeia é linear — um
/// ato é retificado no máximo uma vez — e empilha na cabeça: uma segunda
/// retificação retifica a primeira, não a raiz. As invariantes que dependem do
/// ato retificado (classe de congelamento coincidente, linearidade) vivem no
/// handler, que as consulta no repositório; aqui fica só a simetria de shape.
/// </para>
/// <para>
/// <b>Vínculo com entidades de outros domínios</b> (ADR-0105): o ato carrega os
/// pares <c>(entidade_tipo, entidade_id)</c> que o ligam ao objeto de que trata —
/// opacos para o módulo. Um ato sem vínculo algum é válido: há atos que não
/// pertencem a certame nenhum. Ver <see cref="VinculoAtoEntidade"/>.
/// </para>
/// <para>
/// <b>Anexos com o hash de cada um.</b> O hash do documento principal não prova os
/// arquivos anexos; por isso o ato guarda também a lista deles — nome, tipo de mídia e
/// hash —, na ordem em que foram enviados. Ver <see cref="AnexoDoAto"/>.
/// </para>
/// </remarks>
public sealed class AtoNormativo : IForensicEntity
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();

    /// <summary>Órgão publicador que emitiu o ato (ex.: CEPS, CGA).</summary>
    public string Orgao { get; private init; } = null!;

    /// <summary>Série de numeração do ato (ex.: EDITAL, AVISO) — atravessa certames.</summary>
    public string Serie { get; private init; } = null!;

    /// <summary>Ano da numeração declarada pelo órgão.</summary>
    public int Ano { get; private init; }

    /// <summary>Número que o órgão deu ao ato. Opcional — há atos sem número (ex.: comunicado).</summary>
    public string? Numero { get; private init; }

    /// <summary>A numeração declarada pelo órgão, composta de <see cref="Orgao"/>, <see cref="Serie"/>, <see cref="Ano"/> e <see cref="Numero"/>. Não é persistida.</summary>
    public NumeracaoDoAto Numeracao => new(Orgao, Serie, Ano, Numero);

    /// <summary>Código do tipo de ato no catálogo (<c>TipoAtoPublicado</c>) vigente no registro.</summary>
    public string TipoCodigo { get; private init; } = null!;

    /// <summary>
    /// Nome do tipo de ato no instante do registro — copiado por valor do catálogo, como
    /// os atributos de consequência, para que renomear o tipo não reescreva o título de um
    /// ato já publicado. Nulo nos atos registrados antes de o nome passar a ser copiado.
    /// </summary>
    public string? TipoNome { get; private init; }

    /// <summary>Se o ato produz nova versão congelada da configuração — copiado por valor do catálogo (congelamento dos parâmetros na publicação, UNI-REQ-0019).</summary>
    public bool CongelaConfiguracao { get; private init; }

    /// <summary>Se a publicação do ato não pode ser desfeita — copiado por valor do catálogo.</summary>
    public bool EfeitoIrreversivel { get; private init; }

    /// <summary>
    /// Se o objeto do ato admite um único ato vivo deste tipo — copiado por valor
    /// do catálogo, no instante do registro (ADR-0075), como os demais atributos de
    /// consequência. A unicidade da raiz de cadeia por objeto (#801) ancora-se neste
    /// snapshot: consultar o catálogo vigente no futuro tornaria a regra histórica
    /// instável, porque o cadastro é editável.
    /// </summary>
    public bool UnicoPorObjeto { get; private init; }

    /// <summary>Data que o documento declara. Documental — nunca ordena vigência.</summary>
    public DateOnly DataDoAto { get; private init; }

    /// <summary>Hash SHA-256 do PDF publicado. É o que prova o publicado.</summary>
    public string DocumentoHash { get; private init; } = null!;

    /// <summary>Nome de quem assina o ato.</summary>
    public string Assinante { get; private init; } = null!;

    /// <summary>Instante da divulgação, lido uma vez pelo sistema (relógio do sistema, não o documento) e recebido por quem registra.</summary>
    public DateTimeOffset PublicadoEm { get; private init; }

    /// <summary>
    /// Versão do objeto a que o ato se refere, por valor <c>{id, hash}</c>
    /// (ADR-0075). Nula quando o ato não se refere a versão de objeto algum (ato autônomo).
    /// </summary>
    public ReferenciaVersaoDoObjeto? VersaoDoObjeto { get; private init; }

    /// <summary>
    /// Ato que este retifica, quando é uma retificação (ADR-0103). Nulo num ato que
    /// não emenda outro. Referência intra-módulo por FK — permitida, pois a ADR-0061
    /// só proíbe FK atravessando a fronteira de outro módulo. Simétrico com
    /// <see cref="MotivoRetificacao"/>.
    /// </summary>
    public Guid? AtoRetificadoId { get; private init; }

    /// <summary>
    /// Motivo declarado da retificação. Simétrico com <see cref="AtoRetificadoId"/> —
    /// um existe se e somente se o outro existe.
    /// </summary>
    public string? MotivoRetificacao { get; private init; }

    /// <summary>
    /// Entidades de outros domínios a que este ato se liga (ADR-0105). Vazio num ato
    /// que não trata de objeto algum — o edital que seleciona elaboradores de questões
    /// não pertence a certame nenhum, e é válido assim.
    /// </summary>
    public IReadOnlyCollection<VinculoAtoEntidade> Vinculos => _vinculos.AsReadOnly();

    private readonly List<VinculoAtoEntidade> _vinculos = [];

    /// <summary>
    /// Arquivos anexos ao documento principal, com o hash de cada um. Vazio num ato sem
    /// anexos. Ordenados por <see cref="AnexoDoAto.Ordem"/> na leitura.
    /// </summary>
    public IReadOnlyCollection<AnexoDoAto> Anexos => _anexos.AsReadOnly();

    private readonly List<AnexoDoAto> _anexos = [];

    // EF Core materialization
    private AtoNormativo()
    {
    }

    private AtoNormativo(EssenciaDocumentalDoAto essencia)
    {
        Orgao = essencia.Numeracao.Orgao;
        Serie = essencia.Numeracao.Serie;
        Ano = essencia.Numeracao.Ano;
        Numero = essencia.Numeracao.Numero;
        TipoCodigo = essencia.Tipo.Codigo;
        TipoNome = essencia.Tipo.Nome;
        CongelaConfiguracao = essencia.Tipo.CongelaConfiguracao;
        EfeitoIrreversivel = essencia.Tipo.EfeitoIrreversivel;
        UnicoPorObjeto = essencia.Tipo.UnicoPorObjeto;
        DataDoAto = essencia.DataDoAto;
        DocumentoHash = essencia.DocumentoHash;
        Assinante = essencia.Assinante;
        AtoRetificadoId = essencia.Retificacao.AtoRetificadoId;
        MotivoRetificacao = essencia.Retificacao.Motivo;
    }

    /// <summary>
    /// Registra um ato publicado. A <paramref name="essencia"/> traz os atributos de consequência já
    /// resolvidos por valor do catálogo vigente; a validação de negócio (existência de versão vigente,
    /// formato do payload) é responsabilidade da conferência da declaração. Aqui ficam as invariantes de
    /// última linha, que lançam — o agregado nunca materializa em estado inválido. As regras de
    /// retificação que dependem do ato retificado (classe de congelamento coincidente, linearidade da
    /// cadeia) são do handler, que as consulta no repositório; a essência garante só a simetria de
    /// forma do par.
    ///
    /// O <paramref name="id"/> vem do domínio que publica, quando ele o decide: é o que torna a
    /// reentrega da fila durável (at-least-once) idempotente, porque o segundo processamento tenta gravar
    /// o MESMO id e a chave primária o recusa. Sem isso, a reentrega criaria um ato gêmeo, e o gêmeo
    /// disputaria a vaga de linhagem do objeto (ADR-0107) contra a linhagem do primeiro. Vazio, o
    /// identificador é gerado aqui.
    ///
    /// Os <paramref name="vinculos"/> são construídos a partir do ato recém-criado, de
    /// modo que nenhum deles possa apontar para outro ato. A unicidade da linhagem por
    /// objeto (tipos <c>unico_por_objeto</c>) depende do estado já gravado e é do
    /// handler, não daqui.
    ///
    /// Os <paramref name="anexos"/> são guardados na ordem recebida; a regra do que um anexo
    /// precisa ter é de <see cref="AnexoDoAto.Recusas"/>.
    /// </summary>
    public static AtoNormativo Registrar(
        Guid id,
        EssenciaDocumentalDoAto essencia,
        DateTimeOffset publicadoEm,
        ReferenciaVersaoDoObjeto? versaoDoObjeto,
        IEnumerable<EntidadeVinculada>? vinculos = null,
        IEnumerable<(string Nome, string TipoMidia, string Hash)>? anexos = null)
    {
        AtoNormativo ato = new(essencia.Conferida())
        {
            Id = id == Guid.Empty ? Guid.CreateVersion7() : id,
            PublicadoEm = publicadoEm,
            VersaoDoObjeto = versaoDoObjeto,
        };
        ato.Vincular(vinculos ?? []);
        ato.Anexar(anexos ?? []);

        return ato;
    }

    /// <summary>
    /// Todas as recusas dos vínculos, uma parte por posição: o elemento nulo (campo
    /// <c>vinculos[i]</c>) ou então o tipo e o identificador da entidade e, por último, a repetição.
    /// O trio (ato, tipo, id) é único: a segunda ocorrência da mesma entidade não acrescenta vínculo
    /// nenhum, e o índice único do banco recusaria a segunda linha. A recusa da repetição não escreve o
    /// tipo nem o identificador.
    /// </summary>
    internal static IReadOnlyList<ParteConferida<EntidadeVinculada>> ConferirVinculos(IReadOnlyList<EntidadeVinculada?> vinculos)
    {
        HashSet<EntidadeVinculada> jaVinculadas = [];
        return [.. vinculos.Select((vinculo, posicao) => ConferirVinculo(vinculo, $"vinculos[{posicao}]", jaVinculadas))];
    }

    private static ParteConferida<EntidadeVinculada> ConferirVinculo(
        EntidadeVinculada? vinculo, string caminho, HashSet<EntidadeVinculada> jaVinculadas)
    {
        if (vinculo is not { } entidade)
        {
            return RecusaDoVinculo(caminho, AtoNormativoErrorCodes.VinculoNulo, "Vínculo nulo na lista de vínculos; retire o elemento ou preencha.");
        }

        ParteConferida<EntidadeVinculada> conferida = entidade.Conferir();
        FieldError[] daForma = [.. conferida.Recusas.Select(recusa => recusa.NoCaminhoDe(caminho))];
        FieldError[] daRepeticao = jaVinculadas.Add(entidade) ? [] : [RepeticaoDoVinculo(caminho)];
        FieldError[] recusas = [.. daForma, .. daRepeticao];

        return recusas.Length == 0 ? conferida : ParteConferida<EntidadeVinculada>.Recusada(recusas);
    }

    private static FieldError RepeticaoDoVinculo(string caminho) =>
        new(caminho, new DomainError(
            AtoNormativoErrorCodes.VinculoRepetido,
            "A mesma entidade foi vinculada mais de uma vez a este ato. Retire a repetição."));

    private static ParteConferida<EntidadeVinculada> RecusaDoVinculo(string caminho, string codigo, string mensagem) =>
        ParteConferida<EntidadeVinculada>.Recusada([new(caminho, new DomainError(codigo, mensagem))]);

    private void Vincular(IEnumerable<EntidadeVinculada> vinculos)
    {
        IReadOnlyList<ParteConferida<EntidadeVinculada>> partes =
            ConferirVinculos([.. vinculos.Select(vinculo => (EntidadeVinculada?)vinculo)]);

        foreach (ParteConferida<EntidadeVinculada> parte in partes)
        {
            _vinculos.Add(VinculoAtoEntidade.Criar(this, parte.ValorOuLancar()));
        }
    }

    private void Anexar(IEnumerable<(string Nome, string TipoMidia, string Hash)> anexos)
    {
        (string Nome, string TipoMidia, string Hash)[] recebidos = [.. anexos];
        IReadOnlyList<FieldError> recusas = AnexoDoAto.Recusas(AnexoDoAto.Declarados(recebidos));
        if (recusas.Count > 0)
        {
            throw new ArgumentException(recusas[0].Error.Message, recusas[0].Field);
        }

        _anexos.AddRange(recebidos.Select((anexo, ordem) =>
            AnexoDoAto.Criar(this, ordem, anexo.Nome, anexo.TipoMidia, anexo.Hash)));
    }
}
