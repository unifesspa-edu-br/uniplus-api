namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Modelo de formulário composto pelo administrador para um tipo de processo — ou para todos — que declara
/// se serve à fase de inscrição e se é obrigatório (UNI-REQ-0144, ADR-0171). O processo parte de um modelo
/// e recebe uma cópia por valor (ADR-0061): mudar o modelo depois não muda o processo.
/// </summary>
/// <remarks>
/// O modelo segue as mesmas regras do formulário do processo, pelo mesmo código de
/// <c>Unifesspa.UniPlus.Regras</c>: a forma do cabeçalho, das páginas, dos itens e dos termos, o teto de
/// itens, a estrutura pela declaração e o grafo de coleta. A conferência contra o catálogo de fatos e de
/// termos é de quem tem o catálogo; o modelo recebe dele só as dependências dos derivados por regra.
/// O código e a declaração são imutáveis; vários modelos ativos convivem por tipo e declaração.
/// </remarks>
public sealed class ModeloFormulario : EntityBase, IAuditableEntity
{
    public const int CodigoMaxLength = 60;
    public const int NomeMaxLength = 200;
    public const int DescricaoMaxLength = 1000;
    public const int TipoProcessoCodigoMaxLength = FormatoDoCodigo.ComprimentoMaximo;

    private static readonly CampoDeCodigo CampoCodigo = new(
        "Código do modelo", CodigoMaxLength, ModeloFormularioErrorCodes.CodigoObrigatorio, ModeloFormularioErrorCodes.CodigoFormatoInvalido);

    private const string CaminhoDaDeclaracao = "declaracao";

    public string Codigo { get; private set; } = string.Empty;
    public string Nome { get; private set; } = string.Empty;
    public string? Descricao { get; private set; }

    /// <summary>Se o modelo serve à fase que coleta a inscrição e traz o conjunto básico de dados do candidato.</summary>
    public bool ServeAFaseDeInscricao { get; private set; }

    /// <summary>Se o modelo é obrigatório no processo em que for aplicado.</summary>
    public bool Obrigatorio { get; private set; }

    /// <summary>A declaração do modelo, imutável depois da criação.</summary>
    public DeclaracaoDoFormulario Declaracao => new(ServeAFaseDeInscricao, Obrigatorio);

    /// <summary>O tipo de processo a que o modelo se destina; nulo quando serve a todos.</summary>
    public string? TipoProcessoCodigo { get; private set; }

    public ConteudoDoModelo Conteudo { get; private set; } = new(null, [], [], [], [], []);
    public bool Ativo { get; private set; }
    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    private ModeloFormulario() { }

    /// <summary>
    /// Cria o modelo desativado, acumulando toda violação no mesmo lote (ADR-0125). O modelo só entra
    /// na escolha de processos novos quando o administrador o ativa, depois de montá-lo. A unicidade do
    /// código e a conferência contra o catálogo são de quem chama.
    /// </summary>
    /// <param name="derivacoes">As dependências de cada derivado por regra do catálogo, para o grafo.</param>
    public static Result<ModeloFormulario> Criar(
        string? codigo,
        DescritivoDoModelo descritivo,
        DeclaracaoDoFormulario? declaracao,
        ConteudoDoModelo conteudo,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes)
    {
        List<FieldError> erros = ConferirCriacao(codigo, descritivo, declaracao, conteudo, derivacoes, conteudoPorInteiro: true);
        if (erros.Count > 0)
        {
            return Result<ModeloFormulario>.ValidationFailure(erros);
        }

        ModeloFormulario modelo = Declarado(codigo!, declaracao!.Value);
        modelo.Aplicar(descritivo, conteudo.NaFormaGravada());
        return Result<ModeloFormulario>.Success(modelo);
    }

    /// <summary>Substitui o descritivo e o conteúdo; o código e a declaração são imutáveis.</summary>
    public Result Atualizar(
        DescritivoDoModelo descritivo,
        ConteudoDoModelo conteudo,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes)
    {
        List<FieldError> erros = ConferirAtualizacao(descritivo, conteudo, derivacoes, conteudoPorInteiro: true);
        if (erros.Count > 0)
        {
            return Result.ValidationFailure(erros);
        }

        Aplicar(descritivo, conteudo.NaFormaGravada());
        return Result.Success();
    }

    /// <summary>Reativar o que já está ativo é recusado: a operação é auditada, e aceitar o nada gravaria uma mudança que não houve.</summary>
    public Result Ativar()
    {
        if (Ativo)
        {
            return Result.Failure(new DomainError(ModeloFormularioErrorCodes.JaAtivo, "O modelo de formulário já está ativo."));
        }

        Ativo = true;
        return Result.Success();
    }

    /// <summary>Desativar tira o modelo da escolha de processos novos; os processos que já o copiaram não mudam.</summary>
    public Result Desativar()
    {
        if (!Ativo)
        {
            return Result.Failure(new DomainError(ModeloFormularioErrorCodes.JaDesativado, "O modelo de formulário já está desativado."));
        }

        Ativo = false;
        return Result.Success();
    }

    /// <summary>
    /// Todas as recusas da criação, na ordem das regras: código, descritivo, declaração e conteúdo. Não muda
    /// nada. <paramref name="conteudoPorInteiro"/> é falso quando parte do conteúdo enviado não pôde ser lida:
    /// o que depende do conteúdo inteiro — a estrutura e o grafo de coleta — não é conferido.
    /// </summary>
    public static List<FieldError> ConferirCriacao(
        string? codigo,
        DescritivoDoModelo descritivo,
        DeclaracaoDoFormulario? declaracao,
        ConteudoDoModelo conteudo,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes,
        bool conteudoPorInteiro)
    {
        ArgumentNullException.ThrowIfNull(descritivo);
        ArgumentNullException.ThrowIfNull(conteudo);
        ArgumentNullException.ThrowIfNull(derivacoes);

        return
        [
            .. ValidarCodigo(codigo),
            .. descritivo.Conferir(),
            .. ValidarDeclaracao(declaracao),
            .. ConferenciaDoConteudoDoModelo.Conferir(declaracao, conteudo.NaFormaGravada(), derivacoes, conteudoPorInteiro),
        ];
    }

    /// <summary>
    /// Todas as recusas da edição, na ordem das regras: descritivo e conteúdo, com a declaração do modelo
    /// gravado. Não muda nada; <paramref name="conteudoPorInteiro"/> tem o sentido de <see cref="ConferirCriacao"/>.
    /// </summary>
    public List<FieldError> ConferirAtualizacao(
        DescritivoDoModelo descritivo,
        ConteudoDoModelo conteudo,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes,
        bool conteudoPorInteiro)
    {
        ArgumentNullException.ThrowIfNull(descritivo);
        ArgumentNullException.ThrowIfNull(conteudo);
        ArgumentNullException.ThrowIfNull(derivacoes);

        return
        [
            .. descritivo.Conferir(),
            .. ConferenciaDoConteudoDoModelo.Conferir(Declaracao, conteudo.NaFormaGravada(), derivacoes, conteudoPorInteiro),
        ];
    }

    private static List<FieldError> ValidarDeclaracao(DeclaracaoDoFormulario? declaracao) =>
        declaracao is null ? [DeclaracaoIncompleta] : [];

    private static FieldError DeclaracaoIncompleta { get; } = new(
        CaminhoDaDeclaracao,
        new DomainError(
            ModeloFormularioErrorCodes.DeclaracaoIncompleta,
            "A declaração do modelo informa se ele serve à fase de inscrição e se é obrigatório."));

    private static List<FieldError> ValidarCodigo(string? codigo) =>
        CampoCodigo.Recusa(codigo, "codigo") is { } erro ? [erro] : [];

    private static ModeloFormulario Declarado(string codigo, DeclaracaoDoFormulario declaracao) => new()
    {
        Codigo = codigo,
        ServeAFaseDeInscricao = declaracao.ServeAFaseDeInscricao,
        Obrigatorio = declaracao.Obrigatorio,
        Ativo = false,
    };

    /// <summary>Grava o descritivo e o conteúdo já na forma gravada, na ordem em que é gravado.</summary>
    private void Aplicar(DescritivoDoModelo descritivo, ConteudoDoModelo conteudo)
    {
        Nome = (descritivo.Nome ?? string.Empty).Trim();
        Descricao = FormaDoItem.TextoOpcional(descritivo.Descricao);
        TipoProcessoCodigo = descritivo.TipoProcessoCodigo;
        Conteudo = conteudo.EmOrdem();
    }
}
