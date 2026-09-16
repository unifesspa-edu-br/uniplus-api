namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using System.Text;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O que o operador já transcreveu do Diário Oficial no passo de Revisão, guardado para
/// sobreviver a um recarregamento — e <b>só para isso</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Seleção guarda, não modela.</b> O conteúdo é um documento JSON <b>opaco</b>: não há
/// aqui propriedade que nomeie órgão, série, assinante ou tipo de ato. A fronteira da
/// ADR-0108 — o ato pertence a Publicações, e Seleção só referencia o <c>AtoCriadorId</c>
/// depois de publicado — deixa de depender da disciplina de quem escreve o handler e passa a
/// ser sustentada pelo tipo: não existe membro para ler por engano.
/// </para>
/// <para>
/// <b>Nada daqui alimenta a publicação.</b> O <c>POST …/publicacao</c> continua recebendo os
/// dados do ato no próprio corpo e nunca lê esta linha. Declarar é um ato: o que vale é o que
/// o operador afirma no momento de publicar, não o que ficou guardado de uma sessão anterior.
/// Por isso o conteúdo também não entra no envelope congelado nem é alcançável pelo grafo de
/// configuração.
/// </para>
/// <para>
/// <b>Vinculado ao processo, mas fora do agregado</b> — molde de <see cref="DocumentoEdital"/>:
/// FK e repositório próprios, sem navegação em <see cref="ProcessoSeletivo"/>. Como a raiz é
/// <c>SoftDeletableEntity</c>, nenhum <c>DELETE</c> em cascata jamais dispara: a remoção é
/// explícita, em todo caminho que registra o ato.
/// </para>
/// <para>
/// <b><see cref="EntityBase"/> puro (ADR-0063).</b> Não é evidência forense — essa é o
/// <c>AtoNormativo</c> em Publicações. Apagar apaga de verdade, e não há histórico do que se
/// rascunhou.
/// </para>
/// <para>
/// <b>Um por operador</b> (<c>UNIQUE</c> com <see cref="UsuarioSub"/>): sem recurso
/// compartilhado não há concorrência a arbitrar. A consequência aceita é que o rascunho não se
/// transfere — quem assume o trabalho de um colega não o vê, e transcreve do mesmo documento
/// que ambos têm em mãos.
/// </para>
/// </remarks>
public sealed class RascunhoDePublicacao : EntityBase
{
    /// <summary>
    /// Teto do documento guardado, aferido em <b>bytes UTF-8</b> — a unidade em que o Postgres
    /// realmente armazena, e não em <c>string.Length</c>, que contaria um caractere acentuado
    /// como um e deixaria passar quase o dobro.
    /// </summary>
    /// <remarks>
    /// O bloco do ato são nove campos curtos: preenchido de ponta a ponta não passa de um
    /// quilobyte. O teto existe para impedir que a coluna vire depósito de qualquer outra
    /// coisa, não para apertar o uso legítimo.
    /// </remarks>
    public const int ConteudoMaxBytes = 8 * 1024;

    /// <summary>
    /// Quanto tempo um rascunho vale sem ser regravado.
    /// </summary>
    /// <remarks>
    /// Trinta dias cobrem com folga o caso que motivou guardar o bloco — preencher o ato,
    /// esbarrar numa pendência que leva dias para resolver e voltar depois —, e é curto o
    /// bastante para que um certame abandonado não carregue indefinidamente o nome de quem
    /// assinaria. A expiração é aplicada na leitura; sem job varrendo a tabela, o rascunho de
    /// um processo que ninguém mais abre só desaparece quando o processo for publicado ou o
    /// rascunho descartado.
    /// </remarks>
    public static readonly TimeSpan Prazo = TimeSpan.FromDays(30);

    public Guid ProcessoSeletivoId { get; private set; }

    /// <summary>
    /// Sub do usuário autenticado dono do rascunho (via <c>IUserContext</c>) — mesma origem de
    /// <see cref="RascunhoRetificacao.AbertoPorSub"/>.
    /// </summary>
    public string UsuarioSub { get; private set; } = string.Empty;

    /// <summary>
    /// O documento JSON como o cliente o enviou, guardado e devolvido <b>caractere a
    /// caractere</b>.
    /// </summary>
    /// <remarks>
    /// <b>Nunca vai para log, telemetria ou mensagem de erro.</b> O <c>PiiMaskingEnricher</c>
    /// mascara por <b>nome de propriedade</b>, e um documento opaco não expõe nenhum — o nome
    /// do assinante atravessaria qualquer enricher intacto.
    /// </remarks>
    public string Conteudo { get; private set; } = string.Empty;

    /// <summary>
    /// Versão do <b>formato</b> que o cliente escreveu. Rascunho gravado num formato que o
    /// cliente corrente não reconhece é descartado por quem lê, e não traduzido pela metade.
    /// </summary>
    public int Versao { get; private set; }

    /// <summary>
    /// Quando o rascunho deixa de valer. A expiração é aplicada na <b>leitura</b> — não há
    /// job varrendo a tabela, e assumir um seria prometer o que a infraestrutura não entrega.
    /// </summary>
    public DateTimeOffset ExpiraEm { get; private set; }

    /// <summary>
    /// O instante que a tela mostra como "rascunho salvo às …". Sai da auditoria que o
    /// <c>AuditableInterceptor</c> já carimba, em vez de uma coluna própria que diria o mesmo:
    /// <c>UpdatedAt</c> na regravação, <c>CreatedAt</c> enquanto só houve a primeira.
    /// </summary>
    public DateTimeOffset SalvoEm => UpdatedAt ?? CreatedAt;

    private RascunhoDePublicacao() { }

    public static Result<RascunhoDePublicacao> Criar(
        Guid processoSeletivoId,
        string usuarioSub,
        string conteudo,
        int versao,
        DateTimeOffset agora,
        TimeSpan prazo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(usuarioSub);

        if (Recusar(conteudo, versao) is { } recusa)
        {
            return Result<RascunhoDePublicacao>.Failure(recusa);
        }

        return Result<RascunhoDePublicacao>.Success(new RascunhoDePublicacao
        {
            ProcessoSeletivoId = processoSeletivoId,
            UsuarioSub = usuarioSub,
            Conteudo = conteudo,
            Versao = versao,
            ExpiraEm = agora.Add(prazo),
        });
    }

    /// <summary>
    /// Substitui o documento inteiro e renova o prazo. É substituição, e não mesclagem: o
    /// cliente manda a seção completa a cada gravação, então um campo que ele apagou tem de
    /// sumir daqui também.
    /// </summary>
    public Result Substituir(string conteudo, int versao, DateTimeOffset agora, TimeSpan prazo)
    {
        if (Recusar(conteudo, versao) is { } recusa)
        {
            return Result.Failure(recusa);
        }

        Conteudo = conteudo;
        Versao = versao;
        ExpiraEm = agora.Add(prazo);
        return Result.Success();
    }

    public bool Expirou(DateTimeOffset agora) => agora >= ExpiraEm;

    /// <summary>
    /// As duas únicas recusas do servidor. <b>O conteúdo não é validado</b> — rascunho de
    /// formulário pela metade é exatamente o caso de uso, e recusá-lo travaria o operador
    /// justamente quando ele mais precisa guardar o que já transcreveu. A validação forte
    /// vive no comando de publicação, que recebe os dados do ato tipados.
    /// </summary>
    private static DomainError? Recusar(string conteudo, int versao)
    {
        ArgumentNullException.ThrowIfNull(conteudo);

        if (versao <= 0)
        {
            return new DomainError(
                "RascunhoDePublicacao.VersaoInvalida",
                "A versão do formato do rascunho deve ser um inteiro positivo.");
        }

        if (Encoding.UTF8.GetByteCount(conteudo) > ConteudoMaxBytes)
        {
            return new DomainError(
                "RascunhoDePublicacao.ConteudoMuitoGrande",
                $"O rascunho da publicação deve ter no máximo {ConteudoMaxBytes / 1024} KB.");
        }

        return null;
    }
}
