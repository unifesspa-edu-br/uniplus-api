namespace Unifesspa.UniPlus.Publicacoes.Domain.Entities;

using System.Linq.Expressions;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

/// <summary>
/// Tipo de ato publicado — cadastro do módulo Publicações (ADR-0105). Diz
/// <i>o que um ato é</i> (edital de abertura, aviso, convocação, resultado
/// final) e que consequências ele carrega, sem que nenhum desses tipos apareça
/// como literal em código: acrescentar um tipo é linha de cadastro (ADR-0103).
/// </summary>
/// <remarks>
/// <para>Os quatro atributos são <b>dados lidos</b>, jamais ramos de
/// comportamento. <c>CongelaConfiguracao</c> diz se o ato produz nova versão
/// congelada da configuração (congelamento dos parâmetros na publicação, UNI-REQ-0019); <c>UnicoPorObjeto</c>, se o objeto do ato
/// admite um único ato vivo daquele tipo; <c>EfeitoIrreversivel</c>, se a
/// publicação não pode ser desfeita; <c>EhResultado</c>, se o ato determina a
/// situação do candidato — só um ato assim pode integrar o ciclo recursal de uma
/// fase (UNI-REQ-0080). Os dois últimos não se combinam: não cabe recurso contra ato
/// irreversível, então o cadastro recusa o tipo que seja resultado e irreversível
/// (UNI-REQ-0166). Um <c>if (tipo.Codigo == "CONVOCACAO")</c> seria a violação que
/// a ADR-0103 proíbe.</para>
/// <para>O cadastro é <b>editável</b> — append-only vale para o ato publicado, não
/// para o catálogo. Editar o catálogo não reescreve o passado de ato nenhum: no
/// instante da publicação o ato <b>copia por valor</b> os atributos do tipo. Dois
/// mecanismos distintos, dois alvos distintos — a cópia por valor protege a
/// leitura do ato; a vigência protege a leitura do catálogo.</para>
/// <para>A janela de vigência é <b>semiaberta</b>: <c>[VigenciaInicio, VigenciaFim)</c>,
/// com fim exclusivo, como em <c>ObrigatoriedadeLegal</c>. Uma versão que encerra
/// em 01/06 e a sucessora que começa em 01/06 não se sobrepõem, e no dia da
/// fronteira vigora exatamente uma — a sucessora. A não-sobreposição entre versões
/// vivas do mesmo código é garantida pelo banco (exclusion constraint GIST), não
/// por guarda em memória: duas transações concorrentes furam qualquer guarda.</para>
/// <para>Dado institucional sem PII (LGPD inaplicável). A remoção é sempre
/// soft-delete, e libera a janela para uma nova versão do mesmo código.</para>
/// </remarks>
public sealed class TipoAtoPublicado : SoftDeletableEntity, IAuditableEntity
{
    public const int TamanhoMaximoDoCodigo = 60;

    public string Codigo { get; private set; } = string.Empty;
    public string Nome { get; private set; } = string.Empty;

    /// <summary>Se o ato deste tipo produz uma nova versão congelada da configuração (congelamento dos parâmetros na publicação, UNI-REQ-0019).</summary>
    public bool CongelaConfiguracao { get; private set; }

    /// <summary>Se o objeto do ato admite um único ato vivo deste tipo.</summary>
    public bool UnicoPorObjeto { get; private set; }

    /// <summary>Se a publicação de um ato deste tipo não pode ser desfeita.</summary>
    public bool EfeitoIrreversivel { get; private set; }

    /// <summary>
    /// Se um ato deste tipo determina a situação do candidato — só um ato assim
    /// pode integrar o ciclo recursal de uma fase (UNI-REQ-0080). <see langword="false"/>
    /// é proibição permanente: não é decisão do edital, é natureza do tipo.
    /// </summary>
    public bool EhResultado { get; private set; }

    /// <summary>
    /// Se um ato deste tipo pode ser publicado como documento avulso de um processo seletivo. É o
    /// catálogo que diz quais tipos servem a um aviso, a um comunicado ou a uma errata e quais pertencem
    /// a outro fluxo, como a convocação e a chamada do Ingresso — nenhum código compara o código do
    /// tipo com um literal (ADR-0103).
    /// </summary>
    public bool PublicavelComoAvulso { get; private set; }

    /// <summary>
    /// Se o ato deste tipo corrige sempre outro ato publicado: quem o publica indica o ato corrigido e o
    /// motivo (UNI-REQ-0156).
    /// </summary>
    public bool ExigeAtoCorrigido { get; private set; }

    /// <summary>Primeiro dia em que esta versão do tipo vale. Inclusivo.</summary>
    public DateOnly VigenciaInicio { get; private set; }

    /// <summary>Primeiro dia em que esta versão do tipo <b>já não</b> vale. Exclusivo; nulo enquanto a vigência é aberta.</summary>
    public DateOnly? VigenciaFim { get; private set; }

    /// <summary>Lei, artigo ou resolução que fundamenta o tipo de ato, quando houver.</summary>
    public string? BaseLegal { get; private set; }

    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    // EF Core materialization
    private TipoAtoPublicado()
    {
    }

    /// <summary>Confere a declaração sem alterar o agregado.</summary>
    public static Result<CamposDoTipoAtoPublicado> ValidarDeclaracao(DeclaracaoDoTipoAtoPublicado declaracao)
    {
        ArgumentNullException.ThrowIfNull(declaracao);
        return new ValidacaoDoTipoAtoPublicado(declaracao).Resolver();
    }

    /// <summary>Cria uma versão após conferir a declaração.</summary>
    public static Result<TipoAtoPublicado> Criar(DeclaracaoDoTipoAtoPublicado declaracao)
    {
        Result<CamposDoTipoAtoPublicado> campos = ValidarDeclaracao(declaracao);
        if (campos.IsFailure)
        {
            return Result<TipoAtoPublicado>.ValidationFailure(campos.Errors);
        }

        var tipo = new TipoAtoPublicado();
        tipo.AplicarCampos(campos.Value!);
        return Result<TipoAtoPublicado>.Success(tipo);
    }

    /// <summary>Atualiza os campos, preservando a identidade do tipo.</summary>
    public Result Atualizar(DeclaracaoDoTipoAtoPublicado declaracao)
    {
        Result<CamposDoTipoAtoPublicado> campos = ValidarDeclaracao(declaracao);
        if (campos.IsFailure)
        {
            return Result.ValidationFailure(campos.Errors);
        }

        Result codigo = ConferirCodigoInalterado(campos.Value!.Codigo);
        if (codigo.IsFailure)
        {
            return codigo;
        }

        AplicarCampos(campos.Value!);
        return Result.Success();
    }

    /// <summary>Confere a identidade após a validação do formato.</summary>
    public Result ConferirCodigoInalterado(string? codigo) =>
        string.Equals(codigo, Codigo, StringComparison.Ordinal)
            ? Result.Success()
            : Result.Failure(new DomainError(
                TipoAtoPublicadoErrorCodes.CodigoImutavel,
                "O código do tipo de ato é a sua identidade e não muda: a série de vigências "
                + "agrupa-se por ele, e é por ele que um objeto reserva a vaga de um ato único. "
                + "Para um tipo diferente, cadastre um tipo novo."));

    /// <summary>
    /// A janela semiaberta <c>[inicio, fim)</c> como expressão: fonte única da regra, composta pelas
    /// consultas. Declarada antes dos usos porque os inicializadores estáticos rodam na ordem do texto.
    /// </summary>
    private static readonly Expression<Func<DateOnly, DateOnly?, DateOnly, bool>> JanelaDeVigencia =
        (inicio, fim, dia) => inicio <= dia && (fim == null || fim > dia);

    /// <summary>
    /// Seleciona as versões cuja janela semiaberta <c>[VigenciaInicio, VigenciaFim)</c> contém
    /// <paramref name="dia"/>. É expressão, e não método, para que o EF a traduza em SQL; o dia vai
    /// como variável capturada, e portanto como parâmetro da consulta.
    /// </summary>
    public static Expression<Func<TipoAtoPublicado, bool>> VigenteEm(DateOnly dia)
    {
        Expression<Func<DateOnly>> diaDaConsulta = () => dia;
        ParameterExpression tipo = Expression.Parameter(typeof(TipoAtoPublicado), "tipo");

        return Expression.Lambda<Func<TipoAtoPublicado, bool>>(
            Expression.Invoke(
                JanelaDeVigencia,
                Expression.Property(tipo, nameof(VigenciaInicio)),
                Expression.Property(tipo, nameof(VigenciaFim)),
                diaDaConsulta.Body),
            tipo);
    }

    /// <summary>
    /// Verdadeiro quando <paramref name="data"/> cai na janela semiaberta
    /// <c>[VigenciaInicio, VigenciaFim)</c>. Espelha o predicado que o repositório
    /// traduz para SQL — existe para os testes de domínio e para leitura em memória.
    /// </summary>
    public bool EstaVigenteEm(DateOnly data) =>
        VigenciaInicio <= data && (VigenciaFim is null || VigenciaFim > data);

    private void AplicarCampos(CamposDoTipoAtoPublicado campos)
    {
        Codigo = campos.Codigo;
        Nome = campos.Nome;
        CongelaConfiguracao = campos.CongelaConfiguracao;
        UnicoPorObjeto = campos.UnicoPorObjeto;
        EfeitoIrreversivel = campos.EfeitoIrreversivel;
        EhResultado = campos.EhResultado;
        PublicavelComoAvulso = campos.PublicavelComoAvulso;
        ExigeAtoCorrigido = campos.ExigeAtoCorrigido;
        VigenciaInicio = campos.VigenciaInicio;
        VigenciaFim = campos.VigenciaFim;
        BaseLegal = campos.BaseLegal;
    }
}
