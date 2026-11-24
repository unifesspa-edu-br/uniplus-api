namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using System.Text;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Confere, sem I/O, a declaração de um tipo de documento e devolve os campos já normalizados, ou todas as recusas de uma
/// vez (ADR-0125), cada uma rotulada pelo campo declarado. A auto-equivalência só é conferida depois de o código e o tipo
/// equivalente passarem nas próprias conferências, para não mascarar a causa de um dos dois atrás de uma relação inválida.
/// </summary>
internal sealed class ValidacaoDoTipoDocumento(DeclaracaoDoTipoDocumento declaracao)
{
    private const int NomeMinLength = 2;
    private const int NomeMaxLength = 200;
    private const int DescricaoMaxLength = 1000;
    private const int FormatosAceitosMaxLength = 200;

    // Acompanha o teto do próprio código (2 a 50): o campo guarda o Codigo de outro
    // tipo, então dimensioná-lo acima aceitaria texto que jamais poderá ser um código.
    private const int TipoEquivalenteMaxLength = 50;

    private readonly List<FieldError> _erros = [];

    private CodigoTipoDocumento? _codigo;
    private string? _nome;
    private string? _categoria;
    private string? _tipoEquivalente;

    public Result<CamposDoTipoDocumento> Resolver()
    {
        ConferirCodigo();
        ConferirNome();
        ConferirDescricao();
        ConferirCategoria();
        ConferirFormatosAceitos();
        ConferirTamanhoMaximo();
        ConferirTipoEquivalente();
        ConferirAutoEquivalencia();

        Result<ProtecaoDeDados> protecao = ConferirProtecao();

        return _erros.Count > 0
            ? Result<CamposDoTipoDocumento>.ValidationFailure(_erros)
            : Result<CamposDoTipoDocumento>.Success(Montar(protecao.Value));
    }

    private CamposDoTipoDocumento Montar(ProtecaoDeDados protecao) =>
        new(
            _codigo!,
            _nome!,
            Opcional(declaracao.Descricao),
            _categoria!,
            Opcional(declaracao.FormatosAceitos),
            declaracao.TamanhoMaximoMb,
            _tipoEquivalente,
            protecao);

    // Obrigatoriedade e formato são do value object: ele é a única definição do que conta
    // como código, e a mesma que o conversor aplica na reidratação.
    private void ConferirCodigo()
    {
        Result<CodigoTipoDocumento> codigo = CodigoTipoDocumento.Criar(declaracao.Codigo);
        if (codigo.IsFailure)
        {
            _erros.Add(new("codigo", codigo.Error!));
            return;
        }

        _codigo = codigo.Value;
    }

    private void ConferirNome()
    {
        string? nome = declaracao.Nome?.Trim();
        _nome = Conferido("nome", nome, RecusaDoNome(nome));
    }

    private static DomainError? RecusaDoNome(string? nome) =>
        string.IsNullOrEmpty(nome)
            ? new(TipoDocumentoErrorCodes.NomeObrigatorio, "Nome do tipo de documento é obrigatório.")
            : nome.Length is < NomeMinLength or > NomeMaxLength
                ? new(
                    TipoDocumentoErrorCodes.NomeTamanho,
                    $"Nome do tipo de documento deve ter entre {NomeMinLength} e {NomeMaxLength} caracteres.")
                : null;

    private void ConferirDescricao()
    {
        if (Opcional(declaracao.Descricao)?.Length > DescricaoMaxLength)
        {
            Recusar(
                "descricao",
                TipoDocumentoErrorCodes.DescricaoTamanho,
                $"Descrição do tipo de documento deve ter no máximo {DescricaoMaxLength} caracteres.");
        }
    }

    // A categoria é o código de um cadastro, não um token de vocabulário fechado: aqui só a
    // forma é conhecida. Se existe categoria viva com este código é pergunta para a gravação,
    // que consulta o cadastro (ADR-0125). Trim + NFC antes de validar: o valor normalizado é o
    // que persiste e o que a comparação ordinal a jusante — inclusive contra o cadastro — usa.
    private void ConferirCategoria()
    {
        string? categoria = string.IsNullOrWhiteSpace(declaracao.Categoria)
            ? null
            : declaracao.Categoria.Trim().Normalize(NormalizationForm.FormC);
        _categoria = Conferido("categoria", categoria, RecusaDaCategoria(categoria));
    }

    private static DomainError? RecusaDaCategoria(string? categoria) =>
        categoria is null
            ? new(TipoDocumentoErrorCodes.CategoriaObrigatoria, "Categoria do tipo de documento é obrigatória.")
            : !CodigoCategoriaDocumento.EhValido(categoria)
                ? new(
                    TipoDocumentoErrorCodes.CategoriaFormatoInvalido,
                    "Categoria do tipo de documento deve ser um código que inicia com letra maiúscula "
                    + "e contém apenas letras maiúsculas, dígitos e sublinhado, com 2 a 50 caracteres "
                    + "(ex.: RENDA, DOCUMENTO_PROCESSUAL).")
                : null;

    private void ConferirFormatosAceitos()
    {
        if (Opcional(declaracao.FormatosAceitos)?.Length > FormatosAceitosMaxLength)
        {
            Recusar(
                "formatosAceitos",
                TipoDocumentoErrorCodes.FormatosAceitosTamanho,
                $"Formatos aceitos devem ter no máximo {FormatosAceitosMaxLength} caracteres.");
        }
    }

    private void ConferirTamanhoMaximo()
    {
        if (declaracao.TamanhoMaximoMb is <= 0)
        {
            Recusar(
                "tamanhoMaximoMb",
                TipoDocumentoErrorCodes.TamanhoMaximoInvalido,
                "Tamanho máximo em MB, quando informado, deve ser positivo.");
        }
    }

    private void ConferirTipoEquivalente()
    {
        string? equivalente = Opcional(declaracao.TipoEquivalente);
        if (equivalente?.Length > TipoEquivalenteMaxLength)
        {
            Recusar(
                "tipoEquivalente",
                TipoDocumentoErrorCodes.TipoEquivalenteTamanho,
                $"Tipo equivalente deve ter no máximo {TipoEquivalenteMaxLength} caracteres.");
            return;
        }

        _tipoEquivalente = equivalente;
    }

    // Guarda de auto-equivalência case-sensitive (Ordinal), alinhada ao CHECK
    // `tipo_equivalente <> codigo` do banco (também case-sensitive).
    private void ConferirAutoEquivalencia()
    {
        if (_codigo is not null && string.Equals(_tipoEquivalente, _codigo.Valor, StringComparison.Ordinal))
        {
            Recusar(
                "tipoEquivalente",
                TipoDocumentoErrorCodes.TipoEquivalenteIgualCodigo,
                "Um tipo de documento não pode declarar-se equivalente a si mesmo.");
        }
    }

    // Vocabulário, limite da finalidade e coerência entre classificação e hipótese são os do
    // fato do candidato (ProtecaoDeDados): o tipo só dá o código de cada recusa.
    private Result<ProtecaoDeDados> ConferirProtecao()
    {
        Result<ProtecaoDeDados> protecao = ProtecaoDeDados.Validar(
            declaracao.ClassificacaoProtecao,
            declaracao.FinalidadeTratamento,
            declaracao.HipoteseLegal,
            RecusasDaProtecaoDoTipo);
        _erros.AddRange(protecao.Errors);
        return protecao;
    }

    private string? Conferido(string campo, string? valor, DomainError? recusa)
    {
        if (recusa is null)
        {
            return valor;
        }

        _erros.Add(new(campo, recusa));
        return null;
    }

    private void Recusar(string campo, string codigo, string mensagem) =>
        _erros.Add(new(campo, new DomainError(codigo, mensagem)));

    private static string? Opcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static readonly RecusasDaProtecao RecusasDaProtecaoDoTipo = new(
        "do tipo de documento",
        TipoDocumentoErrorCodes.ProtecaoObrigatoria,
        TipoDocumentoErrorCodes.ClassificacaoProtecaoInvalida,
        TipoDocumentoErrorCodes.ProtecaoObrigatoria,
        TipoDocumentoErrorCodes.FinalidadeTratamentoTamanho,
        TipoDocumentoErrorCodes.ProtecaoObrigatoria,
        TipoDocumentoErrorCodes.HipoteseLegalInvalida,
        TipoDocumentoErrorCodes.HipoteseLegalIncompativelComClassificacao);
}
