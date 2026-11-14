namespace Unifesspa.UniPlus.Publicacoes.Contracts;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Porta de escrita do registro central de atos (ADR-0170): o domínio que publica a chama na mesma transação em que
/// grava o que publicou, e o ato nasce junto, ou nenhum dos dois existe.
/// </summary>
/// <remarks>
/// <para>
/// A porta <b>não confirma</b>: confere o que o registro exige, reserva a vaga do objeto e deixa o ato no rastreador
/// do contexto único. Quem chama faz a gravação única no fim do comando e, na recusa, descarta as alterações não salvas.
/// Por isso a implementação nunca chama o <c>SaveChanges</c>, o rastreador nem escrita em massa.
/// </para>
/// <para>
/// A recusa de negócio volta como <see cref="Result"/> nomeado, antes de qualquer gravação: ou o tipo não tem versão
/// vigente, ou a vaga do objeto já tem ato vivo do tipo, ou o ato já foi retificado. A violação de unicidade que só a
/// gravação enxerga, quando outra transação ocupa a vaga ou retifica a raiz entre a conferência e a confirmação, é
/// traduzida por <see cref="TraduzirConflitoDaGravacao"/>.
/// </para>
/// </remarks>
public interface IRegistroDeAtoNormativo
{
    /// <summary>Confere o ato, reserva as vagas do objeto e o adiciona ao rastreador, sem gravar.</summary>
    Task<Result> RegistrarAsync(AtoNormativoPublicado ato, CancellationToken cancellationToken);

    /// <summary>
    /// A recusa de negócio que corresponde à violação de unicidade que a gravação levantou, ou <see langword="null"/>
    /// quando a exceção não é violação das constraints do registro de atos, e quem chama deve propagá-la.
    /// </summary>
    DomainError? TraduzirConflitoDaGravacao(Exception excecao);
}
