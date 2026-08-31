namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Aresta de precedência entre duas <see cref="FaseCanonica"/> (UNI-REQ-0064): a
/// <see cref="AntecessoraCodigo"/> precede a <see cref="SucessoraCodigo"/> no
/// cronograma de um processo seletivo, com ou sem sobreposição de janela
/// permitida (<see cref="PermiteSobreposicao"/>). O grafo de precedências é
/// <b>dado de cadastro</b>, não código: o gate de publicação do Módulo Seleção lê
/// as arestas vigentes via <c>IPrecedenciaFaseReader</c> — acrescentar uma aresta
/// muda o veredicto sem recompilar.
/// </summary>
/// <remarks>
/// <para>A dependência é <b>condicional</b>: vale onde as duas fases coexistem no
/// cronograma de um processo — a ausência de uma delas não é violação. Essa
/// avaliação é do consumidor (Módulo Seleção); aqui o cadastro só garante que o
/// próprio grafo é bem-formado.</para>
/// <para>Três guardas protegem o grafo na escrita, para que nenhum cronograma que
/// referencie as fases envolvidas se torne impossível de satisfazer: recusa de
/// <b>self-loop</b> (antecessora igual à sucessora), de <b>aresta duplicada</b>
/// (mesmo par já vivo no cadastro) e de qualquer aresta que feche um <b>ciclo</b>
/// no grafo vigente. Só as duas últimas dependem do conjunto de arestas vivas no
/// momento da escrita — por isso são as únicas que exigem o grafo injetado como
/// <b>parâmetro</b> da factory (ADR-0042, domínio nunca navega/consulta); self-loop
/// só compara os dois códigos entre si e pode ser avaliada antes de qualquer
/// leitura do grafo (ver <see cref="ValidarCodigos"/>).</para>
/// <para>Ao contrário de <see cref="FaseCanonica"/>, este cadastro <b>é</b>
/// seed-governado: as seis arestas estruturais do ciclo de vida do processo
/// seletivo são semeadas via migration (mesmo molde de <c>RegraCatalogo</c>), e o
/// CRUD admin permanece disponível para acrescentar novas arestas conforme o CEPS
/// precisar.</para>
/// </remarks>
public sealed class PrecedenciaFase : SoftDeletableEntity, IAuditableEntity
{
    public string AntecessoraCodigo { get; private set; } = null!;
    public string SucessoraCodigo { get; private set; } = null!;
    public bool PermiteSobreposicao { get; private set; }

    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    // EF Core materialization
    private PrecedenciaFase()
    {
    }

    private PrecedenciaFase(string antecessoraCodigo, string sucessoraCodigo, bool permiteSobreposicao)
    {
        AntecessoraCodigo = antecessoraCodigo;
        SucessoraCodigo = sucessoraCodigo;
        PermiteSobreposicao = permiteSobreposicao;
    }

    /// <summary>
    /// Valida formato e pertença ao conjunto canônico de ambos os códigos
    /// (ADR-0125: acumula até dois <see cref="FieldError"/>, um por campo, em vez
    /// de retornar no primeiro) e, só quando os dois são válidos, a guarda de
    /// self-loop — a única das três guardas do grafo que não depende do conjunto
    /// de arestas vivas. Não faz I/O: existe para o handler decidir se vale a pena
    /// travar o grafo e consultá-lo antes mesmo de chamar <see cref="Criar"/>, que
    /// revalida por conta própria e nunca confia num resultado calculado por fora.
    /// </summary>
    public static Result<(string Antecessora, string Sucessora)> ValidarCodigos(
        string? antecessoraCodigo, string? sucessoraCodigo)
    {
        List<FieldError> erros = [];

        Result<string> antecessoraResult = ValidarCodigo(
            antecessoraCodigo,
            PrecedenciaFaseErrorCodes.AntecessoraCodigoObrigatorio,
            PrecedenciaFaseErrorCodes.AntecessoraCodigoFormatoInvalido,
            PrecedenciaFaseErrorCodes.AntecessoraForaDoConjuntoCanonico,
            "Código da fase antecessora");
        if (antecessoraResult.IsFailure)
        {
            erros.Add(new("antecessoraCodigo", antecessoraResult.Error!));
        }

        Result<string> sucessoraResult = ValidarCodigo(
            sucessoraCodigo,
            PrecedenciaFaseErrorCodes.SucessoraCodigoObrigatorio,
            PrecedenciaFaseErrorCodes.SucessoraCodigoFormatoInvalido,
            PrecedenciaFaseErrorCodes.SucessoraForaDoConjuntoCanonico,
            "Código da fase sucessora");
        if (sucessoraResult.IsFailure)
        {
            erros.Add(new("sucessoraCodigo", sucessoraResult.Error!));
        }

        if (erros.Count > 0)
        {
            return Result<(string, string)>.ValidationFailure(erros);
        }

        string antecessora = antecessoraResult.Value!;
        string sucessora = sucessoraResult.Value!;

        // Self-loop não depende do grafo vigente — só compara os dois códigos já
        // normalizados entre si, por isso pode ser avaliado aqui.
        if (string.Equals(antecessora, sucessora, StringComparison.Ordinal))
        {
            return Result<(string, string)>.Failure(new DomainError(
                PrecedenciaFaseErrorCodes.SelfLoop,
                "A fase antecessora não pode ser igual à fase sucessora."));
        }

        return Result<(string, string)>.Success((antecessora, sucessora));
    }

    /// <summary>
    /// Cria uma nova aresta de precedência. Revalida <paramref name="antecessoraCodigo"/>/
    /// <paramref name="sucessoraCodigo"/> via <see cref="ValidarCodigos"/> (formato,
    /// conjunto canônico, self-loop) e recusa aresta duplicada e ciclo contra o
    /// grafo vigente (<paramref name="arestasVivas"/>, o conjunto de arestas vivas
    /// do cadastro no momento da escrita — carregado pelo handler via
    /// <c>IPrecedenciaFaseRepository</c>, nunca consultado pelo domínio).
    /// </summary>
    public static Result<PrecedenciaFase> Criar(
        string? antecessoraCodigo,
        string? sucessoraCodigo,
        bool permiteSobreposicao,
        IReadOnlyList<PrecedenciaFase> arestasVivas)
    {
        ArgumentNullException.ThrowIfNull(arestasVivas);

        Result<(string Antecessora, string Sucessora)> codigosResult =
            ValidarCodigos(antecessoraCodigo, sucessoraCodigo);
        if (codigosResult.IsFailure)
        {
            return Result<PrecedenciaFase>.ValidationFailure(codigosResult.Errors);
        }

        (string antecessora, string sucessora) = codigosResult.Value;

        bool duplicada = arestasVivas.Any(a =>
            string.Equals(a.AntecessoraCodigo, antecessora, StringComparison.Ordinal)
            && string.Equals(a.SucessoraCodigo, sucessora, StringComparison.Ordinal));
        if (duplicada)
        {
            return Result<PrecedenciaFase>.Failure(new DomainError(
                PrecedenciaFaseErrorCodes.ArestaDuplicada,
                "Já existe uma aresta de precedência viva com este par de fases."));
        }

        if (FechaCiclo(antecessora, sucessora, arestasVivas))
        {
            return Result<PrecedenciaFase>.Failure(new DomainError(
                PrecedenciaFaseErrorCodes.CicloDetectado,
                "A aresta informada fecharia um ciclo no grafo de precedências."));
        }

        return Result<PrecedenciaFase>.Success(new PrecedenciaFase(antecessora, sucessora, permiteSobreposicao));
    }

    /// <summary>
    /// Atualiza o único atributo editável da aresta: se ela permite sobreposição de
    /// janela. Antecessora e sucessora são <b>imutáveis</b> — a chave natural do
    /// par não muda; para trocá-lo, remova a aresta e crie outra.
    /// </summary>
    public void Atualizar(bool permiteSobreposicao)
    {
        PermiteSobreposicao = permiteSobreposicao;
    }

    /// <summary>
    /// Detecta se acrescentar a aresta <paramref name="antecessora"/> →
    /// <paramref name="sucessora"/> fecha um ciclo no grafo formado por
    /// <paramref name="arestasVivas"/>: verdadeiro sse já existe, no grafo
    /// vigente, um caminho de <paramref name="sucessora"/> de volta a
    /// <paramref name="antecessora"/> (busca em profundidade).
    /// </summary>
    private static bool FechaCiclo(
        string antecessora, string sucessora, IReadOnlyList<PrecedenciaFase> arestasVivas)
    {
        var visitados = new HashSet<string>(StringComparer.Ordinal);
        var pilha = new Stack<string>();
        pilha.Push(sucessora);

        while (pilha.Count > 0)
        {
            string atual = pilha.Pop();
            if (string.Equals(atual, antecessora, StringComparison.Ordinal))
            {
                return true;
            }

            if (!visitados.Add(atual))
            {
                continue;
            }

            foreach (PrecedenciaFase aresta in arestasVivas.Where(
                a => string.Equals(a.AntecessoraCodigo, atual, StringComparison.Ordinal)))
            {
                pilha.Push(aresta.SucessoraCodigo);
            }
        }

        return false;
    }

    private static Result<string> ValidarCodigo(
        string? valor, string codigoObrigatorio, string codigoFormatoInvalido, string codigoForaDoCanonico, string rotulo)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return Result<string>.Failure(new DomainError(codigoObrigatorio, $"{rotulo} é obrigatório."));
        }

        string normalizado = valor.Trim();
        if (!CodigoFase.EhValido(normalizado))
        {
            return Result<string>.Failure(new DomainError(
                codigoFormatoInvalido,
                $"{rotulo} deve conter apenas letras maiúsculas e sublinhado (sem hífen e sem dígito)."));
        }

        if (!FaseCanonicaCatalogo.EhCanonico(normalizado))
        {
            // Mensagem genérica de propósito (ADR-0023): nunca ecoar o dado rejeitado.
            return Result<string>.Failure(new DomainError(
                codigoForaDoCanonico,
                $"{rotulo} não pertence ao conjunto canônico."));
        }

        return Result<string>.Success(normalizado);
    }
}
