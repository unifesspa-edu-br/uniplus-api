namespace Unifesspa.UniPlus.Publicacoes.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

/// <summary>
/// Ato publicado — o registro central e append-only de que algo foi publicado
/// (ADR-0105). Guarda a essência documental do ato (órgão, série, ano, número,
/// tipo, data de publicação, hash do documento, assinante) e, por valor, os
/// atributos de consequência do catálogo e a versão de configuração que o
/// governou. É a <b>prova</b> do publicado: o passado documental não se muta.
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
/// <b>Data documental ≠ ordem de vigência.</b> <see cref="DataPublicacao"/> é o
/// que o documento declara e nunca ordena vigência; a ordem pertence à versão de
/// configuração, no relógio do sistema. <see cref="RegistradoEm"/> é o instante
/// forense em que o registro entrou no sistema — distinto da data documental.
/// </para>
/// <para>
/// <b>Cópia por valor.</b> <see cref="CongelaConfiguracao"/> e
/// <see cref="EfeitoIrreversivel"/> são copiados do <c>TipoAtoPublicado</c>
/// vigente na data de publicação, no instante do registro — nunca consultados de
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
/// </remarks>
public sealed class AtoNormativo : IForensicEntity
{
    private const int OrgaoMaxLength = 200;
    private const int SerieMaxLength = 100;
    private const int NumeroMaxLength = 60;
    private const int TipoCodigoMaxLength = 60;
    private const int AssinanteMaxLength = 200;
    private const int MotivoRetificacaoMaxLength = 1000;

    public Guid Id { get; private init; } = Guid.CreateVersion7();

    /// <summary>Órgão publicador que emitiu o ato (ex.: CEPS, CRCA).</summary>
    public string Orgao { get; private init; } = null!;

    /// <summary>Série de numeração do ato (ex.: EDITAL, AVISO) — atravessa certames.</summary>
    public string Serie { get; private init; } = null!;

    /// <summary>Ano da numeração declarada pelo órgão.</summary>
    public int Ano { get; private init; }

    /// <summary>Número que o órgão deu ao ato. Opcional — há atos sem número (ex.: comunicado).</summary>
    public string? Numero { get; private init; }

    /// <summary>Código do tipo de ato no catálogo (<c>TipoAtoPublicado</c>) vigente na data de publicação.</summary>
    public string TipoCodigo { get; private init; } = null!;

    /// <summary>Se o ato produz nova versão congelada da configuração — copiado por valor do catálogo (RN08).</summary>
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
    public DateOnly DataPublicacao { get; private init; }

    /// <summary>Hash SHA-256 do PDF publicado. É o que prova o publicado.</summary>
    public string DocumentoHash { get; private init; } = null!;

    /// <summary>Nome de quem assina o ato.</summary>
    public string Assinante { get; private init; } = null!;

    /// <summary>Instante forense em que o registro entrou no sistema (relógio do sistema, não o documento).</summary>
    public DateTimeOffset RegistradoEm { get; private init; }

    /// <summary>
    /// Versão de configuração que governou o ato, por valor <c>{id, hash}</c>
    /// (ADR-0075). Nula quando o ato não invoca configuração (ato autônomo).
    /// </summary>
    public ReferenciaVersaoConfiguracao? VersaoInvocada { get; private init; }

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

    // EF Core materialization
    private AtoNormativo()
    {
    }

    /// <summary>
    /// Registra um ato publicado. Os atributos de consequência
    /// (<paramref name="congelaConfiguracao"/>, <paramref name="efeitoIrreversivel"/>,
    /// <paramref name="unicoPorObjeto"/>) já vêm resolvidos por valor do catálogo
    /// vigente; a validação de negócio (existência de versão vigente, formato do
    /// payload) é responsabilidade do handler e do validator. Aqui ficam as
    /// invariantes de última linha, que lançam — o agregado nunca materializa em
    /// estado inválido. As regras de retificação que dependem do ato retificado
    /// (classe de congelamento coincidente, linearidade da cadeia) são do handler,
    /// que as consulta no repositório; a factory garante só a simetria de shape do
    /// par (<paramref name="atoRetificadoId"/>, <paramref name="motivoRetificacao"/>).
    ///
    /// Os <paramref name="vinculos"/> são construídos a partir do ato recém-criado, de
    /// modo que nenhum deles possa apontar para outro ato. A unicidade da linhagem por
    /// objeto (tipos <c>unico_por_objeto</c>) depende do estado já gravado e é do
    /// handler, não daqui.
    /// </summary>
    public static AtoNormativo Registrar(
        Guid id,
        string orgao,
        string serie,
        int ano,
        string? numero,
        string tipoCodigo,
        bool congelaConfiguracao,
        bool efeitoIrreversivel,
        bool unicoPorObjeto,
        DateOnly dataPublicacao,
        string documentoHash,
        string assinante,
        DateTimeOffset registradoEm,
        ReferenciaVersaoConfiguracao? versaoInvocada,
        Guid? atoRetificadoId = null,
        string? motivoRetificacao = null,
        IEnumerable<(string EntidadeTipo, Guid EntidadeId)>? vinculos = null)
    {
        string orgaoNorm = ExigirTexto(orgao, OrgaoMaxLength, nameof(orgao));
        string serieNorm = ExigirTexto(serie, SerieMaxLength, nameof(serie));
        string tipoCodigoNorm = ExigirTexto(tipoCodigo, TipoCodigoMaxLength, nameof(tipoCodigo));
        string assinanteNorm = ExigirTexto(assinante, AssinanteMaxLength, nameof(assinante));
        string? numeroNorm = NormalizarOpcional(numero, NumeroMaxLength, nameof(numero));
        string? motivoNorm = NormalizarOpcional(motivoRetificacao, MotivoRetificacaoMaxLength, nameof(motivoRetificacao));

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ano);

        if (!HashSha256.TemFormatoValido(documentoHash))
        {
            throw new ArgumentException(
                "Hash do documento deve ser um SHA-256 em hexadecimal minúsculo (64 caracteres).",
                nameof(documentoHash));
        }

        if (atoRetificadoId == Guid.Empty)
        {
            throw new ArgumentException(
                "Identificador do ato retificado não pode ser vazio.",
                nameof(atoRetificadoId));
        }

        // Simetria da retificação (ADR-0103): a linhagem é o par (ato retificado,
        // motivo) — um sem o outro não registra emenda alguma. A auto-referência é
        // impossível por esta via (o Id é gerado aqui, fresco); o insert cru fica
        // barrado por CHECK no banco.
        if ((atoRetificadoId is not null) != (motivoNorm is not null))
        {
            throw new ArgumentException(
                "A retificação é o par (ato retificado, motivo) completo, ou nenhum dos dois.",
                nameof(atoRetificadoId));
        }

        AtoNormativo ato = new()
        {
            // o id vem do domínio que publica, quando ele o decide. É o que
            // torna a reentrega da fila durável (at-least-once) idempotente: o segundo
            // processamento tenta gravar o MESMO id e a chave primária o recusa. Sem
            // isso, a reentrega criaria um ato gêmeo, e o gêmeo disputaria a vaga de
            // linhagem do objeto (ADR-0107) contra a linhagem do primeiro.
            Id = id == Guid.Empty ? Guid.CreateVersion7() : id,
            Orgao = orgaoNorm,
            Serie = serieNorm,
            Ano = ano,
            Numero = numeroNorm,
            TipoCodigo = tipoCodigoNorm,
            CongelaConfiguracao = congelaConfiguracao,
            EfeitoIrreversivel = efeitoIrreversivel,
            UnicoPorObjeto = unicoPorObjeto,
            DataPublicacao = dataPublicacao,
            DocumentoHash = documentoHash,
            Assinante = assinanteNorm,
            RegistradoEm = registradoEm,
            VersaoInvocada = versaoInvocada,
            AtoRetificadoId = atoRetificadoId,
            MotivoRetificacao = motivoNorm,
        };

        foreach ((string entidadeTipo, Guid entidadeId) in vinculos ?? [])
        {
            VinculoAtoEntidade vinculo = VinculoAtoEntidade.Criar(ato, entidadeTipo, entidadeId);

            // O trio (ato, tipo, id) é único: repetir a mesma entidade no mesmo ato não
            // acrescenta vínculo nenhum, e o índice único do banco recusaria a segunda
            // linha. O validator já devolve 422 antes; aqui é a última linha.
            if (ato._vinculos.Exists(v =>
                    string.Equals(v.EntidadeTipo, vinculo.EntidadeTipo, StringComparison.Ordinal)
                    && v.EntidadeId == vinculo.EntidadeId))
            {
                throw new ArgumentException(
                    $"A entidade {vinculo.EntidadeTipo}/{vinculo.EntidadeId} está vinculada mais de uma vez ao mesmo ato.",
                    nameof(vinculos));
            }

            ato._vinculos.Add(vinculo);
        }

        return ato;
    }

    private static string ExigirTexto(string valor, int maxLength, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(valor, paramName);
        string norm = valor.Trim();
        if (norm.Length > maxLength)
        {
            throw new ArgumentException(
                $"'{paramName}' deve ter no máximo {maxLength} caracteres.",
                paramName);
        }

        return norm;
    }

    private static string? NormalizarOpcional(string? valor, int maxLength, string paramName)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        string norm = valor.Trim();
        if (norm.Length > maxLength)
        {
            throw new ArgumentException(
                $"'{paramName}' deve ter no máximo {maxLength} caracteres.",
                paramName);
        }

        return norm;
    }
}
