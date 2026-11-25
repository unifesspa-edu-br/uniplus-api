namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using System.Collections.Generic;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Regra legal data-driven aplicável a um <see cref="ProcessoSeletivo"/>
/// antes da publicação (ADR-0058). Carrega citação legal, vigência temporal e
/// hash canônico para suportar evidência forense em mandados de segurança e
/// processos administrativos.
/// </summary>
/// <remarks>
/// A validação dos campos é do domínio e acumula toda violação do lote
/// (<see cref="ValidarCampos"/>); as factories a repetem, então nenhuma regra
/// nasce nem muda sem ter passado por ela.
/// </remarks>
public sealed class ObrigatoriedadeLegal : SoftDeletableEntity, IAuditableEntity
{
    /// <summary>
    /// Valor sentinela aceito em <see cref="TipoProcessoCodigo"/> para regras
    /// universais (aplicam-se a qualquer tipo de processo). Alinha com a chave
    /// de filtro pública <c>?tipoProcesso=*</c>.
    /// </summary>
    public const string TipoProcessoUniversal = "*";

    private const string ModalidadesMinimasVazia = "ObrigatoriedadeLegal.ModalidadesMinimasVazia";
    private const string CodigoEmBranco = "ObrigatoriedadeLegal.PredicadoComCodigoEmBranco";
    private const string AtendimentoDisponivelVazio = "ObrigatoriedadeLegal.AtendimentoDisponivelVazio";

    public string TipoProcessoCodigo { get; private set; } = null!;
    public CategoriaObrigatoriedade Categoria { get; private set; }
    public string RegraCodigo { get; private set; } = null!;
    public PredicadoObrigatoriedade Predicado { get; private set; } = null!;
    public string DescricaoHumana { get; private set; } = null!;
    public string BaseLegal { get; private set; } = null!;
    public string? AtoNormativoUrl { get; private set; }
    public string? PortariaInternaCodigo { get; private set; }
    public DateOnly VigenciaInicio { get; private set; }
    public DateOnly? VigenciaFim { get; private set; }

    /// <summary>
    /// Hash SHA-256 canônico do conteúdo semântico (CA-05). Recomputado pelo
    /// <c>ObrigatoriedadeLegalHistoricoInterceptor</c> antes do <c>SaveChanges</c>
    /// para garantir que mutações via reflection ou property hidratada
    /// fora-de-factory ainda assim resultem em hash correto.
    /// </summary>
    public string Hash { get; private set; } = null!;

    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    // Construtor de materialização do EF Core.
    private ObrigatoriedadeLegal()
    {
    }

    private ObrigatoriedadeLegal(
        string? tipoProcessoCodigo,
        CategoriaObrigatoriedade? categoria,
        string? regraCodigo,
        PredicadoObrigatoriedade? predicado,
        TextoNormativo texto,
        DateOnly? vigenciaInicio,
        DateOnly? vigenciaFim) =>
        Aplicar(tipoProcessoCodigo, categoria, regraCodigo, predicado, texto, vigenciaInicio, vigenciaFim);

    /// <summary>
    /// Confere todos os campos da regra sem efeito colateral e devolve cada violação, com o nome do campo no
    /// formato do contrato. É a única fonte das regras de campo: o handler a chama antes das consultas, e as
    /// factories a repetem.
    /// </summary>
    public static Result ValidarCampos(
        string? tipoProcessoCodigo,
        CategoriaObrigatoriedade? categoria,
        string? regraCodigo,
        PredicadoObrigatoriedade? predicado,
        TextoNormativo texto,
        DateOnly? vigenciaInicio,
        DateOnly? vigenciaFim) =>
        ValidacaoDaObrigatoriedadeLegal.Validar(
            tipoProcessoCodigo, categoria, regraCodigo, predicado, texto, vigenciaInicio, vigenciaFim);

    /// <summary>
    /// Cria uma regra na forma plena, com o texto aparado e o hash calculado sobre o conteúdo final.
    /// </summary>
    public static Result<ObrigatoriedadeLegal> Criar(
        string? tipoProcessoCodigo,
        CategoriaObrigatoriedade? categoria,
        string? regraCodigo,
        PredicadoObrigatoriedade? predicado,
        TextoNormativo texto,
        DateOnly? vigenciaInicio,
        DateOnly? vigenciaFim)
    {
        ArgumentNullException.ThrowIfNull(texto);
        Result validacao = ValidarCampos(
            tipoProcessoCodigo, categoria, regraCodigo, predicado, texto, vigenciaInicio, vigenciaFim);

        return validacao.IsFailure
            ? Result<ObrigatoriedadeLegal>.ValidationFailure(validacao.Errors)
            : Result<ObrigatoriedadeLegal>.Success(new ObrigatoriedadeLegal(
                tipoProcessoCodigo, categoria, regraCodigo, predicado, texto, vigenciaInicio, vigenciaFim));
    }

    /// <summary>
    /// Substitui todos os campos semânticos da regra e recomputa o hash (ADR-0058). Em violação, o estado
    /// da regra não muda. Semântica full-replace: o texto opcional ausente limpa o valor anterior, então o
    /// chamador repassa o que quer manter.
    /// </summary>
    public Result Atualizar(
        string? tipoProcessoCodigo,
        CategoriaObrigatoriedade? categoria,
        string? regraCodigo,
        PredicadoObrigatoriedade? predicado,
        TextoNormativo texto,
        DateOnly? vigenciaInicio,
        DateOnly? vigenciaFim)
    {
        ArgumentNullException.ThrowIfNull(texto);
        Result validacao = ValidarCampos(
            tipoProcessoCodigo, categoria, regraCodigo, predicado, texto, vigenciaInicio, vigenciaFim);
        if (validacao.IsSuccess)
        {
            Aplicar(tipoProcessoCodigo, categoria, regraCodigo, predicado, texto, vigenciaInicio, vigenciaFim);
        }

        return validacao;
    }

    /// <summary>
    /// Recusa predicado cujo conteúdo o tornaria impossível de avaliar ou
    /// vacuamente verdadeiro. Valida <b>forma</b>, não existência: se o código
    /// referenciado corresponde a um cadastro vivo é pergunta com I/O, que o
    /// handler responde (ADR-0125).
    /// </summary>
    /// <remarks>
    /// <para>Público porque a ordem importa: o handler confere a forma <b>antes</b> de
    /// resolver as referências no cadastro. Um código em branco vira busca por string
    /// vazia, que o cadastro legitimamente não encontra — e a recusa sairia como
    /// "código não existe", escondendo do cliente que o campo simplesmente não foi
    /// preenchido. A factory chama esta mesma validação, então a regra continua com
    /// uma fonte só.</para>
    /// <para>Uma exigência de modalidades mínimas sem nenhuma modalidade é aprovada por
    /// vacuidade em <c>AvaliadorConformidadeLegal</c> — a regra existe, aparece
    /// como cumprida e não exige nada de ninguém. Código em branco tem o mesmo
    /// efeito: nunca casa com o valor congelado no processo, e a cláusula vira
    /// letra morta sem que nada sinalize.</para>
    /// </remarks>
    public static Result ValidarFormaDoPredicado(PredicadoObrigatoriedade predicado) => predicado switch
    {
        ModalidadesMinimas m => ValidarModalidadesMinimas(m),
        EtapaObrigatoria e => CodigoPreenchido(e.TipoEtapaCodigo, "Exigência de etapa obrigatória precisa do código do tipo de etapa."),
        DocumentoObrigatorioParaModalidade d => ValidarDocumentoPorModalidade(d),
        AtendimentoDisponivel a => ValidarAtendimentoDisponivel(a),
        DesempateDeveIncluir d => CodigoPreenchido(d.Criterio, "Exigência de critério de desempate precisa do código do critério."),
        _ => Result.Success(),
    };

    private static Result ValidarModalidadesMinimas(ModalidadesMinimas modalidades) =>
        modalidades.Codigos is null || modalidades.Codigos.Count == 0
            ? Falha(ModalidadesMinimasVazia, "Exigência de modalidades mínimas precisa de ao menos um código de modalidade.")
            : modalidades.Codigos.Any(string.IsNullOrWhiteSpace)
                ? Falha(ModalidadesMinimasVazia, "Exigência de modalidades mínimas não admite código em branco na lista.")
                : Result.Success();

    private static Result ValidarDocumentoPorModalidade(DocumentoObrigatorioParaModalidade documento) =>
        string.IsNullOrWhiteSpace(documento.Modalidade) || string.IsNullOrWhiteSpace(documento.TipoDocumento)
            ? Falha(CodigoEmBranco, "Exigência de documento por modalidade precisa do código da modalidade e do tipo de documento.")
            : Result.Success();

    private static Result ValidarAtendimentoDisponivel(AtendimentoDisponivel atendimento) =>
        atendimento.Necessidades is null || atendimento.Necessidades.Count == 0
            ? Falha(AtendimentoDisponivelVazio, "Exigência de atendimento disponível precisa de ao menos um código de tipo de deficiência.")
            : atendimento.Necessidades.Any(string.IsNullOrWhiteSpace)
                ? Falha(CodigoEmBranco, "Exigência de atendimento disponível não admite código em branco na lista.")
                : Result.Success();

    private static Result CodigoPreenchido(string? codigo, string mensagem) =>
        string.IsNullOrWhiteSpace(codigo) ? Falha(CodigoEmBranco, mensagem) : Result.Success();

    private static Result Falha(string codigo, string mensagem) =>
        Result.ValidationFailure([new FieldError("predicado", new DomainError(codigo, mensagem))]);

    // Só recebe lote que passou por ValidarCampos: os campos obrigatórios chegam preenchidos.
    private void Aplicar(
        string? tipoProcessoCodigo,
        CategoriaObrigatoriedade? categoria,
        string? regraCodigo,
        PredicadoObrigatoriedade? predicado,
        TextoNormativo texto,
        DateOnly? vigenciaInicio,
        DateOnly? vigenciaFim)
    {
        TipoProcessoCodigo = TextoNormalizavel.AparadoEmNfc(ValidacaoDaObrigatoriedadeLegal.Aparar(tipoProcessoCodigo));
        Categoria = categoria!.Value;
        RegraCodigo = regraCodigo!;
        Predicado = predicado!;
        AplicarTexto(texto);
        VigenciaInicio = vigenciaInicio!.Value;
        VigenciaFim = vigenciaFim;
        Hash = HashCanonicalComputer.Compute(this);
    }

    private void AplicarTexto(TextoNormativo texto)
    {
        DescricaoHumana = ValidacaoDaObrigatoriedadeLegal.Aparar(texto.DescricaoHumana);
        BaseLegal = ValidacaoDaObrigatoriedadeLegal.Aparar(texto.BaseLegal);
        AtoNormativoUrl = ValidacaoDaObrigatoriedadeLegal.AparaOuNulo(texto.AtoNormativo);
        PortariaInternaCodigo = ValidacaoDaObrigatoriedadeLegal.AparaOuNulo(texto.PortariaInternaCodigo);
    }

    /// <summary>
    /// Recomputa o hash a partir do estado atual da entidade — invocado pelo
    /// <c>ObrigatoriedadeLegalHistoricoInterceptor</c> antes do save para
    /// proteger contra mutações via reflection/EF property hydration que
    /// bypassem <see cref="Criar"/> e <see cref="Atualizar"/>.
    /// </summary>
    public string RecomputeHash()
    {
        Hash = HashCanonicalComputer.Compute(this);
        return Hash;
    }
}
