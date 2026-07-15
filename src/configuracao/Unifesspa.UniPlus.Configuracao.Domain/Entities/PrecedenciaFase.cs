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
/// no grafo vigente. As três dependem do conjunto de arestas vivas no momento da
/// escrita — por isso são <b>parâmetro</b> da factory (o grafo injetado, ADR-0042),
/// nunca navegação/consulta feita pelo domínio.</para>
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
    /// Cria uma nova aresta de precedência. Valida o formato e a pertença ao
    /// conjunto canônico das quatorze fases de ambos os códigos, e recusa
    /// self-loop, aresta duplicada e ciclo contra o grafo vigente
    /// (<paramref name="arestasVivas"/>, o conjunto de arestas vivas do cadastro no
    /// momento da escrita — carregado pelo handler via
    /// <c>IPrecedenciaFaseRepository</c>, nunca consultado pelo domínio).
    /// </summary>
    public static Result<PrecedenciaFase> Criar(
        string antecessoraCodigo,
        string sucessoraCodigo,
        bool permiteSobreposicao,
        IReadOnlyList<PrecedenciaFase> arestasVivas)
    {
        ArgumentNullException.ThrowIfNull(arestasVivas);

        Result<string> antecessoraResult = ValidarCodigo(
            antecessoraCodigo,
            PrecedenciaFaseErrorCodes.AntecessoraCodigoObrigatorio,
            PrecedenciaFaseErrorCodes.AntecessoraCodigoFormatoInvalido,
            PrecedenciaFaseErrorCodes.AntecessoraForaDoConjuntoCanonico,
            "Código da fase antecessora");
        if (antecessoraResult.IsFailure)
        {
            return Result<PrecedenciaFase>.Failure(antecessoraResult.Error!);
        }

        Result<string> sucessoraResult = ValidarCodigo(
            sucessoraCodigo,
            PrecedenciaFaseErrorCodes.SucessoraCodigoObrigatorio,
            PrecedenciaFaseErrorCodes.SucessoraCodigoFormatoInvalido,
            PrecedenciaFaseErrorCodes.SucessoraForaDoConjuntoCanonico,
            "Código da fase sucessora");
        if (sucessoraResult.IsFailure)
        {
            return Result<PrecedenciaFase>.Failure(sucessoraResult.Error!);
        }

        string antecessora = antecessoraResult.Value!;
        string sucessora = sucessoraResult.Value!;

        if (string.Equals(antecessora, sucessora, StringComparison.Ordinal))
        {
            return Result<PrecedenciaFase>.Failure(new DomainError(
                PrecedenciaFaseErrorCodes.SelfLoop,
                "A fase antecessora não pode ser igual à fase sucessora."));
        }

        bool duplicada = arestasVivas.Any(a =>
            string.Equals(a.AntecessoraCodigo, antecessora, StringComparison.Ordinal)
            && string.Equals(a.SucessoraCodigo, sucessora, StringComparison.Ordinal));
        if (duplicada)
        {
            return Result<PrecedenciaFase>.Failure(new DomainError(
                PrecedenciaFaseErrorCodes.ArestaDuplicada,
                $"Já existe uma aresta de precedência viva de '{antecessora}' para '{sucessora}'."));
        }

        if (FechaCiclo(antecessora, sucessora, arestasVivas))
        {
            return Result<PrecedenciaFase>.Failure(new DomainError(
                PrecedenciaFaseErrorCodes.CicloDetectado,
                $"A aresta de '{antecessora}' para '{sucessora}' fecharia um ciclo no grafo de precedências."));
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

            foreach (PrecedenciaFase aresta in arestasVivas)
            {
                if (string.Equals(aresta.AntecessoraCodigo, atual, StringComparison.Ordinal))
                {
                    pilha.Push(aresta.SucessoraCodigo);
                }
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
            return Result<string>.Failure(new DomainError(
                codigoForaDoCanonico,
                $"{rotulo} '{normalizado}' não pertence ao conjunto canônico das quatorze fases."));
        }

        return Result<string>.Success(normalizado);
    }
}
