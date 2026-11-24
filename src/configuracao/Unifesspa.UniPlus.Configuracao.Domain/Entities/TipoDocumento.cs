namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Tipo de documento — cadastro institucional <b>classificatório puro</b>
/// (UNI-REQ-0013, módulo Configuração): diz <i>o que um documento é</i> (RG,
/// laudo médico, autodeclaração PPI…), nunca uma regra material sobre ele
/// (validade, assinatura, idade de emissão). Essas regras vivem na exigência
/// documental do edital (banco de Seleção) ou na homologação (ADR-0072).
/// </summary>
/// <remarks>
/// <para>O <c>Codigo</c> é a chave natural, único entre tipos vivos (índice único
/// parcial <c>WHERE is_deleted = false</c>) — e <b>editável</b> (diferente da
/// Modalidade), pois o consumo cross-módulo é por snapshot-copy desacoplado
/// (ADR-0061): editar o código vivo não altera o rótulo já congelado numa
/// exigência de Seleção. A unicidade é checada pelo handler (com proteção de
/// corrida via índice).</para>
/// <para>A <c>Categoria</c> é o <b>código</b> de uma categoria do cadastro, guardado
/// como texto sem chave estrangeira — referência classificatória, no molde de
/// <c>PrecedenciaFase</c> → <c>FaseCanonica</c>. A existência é conferida pelo
/// handler no momento da escrita; depois disso o rótulo é autônomo: renomear ou
/// remover a categoria não altera nem invalida os tipos que a referenciavam, e um
/// tipo pode acabar apontando para código sem alvo vivo. É a mesma natureza do
/// <c>TipoEquivalente</c> abaixo, e é o que mantém a remoção de categoria livre —
/// o desacoplamento de quem consome vem do snapshot-copy (ADR-0061), não de
/// integridade referencial.</para>
/// <para>O <c>TipoEquivalente</c> é <b>rótulo classificatório</b> (RG ≡ CIN), não
/// relacionamento material: guarda o <c>Codigo</c> de outro tipo (sem FK), e o
/// único guarda é não ser equivalente a si mesmo. Por ser rótulo e não FK,
/// remover um tipo apontado como equivalente por outro <b>não</b> é bloqueado
/// (CA-04) — o rótulo do outro fica apontando para um código sem alvo vivo.</para>
/// <para>O tipo declara a proteção do documento que descreve — classificação, finalidade e hipótese
/// legal, obrigatórias, com as mesmas regras do fato do candidato. O cadastro em si é dado
/// institucional sem PII. A remoção é sempre soft-delete e nunca bloqueada por referência.</para>
/// </remarks>
public sealed class TipoDocumento : SoftDeletableEntity, IAuditableEntity
{
    public CodigoTipoDocumento Codigo { get; private set; } = null!;
    public string Nome { get; private set; } = string.Empty;
    public string? Descricao { get; private set; }
    public string Categoria { get; private set; } = string.Empty;
    public string? FormatosAceitos { get; private set; }
    public int? TamanhoMaximoMb { get; private set; }
    public string? TipoEquivalente { get; private set; }

    /// <summary>
    /// Proteção que o documento exige: o tipo diz o que o documento contém, e a exigência de um
    /// processo congela esta cópia ao ser definida (ADR-0061).
    /// </summary>
    public ClassificacaoDeProtecao ClassificacaoProtecao { get; private set; }

    public string FinalidadeTratamento { get; private set; } = string.Empty;
    public HipoteseLegalTratamento HipoteseLegal { get; private set; }

    /// <summary>
    /// O circuito de aprovação da proteção de dados (ADR-0153): o tipo só entra na configuração e na
    /// publicação de processo seletivo depois de aprovado, e criar ou mudar a proteção o deixa pendente.
    /// </summary>
    public AprovacaoDaProtecao Aprovacao { get; private set; } = new();

    /// <summary>A proteção de dados do tipo: a classificação, a finalidade e a hipótese legal.</summary>
    public ProtecaoDeDados Protecao => new(ClassificacaoProtecao, FinalidadeTratamento, HipoteseLegal);

    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    // EF Core materialization
    private TipoDocumento()
    {
    }

    /// <summary>
    /// Confere todos os campos da declaração, acumulando cada violação em vez de retornar na primeira (ADR-0125),
    /// sem mutar nada. Existe para a gravação decidir se vale a pena buscar a entidade e consultar o cadastro antes
    /// de chamar <see cref="Atualizar"/> ou <see cref="Criar"/>, que reconferem por conta própria e nunca confiam
    /// num resultado calculado por fora.
    /// </summary>
    public static Result<CamposDoTipoDocumento> ValidarDeclaracao(DeclaracaoDoTipoDocumento declaracao)
    {
        ArgumentNullException.ThrowIfNull(declaracao);

        return new ValidacaoDoTipoDocumento(declaracao).Resolver();
    }

    /// <summary>
    /// Cria um novo TipoDocumento. Reconfere a declaração por <see cref="ValidarDeclaracao"/>, acumulando toda
    /// violação no mesmo lote. A existência da categoria e a unicidade de <c>Codigo</c> entre tipos vivos são da
    /// gravação.
    /// </summary>
    public static Result<TipoDocumento> Criar(DeclaracaoDoTipoDocumento declaracao)
    {
        Result<CamposDoTipoDocumento> campos = ValidarDeclaracao(declaracao);
        if (campos.IsFailure)
        {
            return Result<TipoDocumento>.ValidationFailure(campos.Errors);
        }

        var tipo = new TipoDocumento();
        tipo.AplicarCampos(campos.Value!);

        return Result<TipoDocumento>.Success(tipo);
    }

    /// <summary>
    /// Atualiza os atributos do TipoDocumento. O <c>Codigo</c> é editável; a unicidade dele (quando alterado) é da
    /// gravação. Reconfere a declaração por <see cref="ValidarDeclaracao"/>, inclusive a auto-equivalência.
    /// </summary>
    public Result Atualizar(DeclaracaoDoTipoDocumento declaracao)
    {
        Result<CamposDoTipoDocumento> campos = ValidarDeclaracao(declaracao);
        if (campos.IsFailure)
        {
            return Result.ValidationFailure(campos.Errors);
        }

        AplicarCampos(campos.Value!);

        return Result.Success();
    }

    /// <summary>Envia o tipo pendente à proteção de dados, numa solicitação.</summary>
    public Result SolicitarAprovacaoDaProtecao(string ator, DateTimeOffset em) => Aprovacao.Solicitar(ator, em);

    /// <summary>A proteção de dados aprova a classificação, a finalidade e a hipótese legal do tipo.</summary>
    public Result AprovarProtecao(string ator, DateTimeOffset em) => Aprovacao.Aprovar(ator, em);

    /// <summary>A proteção de dados recomenda o ajuste da proteção do tipo e indica a classificação correta.</summary>
    public Result RecomendarAjusteDaProtecao(ClassificacaoDeProtecao? recomendada, string? justificativa, string ator, DateTimeOffset em)
    {
        Result recusa = AprovacaoDaProtecao.ConferirRecomendacao(recomendada, ClassificacaoProtecao, HipoteseLegal);
        return recusa.IsFailure ? recusa : Aprovacao.RecomendarAjuste(recomendada, justificativa, ator, em);
    }

    /// <summary>A proteção de dados reprova a proteção do tipo, sem recomendar classificação, e explica o motivo.</summary>
    public Result ReprovarProtecao(string? justificativa, string ator, DateTimeOffset em) => Aprovacao.Reprovar(justificativa, ator, em);

    /// <summary>
    /// O administrador aceita a recomendação da proteção de dados: o tipo passa à classificação
    /// recomendada e fica aprovado, sem nova solicitação. O ajuste já recusou a recomendação que a hipótese legal não admite.
    /// </summary>
    public Result AceitarRecomendacaoDaProtecao(string ator, DateTimeOffset em)
    {
        Result<ClassificacaoDeProtecao> recomendada = Aprovacao.RecomendacaoAAceitar();
        if (recomendada.IsFailure)
        {
            return Result.Failure(recomendada.Error!);
        }

        Result aceita = Aprovacao.AceitarRecomendacao(recomendada.Value, ator, em);
        if (aceita.IsSuccess)
        {
            ClassificacaoProtecao = recomendada.Value;
        }

        return aceita;
    }

    /// <summary>
    /// A mudança da proteção reinicia o circuito de aprovação; o item com ajuste recomendado ou reprovado também
    /// recomeça por qualquer edição, e o nome e a descrição de um item aprovado não o reiniciam.
    /// </summary>
    private void AplicarCampos(CamposDoTipoDocumento campos)
    {
        Codigo = campos.Codigo;
        Nome = campos.Nome;
        Descricao = campos.Descricao;
        Categoria = campos.Categoria;
        FormatosAceitos = campos.FormatosAceitos;
        TamanhoMaximoMb = campos.TamanhoMaximoMb;
        TipoEquivalente = campos.TipoEquivalente;
        AplicarProtecao(campos.Protecao);
    }

    private void AplicarProtecao(ProtecaoDeDados protecao)
    {
        ProtecaoDeDados anterior = Protecao;

        ClassificacaoProtecao = protecao.Classificacao;
        FinalidadeTratamento = protecao.Finalidade;
        HipoteseLegal = protecao.Hipotese;

        Aprovacao.AoEditarItem(protecao != anterior);
    }
}
