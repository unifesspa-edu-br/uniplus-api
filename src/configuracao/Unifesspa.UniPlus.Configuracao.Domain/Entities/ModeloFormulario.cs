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
    private static readonly CampoDeCodigo CampoTipoProcesso = new(
        "Código do tipo de processo", TipoProcessoCodigoMaxLength, ModeloFormularioErrorCodes.TipoProcessoCodigoEmBranco,
        ModeloFormularioErrorCodes.TipoProcessoCodigoFormatoInvalido);

    /// <summary>O caminho do descritivo — nome, descrição e tipo de processo — no corpo da escrita, onde as recusas dele se apontam.</summary>
    public const string CaminhoDoDescritivo = "descritivo";

    private const string CaminhoDaDeclaracao = "declaracao";

    public string Codigo { get; private set; } = string.Empty;
    public string Nome { get; private set; } = string.Empty;
    public string? Descricao { get; private set; }

    /// <summary>Se o modelo serve à fase que coleta a inscrição e traz o conjunto básico de dados do candidato.</summary>
    public bool ServeAFaseDeInscricao { get; private set; }

    /// <summary>Se o modelo é obrigatório. Até a finalidade do formulário do processo sair, o modelo que serve à inscrição é tratado como obrigatório ao ser aplicado.</summary>
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
        string? nome,
        string? descricao,
        DeclaracaoDoFormulario? declaracao,
        string? tipoProcessoCodigo,
        ConteudoDoModelo conteudo,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes)
    {
        ArgumentNullException.ThrowIfNull(conteudo);
        ArgumentNullException.ThrowIfNull(derivacoes);

        ConteudoDoModelo normalizado = conteudo.NaFormaGravada();
        List<FieldError> erros = ConferirCadastro(codigo, nome, descricao, tipoProcessoCodigo, declaracao);
        erros.AddRange(ConferenciaDoConteudoDoModelo.Conferir(declaracao, normalizado, derivacoes));
        if (erros.Count > 0)
        {
            return Result<ModeloFormulario>.ValidationFailure(erros);
        }

        ModeloFormulario modelo = Declarado(codigo!, declaracao!.Value);
        modelo.Aplicar(nome ?? string.Empty, descricao, tipoProcessoCodigo, normalizado);
        return Result<ModeloFormulario>.Success(modelo);
    }

    /// <summary>Substitui o descritivo e o conteúdo; o código e a declaração são imutáveis.</summary>
    public Result Atualizar(
        string? nome,
        string? descricao,
        string? tipoProcessoCodigo,
        ConteudoDoModelo conteudo,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes)
    {
        ArgumentNullException.ThrowIfNull(conteudo);
        ArgumentNullException.ThrowIfNull(derivacoes);

        ConteudoDoModelo normalizado = conteudo.NaFormaGravada();
        List<FieldError> erros = ConferirDescritivo(nome, descricao, tipoProcessoCodigo);
        erros.AddRange(ConferenciaDoConteudoDoModelo.Conferir(Declaracao, normalizado, derivacoes));
        if (erros.Count > 0)
        {
            return Result.ValidationFailure(erros);
        }

        Aplicar(nome ?? string.Empty, descricao, tipoProcessoCodigo, normalizado);
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
    /// O que o cadastro confere fora do conteúdo — código, descritivo, tipo de processo e declaração
    /// —, para que essas recusas saiam junto das do conteúdo mesmo quando ele não chega a ser lido.
    /// </summary>
    public static List<FieldError> ConferirCadastro(
        string? codigo, string? nome, string? descricao, string? tipoProcessoCodigo, DeclaracaoDoFormulario? declaracao) =>
        [.. ValidarCodigo(codigo), .. ValidarDescritivo(nome, descricao, tipoProcessoCodigo), .. ValidarDeclaracao(declaracao)];

    /// <summary>O que a edição confere fora do conteúdo: o descritivo e o tipo de processo.</summary>
    public static List<FieldError> ConferirDescritivo(string? nome, string? descricao, string? tipoProcessoCodigo) =>
        ValidarDescritivo(nome, descricao, tipoProcessoCodigo);

    private static List<FieldError> ValidarDeclaracao(DeclaracaoDoFormulario? declaracao) =>
        declaracao is null ? [DeclaracaoIncompleta] : [];

    private static FieldError DeclaracaoIncompleta { get; } = new(
        CaminhoDaDeclaracao,
        new DomainError(
            ModeloFormularioErrorCodes.DeclaracaoIncompleta,
            "A declaração do modelo informa se ele serve à fase de inscrição e se é obrigatório."));

    private static List<FieldError> ValidarCodigo(string? codigo) =>
        CampoCodigo.Recusa(codigo, "codigo") is { } erro ? [erro] : [];

    private static List<FieldError> ValidarDescritivo(string? nome, string? descricao, string? tipoProcessoCodigo) =>
    [
        .. NoDescritivo(
        [
            .. TextoGravavel.Recusar("nome", nome),
            .. TextoGravavel.Recusar("descricao", descricao),
            .. ValidarNome(nome),
            .. ValidarDescricao(descricao),
            .. ValidarTipoProcesso(tipoProcessoCodigo),
        ]),
    ];

    private static IEnumerable<FieldError> NoDescritivo(IEnumerable<FieldError> erros) =>
        erros.Select(static e => e.NoCaminhoDe(CaminhoDoDescritivo));

    private static IEnumerable<FieldError> ValidarNome(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            yield return new("nome", new DomainError(ModeloFormularioErrorCodes.NomeObrigatorio, "O nome do modelo é obrigatório."));
        }
        else if (nome.Trim().Length > NomeMaxLength)
        {
            yield return new("nome", new DomainError(
                ModeloFormularioErrorCodes.NomeTamanho, $"O nome do modelo deve ter no máximo {NomeMaxLength} caracteres."));
        }
    }

    private static IEnumerable<FieldError> ValidarDescricao(string? descricao)
    {
        if (FormaDoItem.TextoOpcional(descricao) is { Length: > DescricaoMaxLength })
        {
            yield return new("descricao", new DomainError(
                ModeloFormularioErrorCodes.DescricaoTamanho, $"A descrição do modelo deve ter no máximo {DescricaoMaxLength} caracteres."));
        }
    }

    private static IEnumerable<FieldError> ValidarTipoProcesso(string? tipoProcessoCodigo)
    {
        if (tipoProcessoCodigo is not null && CampoTipoProcesso.Recusa(tipoProcessoCodigo, "tipoProcessoCodigo") is { } erro)
        {
            yield return erro;
        }
    }

    private static ModeloFormulario Declarado(string codigo, DeclaracaoDoFormulario declaracao) => new()
    {
        Codigo = codigo,
        ServeAFaseDeInscricao = declaracao.ServeAFaseDeInscricao,
        Obrigatorio = declaracao.Obrigatorio,
        Ativo = false,
    };

    /// <summary>Grava o descritivo e o conteúdo já na forma gravada, na ordem em que é gravado.</summary>
    private void Aplicar(string nome, string? descricao, string? tipoProcessoCodigo, ConteudoDoModelo conteudo)
    {
        Nome = nome.Trim();
        Descricao = FormaDoItem.TextoOpcional(descricao);
        TipoProcessoCodigo = tipoProcessoCodigo;
        Conteudo = conteudo.EmOrdem();
    }
}
