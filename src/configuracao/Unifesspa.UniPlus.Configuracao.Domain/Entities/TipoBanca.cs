namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Tipo de banca (UNI-REQ-0139): cadastro classificatório das bancas que atuam na
/// seleção, um registro por tipo do vocabulário <see cref="TipoDeBanca"/>. Dado
/// institucional de referência sem PII (LGPD inaplicável). A <b>composição</b> de
/// uma banca (membros, atas, deliberações) é matéria de um incremento futuro e
/// <b>não</b> é modelada aqui — este cadastro entrega apenas o <b>tipo</b> da banca.
/// </summary>
/// <remarks>
/// <para>O <see cref="Codigo"/> (enum <see cref="TipoDeBanca"/>, gravado pelo código de
/// contrato) é a chave natural, único entre bancas vivas (índice único parcial
/// <c>WHERE is_deleted = false</c>) e <b>imutável</b>. O vocabulário de tipos é fechado
/// no enum.</para>
/// <para>A <see cref="FaseTipica"/> é a fase em que a banca usualmente atua — um
/// rótulo de texto <b>orientativo e não vinculante</b>, <b>não</b> uma referência
/// para o cadastro de fases. Pode ser nula e pode conter um valor que não
/// corresponda a nenhuma fase cadastrada.</para>
/// <para>A <see cref="Atuacao"/> (o papel, as duas marcas e os tipos de decisão julgados) diz como as
/// bancas do tipo atuam; as regras da seleção decidem por eles, nunca pelo código do tipo. Só o papel que avalia
/// o mérito declara tipos de decisão (ao menos um, sem repetição).</para>
/// <para>A remoção é sempre soft-delete; nunca bloqueada — o único consumo é por
/// snapshot-copy desacoplado no Módulo Seleção (ADR-0061).</para>
/// </remarks>
public sealed class TipoBanca : SoftDeletableEntity, IAuditableEntity
{
    private const int NomeMaxLength = 200;
    private const int FaseTipicaMaxLength = 60;
    private const int DescricaoMaxLength = 300;

    public TipoDeBanca Codigo { get; private set; }
    public string Nome { get; private set; } = null!;
    public string? FaseTipica { get; private set; }
    public string? Descricao { get; private set; }
    public AtuacaoDoTipoDeBanca Atuacao { get; private set; } = null!;

    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    // EF Core materialization
    private TipoBanca()
    {
    }

    /// <summary>
    /// Lê o código pelo vocabulário de tipos de banca, sem mutar nada — existe para o
    /// handler de criação decidir se vale a pena consultar a unicidade antes mesmo de
    /// chamar <see cref="Criar"/>, que revalida por conta própria e nunca confia num
    /// resultado calculado por fora. A leitura é pelo código exato, sem aparar espaços.
    /// </summary>
    public static Result<TipoDeBanca> ValidarCodigo(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return Recusa(TipoBancaErrorCodes.CodigoObrigatorio, "Código do tipo de banca é obrigatório.");
        }

        // A mensagem diz o que é aceito, pelos rótulos, e nunca repete o dado rejeitado (ADR-0023).
        return CatalogoDe<TipoDeBanca>.TentarObter(codigo, out TipoDeBanca tipo)
            ? Result<TipoDeBanca>.Success(tipo)
            : Recusa(TipoBancaErrorCodes.CodigoForaDoConjuntoCanonico, MensagemDosTiposAceitos());
    }

    private static string MensagemDosTiposAceitos() =>
        "O tipo de banca informado não é um dos aceitos. Tipos de banca aceitos: "
        + string.Join("; ", CatalogoDe<TipoDeBanca>.Todos().Select(static tipo => tipo.Rotulo))
        + ".";

    private static Result<TipoDeBanca> Recusa(string codigoDoErro, string mensagem) =>
        Result<TipoDeBanca>.ValidationFailure([new("codigo", new DomainError(codigoDoErro, mensagem))]);

    /// <summary>
    /// Valida os campos editáveis (nome, fase típica, descrição e a atuação do tipo), acumulando toda violação
    /// independente em vez de parar na primeira — sem mutar nada. Os limites de tamanho valem sobre o valor já
    /// normalizado (<c>Trim</c>), a mesma medida que <see cref="Aplicar"/> persiste.
    /// </summary>
    public static Result<(string Nome, string? FaseTipica, string? Descricao, AtuacaoDoTipoDeBanca Atuacao)> ValidarCampos(
        DeclaracaoDoTipoDeBanca declaracao)
    {
        ArgumentNullException.ThrowIfNull(declaracao);

        string? nome = NormalizarOpcional(declaracao.Nome);
        string? faseTipica = NormalizarOpcional(declaracao.FaseTipica);
        string? descricao = NormalizarOpcional(declaracao.Descricao);
        Result<AtuacaoDoTipoDeBanca> atuacao = AtuacaoDoTipoDeBanca.Validar(declaracao);
        FieldError[] erros = [.. ErrosDosCamposComuns(nome, faseTipica, descricao), .. atuacao.Errors];

        return erros.Length > 0
            ? Result<(string, string?, string?, AtuacaoDoTipoDeBanca)>.ValidationFailure(erros)
            : Result<(string, string?, string?, AtuacaoDoTipoDeBanca)>.Success((nome!, faseTipica, descricao, atuacao.Value!));
    }

    private static FieldError[] ErrosDosCamposComuns(string? nome, string? faseTipica, string? descricao) =>
    [
        .. new[]
        {
            ErroDoNome(nome),
            ExcedeOLimite("faseTipica", "Fase típica", faseTipica, FaseTipicaMaxLength, TipoBancaErrorCodes.FaseTipicaTamanho),
            ExcedeOLimite("descricao", "Descrição", descricao, DescricaoMaxLength, TipoBancaErrorCodes.DescricaoTamanho),
        }.OfType<FieldError>(),
    ];

    private static FieldError? ErroDoNome(string? nome) =>
        nome is null
            ? new("nome", new DomainError(TipoBancaErrorCodes.NomeObrigatorio, "Nome do tipo de banca é obrigatório."))
            : ExcedeOLimite("nome", "Nome", nome, NomeMaxLength, TipoBancaErrorCodes.NomeTamanho);

    private static FieldError? ExcedeOLimite(string campo, string descricaoDoCampo, string? valor, int maximo, string codigoDoErro) =>
        valor is not null && valor.Length > maximo
            ? new(campo, new DomainError(
                codigoDoErro,
                $"{descricaoDoCampo} do tipo de banca deve ter no máximo {maximo} caracteres."))
            : null;

    /// <summary>
    /// Cria um novo tipo de banca. Revalida <paramref name="codigo"/> via
    /// <see cref="ValidarCodigo"/> e os demais campos via
    /// <see cref="ValidarCampos"/>, acumulando toda violação no mesmo lote.
    /// A unicidade do código entre vivos é responsabilidade do handler.
    /// </summary>
    public static Result<TipoBanca> Criar(string? codigo, DeclaracaoDoTipoDeBanca declaracao)
    {
        Result<TipoDeBanca> codigoResult = ValidarCodigo(codigo);
        Result<(string Nome, string? FaseTipica, string? Descricao, AtuacaoDoTipoDeBanca Atuacao)> camposResult =
            ValidarCampos(declaracao);

        if (codigoResult.IsFailure || camposResult.IsFailure)
        {
            return Result<TipoBanca>.ValidationFailure([.. codigoResult.Errors, .. camposResult.Errors]);
        }

        var banca = new TipoBanca { Codigo = codigoResult.Value };
        banca.Aplicar(camposResult.Value);

        return Result<TipoBanca>.Success(banca);
    }

    /// <summary>
    /// Atualiza os atributos editáveis do tipo de banca. O <c>Codigo</c> e o
    /// <c>Id</c> são <b>imutáveis</b> — este método não os recebe.
    /// </summary>
    public Result Atualizar(DeclaracaoDoTipoDeBanca declaracao)
    {
        Result<(string Nome, string? FaseTipica, string? Descricao, AtuacaoDoTipoDeBanca Atuacao)> camposResult =
            ValidarCampos(declaracao);
        if (camposResult.IsFailure)
        {
            return Result.ValidationFailure(camposResult.Errors);
        }

        Aplicar(camposResult.Value);

        return Result.Success();
    }

    private void Aplicar((string Nome, string? FaseTipica, string? Descricao, AtuacaoDoTipoDeBanca Atuacao) campos)
    {
        Nome = campos.Nome;
        FaseTipica = campos.FaseTipica;
        Descricao = campos.Descricao;
        Atuacao = campos.Atuacao;
    }

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
