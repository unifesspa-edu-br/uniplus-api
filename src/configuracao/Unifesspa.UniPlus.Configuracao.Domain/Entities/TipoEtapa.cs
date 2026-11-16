namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using System.Text;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cadastro institucional de tipos de etapa de processo seletivo (UNI-REQ-0015, UNI-REQ-0087).
/// </summary>
/// <remarks>
/// O código é chave natural imutável e reservado para sempre: desativar impede
/// novos vínculos, mas não libera o código. Consumidores cross-módulo recebem
/// somente itens ativos e congelam sua cópia por valor (ADR-0061).
/// </remarks>
public sealed class TipoEtapa : EntityBase, IAuditableEntity
{
    private const int CodigoMaxLength = 64;
    private const int NomeMaxLength = 200;
    private const int DescricaoMaxLength = 1000;
    private const char CaractereNulo = (char)0;

    private readonly List<TipoConceito> _conceitosAdmitidos = [];

    public string Codigo { get; private set; } = string.Empty;
    public string Nome { get; private set; } = string.Empty;
    public string? Descricao { get; private set; }
    public bool Ativo { get; private set; }

    // O par diz quais caracteres de etapa este tipo admite: só pontuação admite apenas
    // caráter classificatório; só eliminação admite apenas eliminatório; os dois juntos
    // admitem também o caráter que acumula os dois papéis. É por ele que o cadastro
    // responde se uma etapa daquele tipo pode ter peso e se pode ter nota mínima, em vez
    // de o wizard carregar uma lista de códigos que envelhece a cada tipo novo.
    //
    // Dois sinalizadores, e não o próprio caráter da etapa: o caráter é vocabulário do
    // módulo Seleção, e Configuração não depende de Seleção. Limitação aceita: o par
    // expressa "só pontua", "só elimina" e "os dois", mas não expressa "pode acumular os
    // dois papéis, nunca apenas eliminar" — nenhum tipo institucional pediu isso até aqui.
    //
    // O par restringe o que pode ser declarado; não declara o fato. Quem diz que UMA etapa é
    // classificatória continua sendo quem monta o certame, na própria etapa — é por isso que
    // isto não recria a segunda fonte de verdade que a ADR-0113 desfez ao tirar "produz
    // resultado" e "é definitivo" do cadastro de fases canônicas: lá o cadastro afirmava sobre
    // a fase de um certame que ele não conhece, aqui ele afirma sobre a natureza do próprio
    // tipo. O Seleção congela o par no snapshot da etapa (ADR-0061), como congela o resto do
    // que traz daqui: estreitar o tipo depois não alcança a etapa já gravada, só quem mudar o
    // caráter ou o vínculo dela.
    public bool AdmitePontuacao { get; private set; }
    public bool AdmiteEliminacao { get; private set; }

    // Diz que a nota das etapas deste tipo vem do ENEM, e não de banca: é o que o Seleção
    // consulta para compor essa nota, em vez de reconhecer o tipo por um código escrito no
    // código-fonte. Faz parte da identidade do tipo, como o código: vem só da carga do
    // cadastro, e nenhuma escrita da API o altera.
    public bool NotaDeOrigemNoEnem { get; private set; }

    /// <summary>Os tipos de conceito com que as etapas deste tipo podem ser avaliadas, ordenados pelo código.</summary>
    public IReadOnlyList<TipoConceito> ConceitosAdmitidos =>
        [.. _conceitosAdmitidos.OrderBy(static conceito => conceito.Codigo, StringComparer.Ordinal)];

    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    private TipoEtapa() { }

    /// <summary>
    /// Valida o código (formato + tamanho), sem mutar nada — existe para o handler
    /// de criação decidir se vale a pena consultar a unicidade antes mesmo de
    /// chamar <see cref="Criar"/>, que revalida por conta própria.
    /// </summary>
    public static Result<string> ValidarCodigo(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return Result<string>.ValidationFailure([new("codigo", new DomainError(
                TipoEtapaErrorCodes.CodigoObrigatorio, "Código do tipo de etapa é obrigatório."))]);
        }

        // NFC além do Trim: sem normalizar aqui, duas grafias Unicode do mesmo texto (forma
        // composta e decomposta) cadastrariam como códigos DIFERENTES — o índice único do banco
        // é ordinal/binário, então as duas colidiriam com a checagem de duplicidade e, pior,
        // um lookup por código (TipoEtapaReader) feito com a outra forma nunca encontraria este
        // registro, mesmo sendo textualmente "o mesmo" código.
        string codigoNormalizado = codigo.Trim().Normalize(NormalizationForm.FormC);
        if (codigoNormalizado.Contains(CaractereNulo))
        {
            return Result<string>.ValidationFailure([new("codigo", new DomainError(
                TipoEtapaErrorCodes.CodigoComCaractereNulo,
                "Código do tipo de etapa não pode conter o caractere nulo (U+0000)."))]);
        }

        if (codigoNormalizado.Length > CodigoMaxLength)
        {
            return Result<string>.ValidationFailure([new("codigo", new DomainError(
                TipoEtapaErrorCodes.CodigoTamanho,
                $"Código do tipo de etapa deve ter no máximo {CodigoMaxLength} caracteres."))]);
        }

        return Result<string>.Success(codigoNormalizado);
    }

    /// <summary>
    /// Valida os campos editáveis — nome, descrição e o par de caracteres admitidos —,
    /// acumulando toda violação independente em vez de parar na primeira, sem mutar nada. O
    /// código não participa: é imutável, então <see cref="Atualizar"/> e seu
    /// handler nunca precisam revalidá-lo.
    /// </summary>
    /// <remarks>
    /// Os dois sinalizadores chegam anuláveis para que campo ausente no payload seja recusado
    /// aqui, com erro de domínio por campo, em vez de virar <c>false</c> em silêncio: um tipo
    /// nasceria declarando que não compõe nota sem ninguém ter dito isso.
    /// </remarks>
    public static Result<(string Nome, string? Descricao, bool AdmitePontuacao, bool AdmiteEliminacao)> ValidarCamposEditaveis(
        string? nome, string? descricao, bool? admitePontuacao, bool? admiteEliminacao)
    {
        List<FieldError> erros = [];
        string? nomeNormalizado = ConferirNome(nome, erros);
        string? descricaoNormalizada = ConferirDescricao(descricao, erros);
        erros.AddRange(ViolacoesDoCarater(admitePontuacao, admiteEliminacao));

        return erros.Count > 0
            ? Result<(string, string?, bool, bool)>.ValidationFailure(erros)
            : Result<(string, string?, bool, bool)>.Success(
                (nomeNormalizado!, descricaoNormalizada, admitePontuacao!.Value, admiteEliminacao!.Value));
    }

    private static string? ConferirNome(string? nome, List<FieldError> erros)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            erros.Add(Violacao("nome", TipoEtapaErrorCodes.NomeObrigatorio, "Nome do tipo de etapa é obrigatório."));
            return null;
        }

        string normalizado = nome.Trim().Normalize(NormalizationForm.FormC);
        if (ViolacaoDoNome(normalizado) is not { } violacao)
        {
            return normalizado;
        }

        erros.Add(violacao);
        return null;
    }

    private static FieldError? ViolacaoDoNome(string nome)
    {
        if (nome.Contains(CaractereNulo))
        {
            return Violacao("nome", TipoEtapaErrorCodes.NomeComCaractereNulo, "Nome do tipo de etapa não pode conter o caractere nulo (U+0000).");
        }

        return nome.Length > NomeMaxLength
            ? Violacao("nome", TipoEtapaErrorCodes.NomeTamanho, $"Nome do tipo de etapa deve ter no máximo {NomeMaxLength} caracteres.")
            : null;
    }

    private static string? ConferirDescricao(string? descricao, List<FieldError> erros)
    {
        string? normalizada = NormalizarOpcional(descricao);
        if (normalizada is not null && ViolacaoDaDescricao(normalizada) is { } violacao)
        {
            erros.Add(violacao);
        }

        return normalizada;
    }

    private static FieldError? ViolacaoDaDescricao(string descricao)
    {
        if (descricao.Contains(CaractereNulo))
        {
            return Violacao("descricao", TipoEtapaErrorCodes.DescricaoComCaractereNulo, "Descrição do tipo de etapa não pode conter o caractere nulo (U+0000).");
        }

        return descricao.Length > DescricaoMaxLength
            ? Violacao("descricao", TipoEtapaErrorCodes.DescricaoTamanho, $"Descrição do tipo de etapa deve ter no máximo {DescricaoMaxLength} caracteres.")
            : null;
    }

    private static FieldError[] ViolacoesDoCarater(bool? admitePontuacao, bool? admiteEliminacao) =>
        [.. SinalizadoresAusentes(admitePontuacao, admiteEliminacao), .. TipoSemCarater(admitePontuacao, admiteEliminacao)];

    private static IEnumerable<FieldError> SinalizadoresAusentes(bool? admitePontuacao, bool? admiteEliminacao)
    {
        if (admitePontuacao is null)
        {
            yield return Violacao("admitePontuacao", TipoEtapaErrorCodes.AdmitePontuacaoObrigatorio, "É obrigatório declarar se o tipo de etapa admite compor a nota final.");
        }

        if (admiteEliminacao is null)
        {
            yield return Violacao("admiteEliminacao", TipoEtapaErrorCodes.AdmiteEliminacaoObrigatorio, "É obrigatório declarar se o tipo de etapa admite eliminar candidato.");
        }
    }

    // Um tipo que não compõe nota nem elimina não configura etapa nenhuma: toda etapa
    // do certame declara um caráter, e nenhum caráter sobraria para escolher.
    private static IEnumerable<FieldError> TipoSemCarater(bool? admitePontuacao, bool? admiteEliminacao)
    {
        if (admitePontuacao == false && admiteEliminacao == false)
        {
            yield return Violacao("admitePontuacao", TipoEtapaErrorCodes.SemCaraterAdmitido, "O tipo de etapa deve admitir compor a nota final, eliminar candidato, ou os dois.");
        }
    }

    private static FieldError Violacao(string campo, string codigo, string mensagem) => new(campo, new DomainError(codigo, mensagem));

    /// <summary>
    /// Cria um novo TipoEtapa. Revalida código, nome e descrição, acumulando toda
    /// violação no mesmo lote. A unicidade do código é responsabilidade do
    /// handler.
    /// </summary>
    public static Result<TipoEtapa> Criar(
        string? codigo, string? nome, string? descricao, bool? admitePontuacao, bool? admiteEliminacao)
    {
        List<FieldError> erros = [];

        Result<string> codigoResult = ValidarCodigo(codigo);
        if (codigoResult.IsFailure)
        {
            erros.AddRange(codigoResult.Errors);
        }

        Result<(string Nome, string? Descricao, bool AdmitePontuacao, bool AdmiteEliminacao)> camposResult =
            ValidarCamposEditaveis(nome, descricao, admitePontuacao, admiteEliminacao);
        if (camposResult.IsFailure)
        {
            erros.AddRange(camposResult.Errors);
        }

        if (erros.Count > 0)
        {
            return Result<TipoEtapa>.ValidationFailure(erros);
        }

        return Result<TipoEtapa>.Success(new TipoEtapa
        {
            Codigo = codigoResult.Value!,
            Nome = camposResult.Value.Nome,
            Descricao = camposResult.Value.Descricao,
            Ativo = true,
            AdmitePontuacao = camposResult.Value.AdmitePontuacao,
            AdmiteEliminacao = camposResult.Value.AdmiteEliminacao,
        });
    }

    /// <summary>
    /// Atualiza apenas os campos editáveis; o código e a origem da nota permanecem imutáveis.
    /// Acumula as violações dos campos com a que depende do próprio tipo.
    /// </summary>
    public Result Atualizar(string? nome, string? descricao, bool? admitePontuacao, bool? admiteEliminacao)
    {
        Result<(string Nome, string? Descricao, bool AdmitePontuacao, bool AdmiteEliminacao)> campos =
            ValidarCamposEditaveis(nome, descricao, admitePontuacao, admiteEliminacao);
        List<FieldError> erros = campos.IsFailure ? [.. campos.Errors] : [];

        // A etapa de nota do ENEM compõe a média; sem pontuação, nenhuma etapa deste tipo
        // poderia ser configurada.
        if (NotaDeOrigemNoEnem && admitePontuacao == false)
        {
            erros.Add(new("admitePontuacao", new DomainError(
                TipoEtapaErrorCodes.NotaDeOrigemNoEnemExigePontuacao,
                "O tipo de etapa com nota de origem no ENEM deve admitir compor a nota final.")));
        }

        if (erros.Count > 0)
        {
            return Result.ValidationFailure(erros);
        }

        Nome = campos.Value.Nome;
        Descricao = campos.Value.Descricao;
        AdmitePontuacao = campos.Value.AdmitePontuacao;
        AdmiteEliminacao = campos.Value.AdmiteEliminacao;
        return Result.Success();
    }

    /// <summary>
    /// Troca o conjunto de conceitos admitidos pelo informado. Os conceitos chegam já conferidos
    /// contra o catálogo (<see cref="ConceitosPedidos"/>): existem, estão ativos e não se repetem.
    /// </summary>
    public void SubstituirConceitosAdmitidos(IReadOnlyCollection<TipoConceito> conceitos)
    {
        ArgumentNullException.ThrowIfNull(conceitos);
        _conceitosAdmitidos.Clear();
        _conceitosAdmitidos.AddRange(conceitos);
    }

    public Result Desativar()
    {
        if (!Ativo)
        {
            return Result.Failure(new DomainError(
                TipoEtapaErrorCodes.JaDesativado,
                "Tipo de etapa já foi removido."));
        }

        // Desativado, o tipo sai da leitura cross-módulo, e nenhuma etapa de nota do ENEM
        // poderia ser configurada.
        if (NotaDeOrigemNoEnem)
        {
            return Result.Failure(new DomainError(
                TipoEtapaErrorCodes.NotaDeOrigemNoEnemNaoDesativa,
                "O tipo de etapa com nota de origem no ENEM não pode ser removido."));
        }

        Ativo = false;
        return Result.Success();
    }

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim().Normalize(NormalizationForm.FormC);
}
